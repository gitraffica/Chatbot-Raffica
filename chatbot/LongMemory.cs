using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chatbot {

    [Serializable]
    public class LongMemory {

        public List<ChatData> groundTruth;
        public List<ChatData> knowledge;
        
        public LongMemory() {
            groundTruth = new List<ChatData>();
            knowledge = new List<ChatData>();
        }

    }

}
