using GeniePlugin.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Monitor
{
    public class Monitor : IPlugin
    {
        private LnetServer server = new LnetServer();
        private IHost _host;

        public string Author
        {
            get
            {
                return "Seped";
            }
        }

        public string Description
        {
            get
            {
                return "Monitor Custom Window";
            }
        }

        private bool _enabled = true;

        public bool Enabled
        {
            get
            {
                return _enabled;
            }

            set
            {
                _enabled = value;
            }
        }

        public string Name
        {
            get
            {
                return "Monitor";
            }
        }

        public string Version
        {
            get
            {
                return "1.0";
            }
        }

        public void Initialize(IHost Host)
        {
            _host = Host;
        }

        public void ParentClosing()
        {
            stopServer();           
        }

        private LnetMessage PendingMessage()
        {
            var result = outMessage;
            outMessage = null;
            return result;
        }

        private void NewMessage(String input)
        {
            _host.SendText("#echo >LNet " + Regex.Unescape(input));
        }

        private Regex lnet_start = new Regex(@"^\/lnet$", RegexOptions.IgnoreCase);
        private Regex lnet_start_debug = new Regex(@"^\/lnet debug$", RegexOptions.IgnoreCase);
        private Regex lnet_stop = new Regex(@"^\/lnet stop$", RegexOptions.IgnoreCase);

        private Regex lnet_chat = new Regex(@"^;chat (.*)", RegexOptions.IgnoreCase);
        private Regex lnet_priv = new Regex(@"^;chat to (\w+) (.+)", RegexOptions.IgnoreCase); 
        private Task task;
        private LnetMessage outMessage;

        public string startServer(bool debug)
        {            
            if (task != null)
            {
                _host.SendText("#echo >LNet LNet is still running!");
                return "";
            }
            try
            {
                task = server.ConnectToServer(NewMessage, PendingMessage, _host.get_Variable("charactername"), debug);
            }
            catch (Exception e)
            {
                _host.SendText("#echo >LNet [debug] exception:" + e.Message);
            }
            return "";
        }

        public string stopServer()
        {
            if (task != null)
            {
                outMessage = new LnetMessage();                
            }
            return "";
        }

        public string ParseInput(string Text)
        {
            MatchCollection matches = lnet_start_debug.Matches(Text);            
            if (matches.Count > 0)
            {
                return startServer(true);
            }

            matches = lnet_start.Matches(Text);
            if (matches.Count > 0)
            {
                return startServer(false);
            }

            matches = lnet_stop.Matches(Text);
            if (matches.Count > 0)
            {
                return stopServer();
            }

            matches = lnet_priv.Matches(Text);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    GroupCollection groups = match.Groups;                    
                    outMessage = new LnetMessage("private", groups[1].Value, groups[2].Value);
                }
                return "";
            }

            matches = lnet_chat.Matches(Text);
            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    GroupCollection groups = match.Groups;
                    outMessage = new LnetMessage("channel", groups[1].Value);
                }
                return "";
            }


            return Text;            
        }

        public string ParseText(string Text, string Window)
        {   
            return Text;
        }

        public void ParseXML(string XML)
        {            
        }

        public void Show()
        {         
        }

        public void VariableChanged(string Variable)
        {         
        }
    }
}
