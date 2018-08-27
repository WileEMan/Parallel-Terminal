using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Principal;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Xml.Linq;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;

namespace Parallel_Terminal
{
    /// <summary>
    /// Slave_Connection is the host (terminal) code that handles the network connection to a slave.  It is responsible for opening and managing the connection,
    /// and for protocols in communicating with the slave.
    /// </summary>
    public class Slave_Connection : Core_Library.CommonCore, IDisposable
    {       
        Thread WorkerThread;
        bool Stopping = false;
        bool SilentFail;

        public string HostName;
        RegistryCertificateStore AcceptedCertificates;

        object ExceptionLock = new object();
        Exception PendingException;
        bool FailedSilently = false;

        // Password should be cleared after it is no longer necessary.
        string Domain, UserName, Password;

        public Slave_Connection(string HostName, RegistryCertificateStore AcceptedCertificateStore)
        {
            this.HostName = HostName;
            this.AcceptedCertificates = AcceptedCertificateStore;
        }

        public void Open(bool SilentFail, string Domain, string UserName, string Password)
        {
            if (WorkerThread != null)
            {
                if (this.FailedSilently)
                {
                    Stopping = true;
                    WorkerThread.Join();
                    WorkerThread = null;
                }
                else if (this.SilentFail && !SilentFail)
                {
                    // The previous Open() call was done in silent fail mode.  The new call was user-initiated and is to display errors.  We want to
                    // abort the previous attempt.
                    Stopping = true;
                    WorkerThread.Join();
                    WorkerThread = null;
                }
                else
                {
                    throw new Exception("Already opened.");
                }
            }
            this.SilentFail = SilentFail;
            this.FailedSilently = false;
            this.Domain = Domain;
            this.UserName = UserName;
            this.Password = Password;

            CurrentState_ = Connection_State.Connecting;
            if (OnConnectionState != null) OnConnectionState(this, Connection_State.Connecting);

            Stopping = false;
            WorkerThread = new Thread(WorkerThreadEntry);
            WorkerThread.Name = "Slave_Connection Worker (" + HostName + ")";
            WorkerThread.Start();
        }

        public void Close()
        {
            Stopping = true;
            if (WorkerThread != null)
            {
                WorkerThread.Join();
                WorkerThread = null;
            }
            FailedSilently = false;
            Password = "";
        }

        public Exception GetLastError()
        {
            Exception ret;
            lock (ExceptionLock)
            {
                ret = PendingException;
                PendingException = null;
            }
            if (ret is ConnectionException)
            {
                if (this.SilentFail)
                {
                    this.FailedSilently = true;
#if DEBUG
                    Debug.WriteLine(ret.ToString());
#endif
                    ret = null;

                    CurrentState_ = Connection_State.Disconnected;
                    if (OnConnectionState != null) OnConnectionState(this, Connection_State.Disconnected);
                }
            }
            return ret;
        }

        object ClientStreamLock = new object();
        //NegotiateStream ClientStream;
        SslStream ClientStream;

        // The RemoteCertificateValidationCallback annoyingly doesn't give us a reference to this class.  It does reference the SslStream though.  So we have to
        // go to the work of finding the matching class for the SslStream given from a global, locked list.  The list is only maintained for this single API call.
        static object GlobalLock = new object();
        static Dictionary<SslStream, Slave_Connection> ConnectingStreams = new Dictionary<SslStream, Slave_Connection>();

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            Slave_Connection sc;
            lock (GlobalLock)
            {
                if (!ConnectingStreams.ContainsKey((SslStream)sender)) throw new Exception("Expected connecting streams table to contain any stream being validated.");
                sc = ConnectingStreams[(SslStream)sender];
            }
            return sc.ValidateServerCertificate(certificate, chain, sslPolicyErrors);
        }

