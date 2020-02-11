using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatbot {

    [Serializable]
    public class ShortMemory {
        public List<ChatData> memory;
        public int gram = 5;
        public double nearEPS = 114513;

        public ShortMemory() {
            memory = new List<ChatData>();
        }

        public bool AddMemory(string x) {
            var nearChat = Chatbot.SingletonClassify(memory, x, gram);
            string near = (nearChat != null ? nearChat.text : "");
            if(Chatbot.Similarity(near, x, gram) > nearEPS) {               //记忆体已经有相似的东西
                return false;
            }
            ChatData t = new ChatData(x, DateTime.Now);
            memory.Add(t);
            return true;
        }

        public string outputMemory() {
            string res = "";
            foreach(var i in memory) {
                res += i.time + "\t" + i.text + Environment.NewLine;
            }
            return res;
        }

    }

}
