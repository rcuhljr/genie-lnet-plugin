using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitor
{
    class LnetMessage
    {
        public LnetMessage(String MsgType, String Contents)
        {
            contents = Contents;
            msgType = MsgType;
            killServer = false;
        }

        public LnetMessage(String MsgType, String To, String Contents)
        {
            contents = Contents;
            to = To;
            msgType = MsgType;
            killServer = false;
        }

        public LnetMessage()
        {
            killServer = true;
        }


        
        public string contents { get; private set; }
        public string msgType { get; private set; }
        public string to { get; private set; }
        public bool killServer{ get; private set; }
    }
}