        private bool ValidateServerCertificate(
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            // TODO: this callback would be better suited as in the MainForm GUI code.  It's popping a dialog box and accessing the registry key to look at a global list
            // of accepted certificates that is managed by the MainForm anyway.  Can then get rid of the reference to the global list in the constructor.

            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            //Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            bool PreviouslyAccepted = AcceptedCertificates.IsStored(GetFQDN(HostName), new X509Certificate2(certificate));
            if (PreviouslyAccepted) return true;

            lock (ExceptionLock)
            {
                // Do not allow this client to communicate with unauthenticated servers without asking user.
                if (this.SilentFail) return false;
            }            

            ValidateCertificateForm vcf = new ValidateCertificateForm(certificate);
            if (vcf.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;

            // User has given us permission to proceed regardless.  We should save the certificate for next time.
            AcceptedCertificates.Store(GetFQDN(HostName), new X509Certificate2(certificate));
            return true;
        }

        TcpClient client;

        void EstablishConnection()
        {
            // Establish the remote endpoint for the socket.                
            IPHostEntry ipHostInfo = Dns.GetHostEntry(HostName);
            IPAddress ipAddress = ipHostInfo.AddressList[0];            
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, PortNumber);
            // Create a TCP/IP socket.            
            client = new TcpClient(ipAddress.AddressFamily);
            // Connect the socket to the remote endpoint.
            client.Connect(remoteEP);
#if DEBUG
            System.Diagnostics.Debug.WriteLine("TCP client connected to " + remoteEP.ToString());
#endif

            // Ensure the client does not close when there is still data to be sent to the server.
            client.LingerState = (new LingerOption(true, 0));
            lock (ClientStreamLock)
            {
                // Request authentication.
                //ClientStream = new NegotiateStream(client.GetStream(), false);
                //ClientStream.AuthenticateAsClient();

                ClientStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

                // We're going to have a callback to the static ValidateServerCertificate once we call AuthenticateAsClient.  We're going to need
                // some context in that callback function, so we need to find this Slave_Connection object and call the non-static 
                // ValidateServerCertificate.  Since they didn't give us an opaque token to the callback, we have to find ourselves some other
                // way.  The callback does receive the SslStream on which we initiated the request as 'sender'.  So we setup a global/static dictionary
                // that can point back to here just long enough to accomplish this, then we'll remove ourselves from the dictionary.
                lock (GlobalLock) ConnectingStreams.Add(ClientStream, this);
                try
                {
                    ClientStream.ReadTimeout = 1500;
                    ClientStream.AuthenticateAsClient(GetFQDN(HostName), null, SslProtocols.Tls12, true);          // The server name specified here needs to match the certificate.
                }
                catch (Exception ex)
                {
                    // Remove ourselves from the dictionary.
                    // Can't do this with a finally block because we are next going to null out the ClientStream.
                    lock (GlobalLock)
                    {
                        if (ConnectingStreams.ContainsKey(ClientStream))
                            ConnectingStreams.Remove(ClientStream);
                    }
                    try { ClientStream.Close(); ClientStream = null; } finally { throw ex; }
                }
                // Remove ourselves from the dictionary.  
                lock (GlobalLock)
                {
                    if (ConnectingStreams.ContainsKey(ClientStream))
                        ConnectingStreams.Remove(ClientStream);
                }

                if (!ClientStream.IsAuthenticated) throw new Exception("Expected authenticated communication layer.");
                if (!ClientStream.IsEncrypted) throw new Exception("Expected encrypted communication layer after authentication.");

#if DEBUG
                System.Diagnostics.Debug.WriteLine("TCP authentication complete [terminal]!");
#endif
                
                XElement xMsg = new XElement("Connect-Terminal", 
                    new XAttribute("Domain", Domain),
                    new XAttribute("UserName", UserName),
                    new XAttribute("Password", Password)
                    );
                string Msg = xMsg.ToString();
                byte[] RawMsg = Encoding.Unicode.GetBytes(Msg);                
                SendMessage(ClientStream, RawMsg);
            }

            CurrentState_ = Connection_State.EncryptionEstablished;
            if (OnConnectionState != null) OnConnectionState(this, Connection_State.EncryptionEstablished);
        }

        public class ConnectionException : Exception
        {
            public ConnectionException(string Message, Exception innerException) : base(Message, innerException) { }
        }

