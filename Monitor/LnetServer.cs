using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Monitor
{
    class LnetServer
    {
        
        public Task ConnectToServer(Action<String> MessageReceived, Func<LnetMessage> BroadcastMessage, string userName, Boolean debugMode)
        {
            var myTask = Task.Factory.StartNew(() =>
            {

                RunClient(MessageReceived, BroadcastMessage, userName, debugMode);
                
                
            });

            return myTask;
        }

        
        private static Hashtable certificateErrors = new Hashtable();
        private static Task<int> myTask;
        private static byte[] buffer;

        private Boolean debugMode;

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }


        private void RunClient(Action<string> messageReceived, Func<LnetMessage> broadcastMessage, String userName, Boolean debugMode)
        {
            this.debugMode = debugMode;

            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile("LnetCert.txt")));
            store.Close();

            // Create a TCP/IP client socket.
            // machineName is the host running the server application.
            TcpClient client = new TcpClient("lnet.lichproject.org", 7155);
            if (debugMode)
            {
                messageReceived.Invoke("Client connected.");
            }
            
            // Create an SSL stream that will close the client's stream.
            SslStream sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );
            // The server name must match the name on the server certificate.
            try
            {
                sslStream.AuthenticateAsClient("lichproject.org");
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    messageReceived.Invoke("Inner exception:"+ e.InnerException.Message);
                }
                messageReceived.Invoke("Authentication failed - closing the connection.");
                client.Close();
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = false;
            var element = doc.CreateElement("login");
            element.SetAttribute("name", userName);
            element.SetAttribute("game", "DR");
            element.SetAttribute("client", "1.5");
            element.SetAttribute("lich", "Custom");

            if (debugMode)
            {
                messageReceived.Invoke("Attempting to connect with:" + element.OuterXml);
                messageReceived.Invoke("Authenticated:" + sslStream.IsAuthenticated.ToString());
            }

            byte[] messsage = Encoding.UTF8.GetBytes(element.OuterXml);
            // Send hello message to the server. 
            sslStream.Write(messsage);
            sslStream.Flush();
            // Read message from the server.

            
            while (client.Connected)
            {
                string serverMessage = ReadMessage(sslStream);
                if(serverMessage != String.Empty)
                {
                    handleMessage(serverMessage, messageReceived, sslStream);                    
                }

                var outMessage = broadcastMessage.Invoke();
                if (outMessage != null)
                {
                    if (outMessage.killServer)
                    {                        
                        client.Close();
                        sslStream.Close();
                        if (debugMode)
                        {
                            messageReceived.Invoke("Client closed.");
                        }
                    }
                    else
                    {
                        element = doc.CreateElement("message");

                        if (outMessage.msgType == "channel")
                        {
                            element.SetAttribute("type", "channel");
                            element.InnerText = outMessage.contents;

                        }
                        else if (outMessage.msgType == "private")
                        {
                            element.SetAttribute("type", "private");
                            element.SetAttribute("to", outMessage.to);
                            element.InnerText = outMessage.contents;
                        }

                        messsage = Encoding.UTF8.GetBytes(element.OuterXml);

                        sslStream.Write(messsage);
                        sslStream.Flush();
                    }                    
                }                
            }

            if (debugMode)
            {
                messageReceived.Invoke("Task Done.");
            }

        }

        private void handleMessage(string serverMessage, Action<string> messageReceived, SslStream sslStream)
        {
            if (debugMode)
            {
                messageReceived.Invoke(serverMessage);
            }
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(serverMessage);

            var item = doc.DocumentElement;            
            
            if (item.Name.Equals("message"))
            {
                var output = String.Empty;
                if (item.Attributes["type"].Value == "greeting")
                {
                    output = item.InnerText;
                }else if(item.Attributes["type"].Value == "private")
                {
                    output = "[Private]-" + item.Attributes["from"].Value +": \""+ item.InnerText+ "\"";
                }else if (item.Attributes["type"].Value == "privateto")
                {
                    output = "[PrivateTo]-" + item.Attributes["to"].Value + ": \"" + item.InnerText+ "\"";
                }
                else if (item.Attributes["type"].Value == "channel")
                {
                    output = "["+ item.Attributes["channel"].Value +"]-" + item.Attributes["from"].Value + ": \"" + item.InnerText+"\"";
                }
                if(output != String.Empty)
                {
                    messageReceived.Invoke(output);
                }
                else
                {
                    if (debugMode)
                    {
                        messageReceived.Invoke(serverMessage);
                    }
                }
                
            }
            else if (item.Name.Equals("ping"))
            {
                if (debugMode)
                {
                    messageReceived.Invoke("Pinged");
                }
                var element = doc.CreateElement("pong");

                var messsage = Encoding.UTF8.GetBytes(element.OuterXml);

                sslStream.Write(messsage);
                sslStream.Flush();
            }else
            {
                if (debugMode)
                {
                    messageReceived.Invoke(serverMessage);
                }
            }            
        }

        static string ReadMessage(SslStream sslStream)
        {
            if(myTask != null)
            {
                if (myTask.IsCompleted)
                {
                    int bytes = myTask.Result;
                    StringBuilder messageData = new StringBuilder();
                    Decoder decoder = Encoding.UTF8.GetDecoder();
                    char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                    decoder.GetChars(buffer, 0, bytes, chars, 0);
                    messageData.Append(chars);

                    myTask = null;                

                    return messageData.ToString();
                }
                return "";
            }
            // Read the  message sent by the server.
            // The end of the message is signaled using the
            // "<EOF>" marker.
            buffer = new byte[2048];            
            myTask = sslStream.ReadAsync(buffer, 0, buffer.Length);
            return "";            
        }


    }    
}
