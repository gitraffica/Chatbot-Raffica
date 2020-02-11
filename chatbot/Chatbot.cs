using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace chatbot {

    public class FeatureList {
        public Dictionary<string, double> feature;
        public List<Tuple<string, double>> relation;                //相同的词汇出现复数次
        public FeatureList() {
            feature = new Dictionary<string, double>();
            relation = new List<Tuple<string, double>>();
        }
    }

    public class Chatbot {

        public LongMemory LM;
        public ShortMemory SM;
        const double INF = 1e9 + 7;
        public double nearEPS = 0.01;
        int gram = 5;                       //滑动窗口的大小
        public bool DELCorrectness = false;
        public string DELCorrectItem = "";
        private string HELP = "使用说明" + Environment.NewLine
            + "raffica.top/bot/readme.pdf";

        #region Trivials
        public Chatbot() {
            LM = new LongMemory();
            SM = new ShortMemory();
            try {
                SerializeSavor.LoadChatDataList(LM.groundTruth  , "LM.txt");
                SerializeSavor.LoadChatDataList(SM.memory       , "SM.txt");
                SerializeSavor.LoadChatDataList(LM.knowledge    , "KL.txt");
            } catch(Exception e) {
                SerializeSavor.SaveChatDataList(LM.groundTruth  , "LM.txt");
                SerializeSavor.SaveChatDataList(SM.memory       , "SM.txt");
                SerializeSavor.SaveChatDataList(LM.knowledge    , "KL.txt");
                MessageBox.Show(e.Message);
                MessageBox.Show("没有创建记忆文件，已自动创建");
            }
        }

        public static void Log(string x) { MessageBox.Show(x); }
        #endregion

        #region Tag分类
        public string BotInput(string input) {
            if (input.StartsWith("<HELP>")) {
                return HELP;
            }
            if (input.StartsWith("<TEA>")) {
                string content = input.Substring(5, input.Length - 5);
                content = content.Trim();
                if (content == "") {
                    return "¿";
                }
                return Teach(content);
            }
            if(input.StartsWith("<READ>")) {
                string toOutput = "";
                toOutput += "Ground Truth:" + Environment.NewLine;
                foreach(var i in LM.groundTruth) {
                    toOutput += i.time + "\t" + i.text + Environment.NewLine;
                }
                toOutput += "Knowledge:" + Environment.NewLine;
                foreach (var i in LM.knowledge) {
                    toOutput += i.time + "\t" + i.text + Environment.NewLine;
                }
                return toOutput;
            }
            if(input.StartsWith("<DEL>")) {
                string content = input.Substring(5, input.Length - 5);
                content = content.Trim();
                ChatData cor = SingletonClassify(LM.groundTruth, content, gram);
                ChatData corKL = SingletonClassify(LM.knowledge, content, gram);
                double simCOR = (cor == null ? -114514 : Similarity(cor.text, content, gram));
                double simCORKL = (corKL == null ? -114514 : Similarity(corKL.text, content, gram));
                if (simCORKL > simCOR) cor = corKL;
                if (cor != null && !DELCorrectness) {
                    DELCorrectness = true;
                    DELCorrectItem = content;
                    return "请再次输入相同的指令以确认你要删除的知识。" + Environment.NewLine
                        + "要取消删除，输入<DEL>+任意不同指令。" + Environment.NewLine
                        + "要删除的知识是：" + cor.text + Environment.NewLine;
                } else if(cor != null && DELCorrectness) {
                    DELCorrectness = false;
                    if (DELCorrectItem == content) {
                        DELCorrectItem = "";
                        cor.text = "<DEL>" + cor.text;
                        SerializeSavor.SaveChatDataList(LM.groundTruth, "LM.txt");
                        SerializeSavor.SaveChatDataList(LM.knowledge,   "KL.txt");
                        return "删除完成。" + Environment.NewLine + "删除的知识是：" + cor.text + Environment.NewLine;
                    } else {
                        DELCorrectItem = "";
                        return "已取消删除。";
                    }
                } else return "没能找到要删除的知识" + Environment.NewLine;         //防止知识库是空的
            }
            if(input.StartsWith("<CHAT>")) {
                string content = input.Substring(6, input.Length - 6);
                content = content.Trim();
                return Chat(content);
            }
            if(input.StartsWith("<CLEAR>")) {
                SM.memory.Clear();
                return "短时记忆清空完成" + Environment.NewLine;
            }
            if(input.StartsWith("<MEM>")) {
                return SM.outputMemory();
            }
            if(input.StartsWith("<ADDMEM>")) {
                string content = input.Substring(8, input.Length - 8);
                content = content.Trim();
                bool res = SM.AddMemory(content);
                if (res) {
                    return "已加入缓存。加入的内容为：" + Environment.NewLine + content + Environment.NewLine + "<NEXT>";
                } else {
                    return "内容为：" + content + Environment.NewLine + "的指令已被写入缓存，请勿重复添加。";
                }
            }
            return "¿";
        }
        #endregion

        #region 聊天
        public string Chat(string content) {
            ChatData cor = SingletonClassify(LM.knowledge, content, gram);
            ChatData cor2 = SingletonClassify(LM.knowledge, content, gram, true);
            double simA = Similarity(cor.text, content, gram);
            double sim1 = (cor2 == null ? -114514 : Similarity(GetToken(cor2.text, "<Q1>"), content, gram));
            double sim2 = (cor2 == null ? -114514 : Similarity(GetToken(cor2.text, "<Q2>"), content, gram));
            if ((cor == null
                || simA < Math.Max(sim1, sim2)) && GetToken(content, "<NEXT>").CompareTo("<NOTFOUND>") == 0) {                       //知识库里没有相关的问答
                string nearestKL = "";
                if (sim2 > sim1) {
                    nearestKL = GetToken(cor2.text, "<Q2>");
                    sim1 = sim2;
                } else {
                    nearestKL = GetToken(cor.text, "<Q1>");
                }
                if (sim1 < nearEPS) {
                    return "未能找到适合的回复。使用下列语句为bot追加语料。" + Environment.NewLine
                    + "<TEA><Q>" + content + "<A>" + Environment.NewLine
                    + "<TEA><Q1><Q2>" + content + "<A>" + Environment.NewLine;
                } else {
                    string reply = StringAlignment(nearestKL, nearestKL, content);
                    reply = reply.Substring(1, reply.Length - 2);
                    bool res = SM.AddMemory(reply);
                    if (res) {
                        return "已加入缓存。加入的内容为：" + Environment.NewLine + reply + Environment.NewLine + "<NEXT>";
                    } else {
                        return "内容为：" + reply + Environment.NewLine + "的指令已被写入缓存，请勿重复添加。";
                    }
                }
            } else {
                content = content.Replace("<NEXT>", "");
                string replyNoInference = GetToken(cor.text, "<A>");
                string reply = replyNoInference;
                double maxSim = 0;
                maxSim = SimilarityByClass(cor.text, content, gram);
                ChatData ansData = cor;
                List<ChatData> findList = SM.memory.ConvertAll(chatdata => new ChatData(chatdata.text, chatdata.time));
                findList.AddRange(LM.groundTruth);
                string toAlign = content;
                foreach (var i in findList) {
                    if (i.text.Contains("<DEL>")) continue;
                    string splicedStr = "";
                    ChatData cur = null;
                    double sim = 0;

                    splicedStr = content + i.text;
                    cur = SingletonClassify(LM.knowledge, splicedStr, gram);
                    sim = SimilarityByClass(cur.text, splicedStr, gram);
                    if(maxSim < sim) {
                        maxSim = sim;
                        toAlign = splicedStr;
                        reply = GetToken(cur.text, "<A>");
                        ansData = cur;
                    }

                    splicedStr = i.text + content;
                    cur = SingletonClassify(LM.knowledge, splicedStr, gram);
                    sim = SimilarityByClass(cur.text, splicedStr, gram);
                    //Log(sim + " " + maxSim + " " + splicedStr + " " + cur.text);
                    if (maxSim < sim) {
                        maxSim = sim;
                        toAlign = splicedStr;
                        reply = GetToken(cur.text, "<A>");
                        ansData = cur;
                    }
                }//<CHAT>d平行于e
                if (GetToken(ansData.text, "<Q>") != "<NOTFOUND>") {
                    reply = StringAlignment(GetToken(ansData.text, "<Q>"), GetToken(ansData.text, "<A>"), toAlign);
                } else if(GetToken(ansData.text, "<Q1>") != "<NOTFOUND>" && GetToken(ansData.text, "<Q2>") != "<NOTFOUND>") {
                    reply = StringAlignment(GetToken(ansData.text, "<Q1>") + GetToken(ansData.text, "<Q2>"), GetToken(ansData.text, "<A>"), toAlign);
                }
                reply = "回答：" + reply.Substring(1, reply.Length - 2);
                reply += Environment.NewLine + "原句为：" + ansData.text;
                reply += Environment.NewLine + "合并句为：" + toAlign;
                reply += Environment.NewLine + "概率为" + maxSim;
                return reply;
            }
        }
        #endregion

        #region 教学
        private string Teach(string content) {
            if (content.Contains("<Q>") && content.Contains("<A>")) {         //同时含有<Q>和<A>
                //<TEA><Q>fuck you<A>fuck you leather man
                string Q = GetToken(content, "<Q>");
                string A = GetToken(content, "<A>");
                string text = "<Q>" + Q + "<A>" + A;
                ChatData x = new ChatData(text, DateTime.Now);
                ChatData nearest = SingletonClassify(LM.knowledge, x.text, gram);
                if (nearest == null || Similarity(x.text, nearest.text, gram) < nearEPS) {
                    LM.knowledge.Add(x);
                    SerializeSavor.SaveChatDataList(LM.knowledge, "KL.txt");
                    return "Q: " + Q + Environment.NewLine + "A:" + A + Environment.NewLine + "学习完成";
                } else {
                    double sim = Similarity(x.text, nearest.text, gram);
                    if (sim > INF - 1) {
                        return "知识库中已经有相同的知识！";
                    } else {
                        if (content.Contains("<EXACT>")) {
                            LM.knowledge.Add(x);
                            SerializeSavor.SaveChatDataList(LM.knowledge, "KL.txt");
                            return "Q: " + Q + Environment.NewLine + "A:" + A + Environment.NewLine + "学习完成" + Environment.NewLine;
                        } else {
                            return "知识库中已有相似知识。请在<TEA>后追加<EXACT>指令。" + Environment.NewLine
                                + "相似知识为：" + nearest.text + Environment.NewLine;
                        }
                    }
                }
            } else if (content.Contains("<G>")) {
                string G = GetToken(content, "<G>");
                string text = G;
                ChatData x = new ChatData(text, DateTime.Now);
                ChatData nearest = SingletonClassify(LM.groundTruth, x.text, gram);
                if (nearest == null || Similarity(x.text, nearest.text, gram) < nearEPS) {
                    LM.groundTruth.Add(x);
                    SerializeSavor.SaveChatDataList(LM.groundTruth, "LM.txt");
                    return G + Environment.NewLine + "学习完成";
                } else {
                    double sim = Similarity(x.text, nearest.text, gram);
                    if (sim > INF - 1) {
                        return "知识库中已经有相同的知识！";
                    } else {
                        if (content.Contains("<EXACT>")) {
                            LM.groundTruth.Add(x);
                            SerializeSavor.SaveChatDataList(LM.groundTruth, "LM.txt");
                            return G + Environment.NewLine + "学习完成";
                        } else {
                            return "知识库中已有相似知识。请在<TEA>后追加<EXACT>指令。" + Environment.NewLine
                                + "相似知识为：" + nearest.text + Environment.NewLine;
                        }
                    }
                }
            } else if (content.Contains("<A>") && content.Contains("<Q1>") && content.Contains("<Q2>")) {
                string Q1 = GetToken(content, "<Q1>");
                string Q2 = GetToken(content, "<Q2>");
                string A = GetToken(content, "<A>");
                string text = "<Q1>" + Q1 + "<Q2>" + Q2 + "<A>" + A;
                ChatData x = new ChatData(text, DateTime.Now);
                ChatData nearest = SingletonClassify(LM.knowledge, x.text, gram);
                if (nearest == null || Similarity(x.text, nearest.text, gram) < nearEPS) {
                    LM.knowledge.Add(x);
                    SerializeSavor.SaveChatDataList(LM.knowledge, "KL.txt");
                    return "Q1: " + Q1 + Environment.NewLine
                        + "Q2" + Q2 + Environment.NewLine
                        + "A:" + A + Environment.NewLine + "学习完成";
                } else {
                    double sim = Similarity(x.text, nearest.text, gram);
                    if (sim > INF - 1) {
                        return "知识库中已经有相同的知识！";
                    } else {
                        if (content.Contains("<EXACT>")) {
                            LM.knowledge.Add(x);
                            SerializeSavor.SaveChatDataList(LM.knowledge, "KL.txt");
                            return "Q1: " + Q1 + Environment.NewLine
                        + "Q2" + Q2 + Environment.NewLine
                        + "A:" + A + Environment.NewLine + "学习完成";
                        } else {
                            return "知识库中已有相似知识。请在<TEA>后追加<EXACT>指令。" + Environment.NewLine
                                + "相似知识为：" + nearest.text + Environment.NewLine;
                        }
                    }
                }
            } else {
                return "格式错误。";
            }
        }
        #endregion

        #region 分类

        public static List<Tuple<string, double>> RepeatNormalize(string input) {
            List<Tuple<string, double>> res = new List<Tuple<string, double>>();
            for (int i = 0; i < input.Length; i++) {
                double num = input.Substring(0, i + 1).Count(f => f == input[i]) / input.Count(f => f == input[i]);     //位置
                res.Add(new Tuple<string, double>(input.Substring(i, 1), num));
            }
            return res;
        }

        /* FindNearestRelavance
         * 找到input中最可能对应到target的部分。如果对应不了，返回-1
         * */

        public static int FindNearestRelevance(List<Tuple<string, double>> input, Tuple<string, double> target) {
            double minDiff = 100000;
            int argmin = -1;
            for (int i = 0; i < input.Count; i++) {
                if (input[i].Item1.CompareTo(target.Item1) == 0) {
                    if (minDiff > Math.Abs(input[i].Item2 - target.Item2)) {
                        argmin = i;
                        minDiff = Math.Abs(input[i].Item2 - target.Item2);
                    }
                }
            }
            return argmin;
        }
        public static string StringAlignment(string input, string output, string userInput) {
            string reply = "";
            input = "か" + input + "ぐ";
            userInput = "か" + userInput + "ぐ";
            output = "が" + output + "く";
            List<Tuple<string, double>> inputList = new List<Tuple<string, double>>();
            List<Tuple<string, double>> outputList = new List<Tuple<string, double>>();
            List<Tuple<string, double>> userInputList = new List<Tuple<string, double>>();
            List<int> correspondInputOutputList = new List<int>();
            List<int> correspondInputUserInputList = new List<int>();
            inputList = RepeatNormalize(input);
            outputList = RepeatNormalize(output);
            userInputList = RepeatNormalize(userInput);
            //第二次循环，找出user input和标准input中的相同点
            //注意力集中在不同点上。相同点对应的output和正常的相同
            //但是不同点对应的output则应该有相应的变化。
            int nowIDX = 0;
            for (int i = 0; i < outputList.Count; i++) {
                var x = outputList[i];
                int nearest = FindNearestRelevance(userInputList, x);
                int homoNearest = FindNearestRelevance(inputList, x);       //本物のnearest
                if(homoNearest != -1 && nearest == -1) {      //如果这个回答本来是有对应的地方的，然后用户输入没有这个对应的地方
                    bool kick = false;
                    int start = FindNearestRelevance(userInputList, inputList[homoNearest - 1]);
                    for(int j = start+1;j < inputList.Count;j ++) {
                        int correspond = FindNearestRelevance(userInputList, inputList[j]);
                        if (correspond != -1) {              //找到了对应的输入
                            if(correspond - start - 1 >= 0) reply += userInput.Substring(start+1, correspond - start - 1);
                            //<CHAT>说出A、B、奥尔加右边的那个东西
                            kick = true;
                            break;
                        }
                    }
                    if(!kick) {
                        reply += userInput.Substring(start, userInputList.Count - start);
                        break;
                    }
                } else if(homoNearest != -1 && nearest != -1) {                     //完全对应
                    reply += outputList[i].Item1;
                    nowIDX = nearest;
                } else if(homoNearest == -1) {                                      //完全没有对应
                    reply += outputList[i].Item1;
                }
            }
            return reply;
        }

        public static double SimilarityByClass(string classify, string b, int gram) {
            if (GetToken(classify, "<Q>") != "<NOTFOUND>") {
                return Similarity(GetToken(classify, "<Q>"), b, gram);
            } else if (GetToken(classify, "<Q1>") != "<NOTFOUND>" && GetToken(classify, "<Q2>") != "<NOTFOUND>") {
                return Similarity(GetToken(classify, "<Q1>") + GetToken(classify, "<Q2>"), b, gram);
            }
            return -114514;
        }

        public static double Similarity(string a, string b, int gram) {
            double res = 0;
            if(a.CompareTo(b) == 0) {               //完全相等
                return INF;
            }
            var dictA = GetFeature(a, gram);
            var dictB = GetFeature(b, gram);
            foreach(var i in dictA.feature) {
                if (dictB.feature.ContainsKey(i.Key)) res += i.Value * i.Value;
            }
            foreach(var i in dictA.relation) {
                double minRelation = INF;
                foreach(var j in dictB.relation) {
                    if(Math.Abs(i.Item2 - j.Item2) < minRelation) {
                        minRelation = Math.Abs(i.Item2 - j.Item2);
                    }
                }
                res += 1 / (minRelation + 1);
            }
            //<CHAT>d平行于e
            res /= (a.Length + 1) * (b.Length + 1) * (Math.Abs(a.Length - b.Length) + 1);
            return res;
        }

        public static string GetToken(string content, string token) {
            try {
                string res = "";
                int begin = content.IndexOf(token) + token.Length;
                int end = content.IndexOf('<', begin);
                if (end == -1) end = content.Length;
                if (content.IndexOf(token) == -1) return "<NOTFOUND>";
                res = content.Substring(begin, end - begin);
                return res;
            } catch {
                return "<NOTFOUND>";
            }
        }

        public static FeatureList GetFeature(string content, int gram) {
            FeatureList res = new FeatureList();
            Dictionary<string, double> dict = new Dictionary<string, double>();
            for (int i = 1; i <= Math.Min(gram, content.Length); i++) {       //特征的长度
                for (int j = 0; j <= content.Length - i; j++) {                //截取特征的开始处
                    string key = content.Substring(j, i);
                    if (dict.ContainsKey(key)) {
                        dict[key] += i * i;
                        res.relation.Add(new Tuple<string, double>(key, (double)j / content.Length));
                    } else dict[key] = i * i;
                }
            }
            res.feature = dict;
            return res;
        }

        //is2PreKnowledge表示 2个前提的知识中去划分出到底是哪个前提
        public static ChatData SingletonClassify(List<ChatData> list, string input, int gram, bool is2PreKnowledge = false) {
            ChatData argmax = null;
            double maxLikelihood = 0;
            FeatureList inputDict;
            FeatureList LMDict;
            inputDict = GetFeature(input, gram);
            for (int R = 0;R < list.Count;R ++) {
                double likelihood = 0;
                if (!is2PreKnowledge) {                         //如果是从list中提取全部的话
                    string content = list[R].text;
                    if (content.Contains("<DEL>")) continue;
                    if (content.Contains("<Q>")) content = GetToken(content, "<Q>");
                    if (content.Contains("<Q1>") && content.Contains("<Q2>")) content = GetToken(content, "<Q1>") + GetToken(content, "<Q2>");
                    LMDict = GetFeature(content, gram);
                    foreach (var i in inputDict.feature) {
                        if (LMDict.feature.ContainsKey(i.Key)) {
                            likelihood += LMDict.feature[i.Key] * i.Value;
                        }
                    }
                } else {
                    string content = list[R].text;
                    if (content.Contains("<Q1>") && content.Contains("<Q2>")) {
                        content = GetToken(content, "<Q1>");
                        LMDict = GetFeature(content, gram);
                        foreach (var i in inputDict.feature) {
                            if (LMDict.feature.ContainsKey(i.Key)) {
                                likelihood += LMDict.feature[i.Key] * i.Value;
                            }
                        }
                        if (likelihood > maxLikelihood) {
                            argmax = list[R];
                            maxLikelihood = likelihood;
                        }
                        likelihood = 0;
                        content = GetToken(content, "<Q2>");
                        LMDict = GetFeature(content, gram);
                        foreach (var i in inputDict.feature) {
                            if (LMDict.feature.ContainsKey(i.Key)) {
                                likelihood += LMDict.feature[i.Key] * i.Value;
                            }
                        }
                    }                       //else is a <Q> item
                }
                if (likelihood > maxLikelihood) {
                    argmax = list[R];
                    maxLikelihood = likelihood;
                }
            }
            return argmax;
        }
        #endregion
    }

}