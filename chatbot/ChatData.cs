using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatbot {

    [Serializable]
    public class ChatData {

        public string text;
        public DateTime time;

        public ChatData(string _text, DateTime _time) {
            text = _text;
            time = _time;
        }

    }
}
