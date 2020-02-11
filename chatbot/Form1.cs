using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace chatbot {
    public partial class Form1 : Form {

        public Chatbot bot;
        public List<string> messageRecord = new List<string>();
        public int messagePointer = 0;

        public Form1() {
            InitializeComponent();
            textBox1.KeyDown += textBox1_KeyDown;
            messageRecord.Add("<READ>");
            messageRecord.Add("<TEA><EXACT><Q><A>");
            messageRecord.Add("<TEA><EXACT><Q1><Q2><A>");
            messageRecord.Add("<TEA><G>");
            messageRecord.Add("<CHAT>");
        }

        private void Form1_Load(object sender, EventArgs e) {
            bot = new Chatbot();
            ActiveControl = textBox1;
        }

        private void button1_Click(object sender, EventArgs e) {
            string input = textBox1.Text;
            handleInput(input);
            textBox1.Text = "";
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
            
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                string input = textBox1.Text;
                handleInput(input);
                textBox1.Text = "";
                messagePointer = messageRecord.Count - 1;
            }
            if(e.KeyCode == Keys.Up) {
                messagePointer = messagePointer > 0 ? messagePointer - 1 : 0;
                textBox1.Text = messageRecord[messagePointer];
            }
            if (e.KeyCode == Keys.Down) {
                messagePointer = messagePointer < messageRecord.Count-1 ? messagePointer + 1 : messageRecord.Count-1;
                textBox1.Text = messageRecord[messagePointer];
            }
        }

        public void handleInput(string text) {
            text = Regex.Replace(text, @"[\n\r]+", "");
            messageRecord.Add(text);
            string ans = "";
            textBox2.AppendText(text + Environment.NewLine);
            int cnt = 0;
            do {
                if (cnt > 0) text = text + "<NEXT>";
                ans = bot.BotInput(text) + Environment.NewLine;
                textBox2.AppendText(ans);
                cnt++;
                if (cnt > 5) break;
            } while (Chatbot.GetToken(ans, "<NEXT>").CompareTo("<NOTFOUND>") != 0);
        }
    }
}