        protected void WorkerThreadEntry()
        {
            try
            {
                EstablishConnection();
                this.Password = "";
            }
            catch (Exception ex)
            {
                this.Password = "";
                lock (ExceptionLock)
                    if (PendingException == null) PendingException = new ConnectionException("Unable to connect to '" + HostName + "': " + ex.Message, ex);
                return;
            }

            try
            { 
                while (IsStillConnected(client) && !Stopping)
                {
                    // PollConnection() checks and assembles incoming messages and calls OnMessage() when one is ready.
                    List<byte[]> Messages = null;
                    lock (ClientStreamLock)
                    {                        
                        if (client.GetStream().DataAvailable)
                            Messages = PollConnection(ClientStream);
                    }
                    if (Messages != null)
                    {
                        foreach (byte[] Message in Messages) OnMessageReceived(Message);
                    }

                    Thread.Sleep(10);
                }

                //if (IsStillConnected(client))
                lock (ClientStreamLock)
                {
                    if (ClientStream != null) { ClientStream.Close(); ClientStream.Dispose(); ClientStream = null; }
                    client.Close();
                }

                #if DEBUG
                System.Diagnostics.Debug.WriteLine("TCP disconnected.");
                #endif

                CurrentState_ = Connection_State.Disconnected;
                if (OnConnectionState != null) OnConnectionState(this, Connection_State.Disconnected);
            }
            catch (Exception e)
            {
                this.Password = "";
                lock (ExceptionLock)
                    if (PendingException == null) PendingException = e;
                return;
            }
        }

        public delegate void MessageHandler(Slave_Connection SC, XElement xMsg);
        public event MessageHandler OnMessage;
        
        /// <summary>
        /// OnMessageRecieved() gets called when the slave has transmitted something, such as a <StdOut/> message.  The message
        /// is sent to the event handler after conversion to XML.  Note that this routine and the OnMessage delegate will be
        /// called from the worker thread.
        /// </summary>
        void OnMessageReceived(byte[] RawMessage)
        {
            string Message = Encoding.Unicode.GetString(RawMessage);
            XElement xMessage = XElement.Parse(Message, LoadOptions.PreserveWhitespace);
            if (OnMessage != null)
            {
                if (xMessage.Name == "Terminal-Connected")
                {
                    CurrentState_ = Connection_State.Connected;
                    if (OnConnectionState != null) OnConnectionState(this, Connection_State.Connected);
                }

                OnMessage(this, xMessage);
            }
        }

        public enum Connection_State
        {
            Disconnected,
            Connecting,
            EncryptionEstablished,
            Connected
        }

        public delegate void StateHandler(Slave_Connection SC, Connection_State NewState);
        public event StateHandler OnConnectionState;

        private Connection_State CurrentState_;
        public Connection_State CurrentState { get { return CurrentState_; } }                

        /// <summary>
        /// SendConsoleInput() is a public function that the terminal can call to instruct the Slave_Connection to deliver text to the 
        /// console on the slave.
        /// </summary>
        /// <param name="NewText"></param>        
        public void SendConsoleInput(List<wb.ConsoleApi.KEY_EVENT_RECORD> Keys)
        {
            //string EncodedText = System.Convert.ToBase64String(Encoding.Unicode.GetBytes(NewText));
            //string EncodedText = System.Convert.ToBase64String(ConsoleEncoding.GetBytes(NewText));
            XElement xMsg = new XElement("Console-Input");
            foreach (var Key in Keys)
            {
                xMsg.Add(
                    new XElement("Key",
                        new XAttribute("down", Key.bKeyDown.ToString()),
                        new XAttribute("ctrl", ((UInt32)Key.dwControlKeyState).ToString()),
                        new XAttribute("char", ((UInt32)Key.UnicodeChar).ToString()),
                        new XAttribute("repeat", ((UInt16)Key.wRepeatCount).ToString()),
                        new XAttribute("key", ((UInt16)Key.wVirtualKeyCode).ToString()),
                        new XAttribute("scan", ((UInt16)Key.wVirtualScanCode).ToString())
                        ));
            }
            string Msg = xMsg.ToString();
            byte[] RawMsg = Encoding.Unicode.GetBytes(Msg);
            
            lock (ClientStreamLock) SendMessage(ClientStream, RawMsg);            
        }

        public void SendReloadConsoleRequest()
        {
            XElement xMsg = new XElement("Reload-Console");
            string Msg = xMsg.ToString();
            byte[] RawMsg = Encoding.Unicode.GetBytes(Msg);
            lock (ClientStreamLock) SendMessage(ClientStream, RawMsg);
        }

        public void Dispose()
        {
            if (!Stopping) Close();
            GC.SuppressFinalize(true);
        }
    }
}
