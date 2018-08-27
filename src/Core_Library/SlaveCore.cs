//#define ShowCmdWindow 

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
using System.Xml.Linq;
using System.IO.Pipes;
using wb;
using System.Runtime.InteropServices;
using System.Security;

namespace Core_Library
{
    public enum Severity
    {
        Debug,
        Information,
        Warning,
        Error
    }

    public interface ILogger
    {
        void WriteLine(string Message, Severity Severity);
    }

    /// <summary>
    /// UPDATE: Now supports FileStreams provided by the ProcessLauncher().
    /// 
    /// There seems to be no way to call the StandardError and StandardOutput streams that is non-blocking but character level except this.  The call to StreamReader.ReadLine()
    /// would block indefinitely.  The asynchronous methods are available, but are line-buffered.  I'm not sure if this is also line-buffered, but it's the only possible solution
    /// I see using the C# API.
    /// </summary>
    public class PipeReader : IDisposable
    {
        //AnonymousPipeClientStream Underlying;
        ReadPipe Underlying;
        Thread Worker;
        bool Stopping = false;

        //public PipeReader(AnonymousPipeClientStream ToRead, string Label)
        public PipeReader(ReadPipe ToRead, string Label)
        {
            this.Underlying = ToRead;

            Worker = new Thread(ReaderThread);
            Worker.Name = "StreamReaderThread_" + Label;
            Worker.Start();
        }

        object OSThreadIdLock = new object();
        uint OSThreadId;

        public void Dispose()
        {
            if (!Stopping)
            {
                #if false
                // So much work went into figuring out how to abort the Read() operation...
                uint TheOSThreadId;
                lock (OSThreadIdLock)
                {
                    TheOSThreadId = OSThreadId;
                }
                CancelSynchronousIoByThreadId(TheOSThreadId);
                #endif
                Underlying.Close();
                Stopping = true;
                if (Worker != null) Worker.Join();
            }
            GC.SuppressFinalize(this);
        }

        StringBuilder Received = new StringBuilder();

        object PendingExceptionLock = new object();
        Exception PendingException = null;

        [DllImport("kernel32.dll")]
        static extern bool SetNamedPipeHandleState(Microsoft.Win32.SafeHandles.SafePipeHandle hNamedPipe,            
            ref uint lpMode,
            IntPtr lpMaxCollectionCount,
            IntPtr lpCollectDataTimeout);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CancelIo(Microsoft.Win32.SafeHandles.SafePipeHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenThread(uint desiredAccess, bool inheritHandle, uint threadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CancelSynchronousIo(IntPtr hThread);

        static bool CancelSynchronousIoByThreadId(uint threadId)
        {
            // GENERIC_WRITE, Non-inheritable
            var threadHandle = OpenThread(0x40000000, false, (uint)threadId);
            var ret = CancelSynchronousIo(threadHandle);
            CloseHandle(threadHandle);
            return ret;
        }

        Encoding ConsoleEncoding = Encoding.GetEncoding(437);

        void ReaderThread()
        {
            Thread.BeginThreadAffinity();

            try
            {
                byte[] buffer = new byte[65536];
                //int BufferUsed = 0;

                lock (OSThreadIdLock) OSThreadId = GetCurrentThreadId();

                while (!Stopping)
                {
#if false
                    int BytesRead = Underlying.Read(buffer, BufferUsed, buffer.Length - BufferUsed);                    
                    while (BytesRead == 4096)
                    {
                        BufferUsed += BytesRead;
                        Array.Resize(ref buffer, buffer.Length + 65536);
                        continue;
                    }
                    BufferUsed += BytesRead;
                    if (BufferUsed > 0)
                    {
                        lock (Received)
                        {
                            Received.Append(Encoding.ASCII.GetString(buffer, 0, BufferUsed));
                        }
                        BufferUsed = 0;
                    }
                    else Thread.Sleep(10);
#else
#if false
                    int BytesRead = Underlying.Read(buffer, 0, 1);
                    if (BytesRead == 1)
                    {
                        lock (Received) Received.Append(ConsoleEncoding.GetString(buffer, 0, 1));
                    }
#else
                    // This is probably not the most efficient approach.  We'd be better off with a wait handle, because the process may get delayed waiting for us to read from the pipe.
                    // But, I'll take whatever works.
                    int BytesRead = Underlying.NonBlockingRead(buffer, buffer.Length);
                    if (BytesRead > 0)
                    {
                        lock (Received)
                        {
                            Received.Append(ConsoleEncoding.GetString(buffer, 0, BytesRead));
                        }
                    }
                    else Thread.Sleep(1);
                    #endif
#endif
                }
            }
            catch (Exception ex)
            {
                lock (PendingExceptionLock)
                {
                    if (PendingException == null)
                        PendingException = ex;
                }
            }
            finally
            {
                Thread.EndThreadAffinity();
            }
        }

        public string GetNewText()
        {
            lock (PendingExceptionLock)
            {
                if (PendingException != null)
                {
                    Exception ex = PendingException;
                    PendingException = null;
                    throw ex;
                }
            }

            string ret;
            lock (Received)
            {
                ret = Received.ToString();
                Received.Clear();
            }
            return ret;
        }
    }

    public class SlaveProcess : IDisposable
    {
        public ProcessLauncher CommandPrompt;
        //public TrimProcessLauncher CommandPrompt;
        public PipeReader StandardOutput;
        public PipeReader StandardError;

        public SlaveProcess()
        {
            CommandPrompt = new ProcessLauncher();
        }

        public void Start(string Domain, string UserName, string Password, bool CreateWindow)
        {
            // Create the worker shell...
#if true
            
            CommandPrompt.StartInfo.UseShellExecute = false;        // Required to enable RedirectStandard...
            CommandPrompt.StartInfo.CreateNoWindow = !CreateWindow;
            CommandPrompt.StartInfo.FileName = "cmd.exe";
            CommandPrompt.StartInfo.Arguments = "";
            //CommandPrompt.StartInfo.RedirectStandardError = true;
            //CommandPrompt.StartInfo.RedirectStandardInput = true;            
            //CommandPrompt.StartInfo.RedirectStandardOutput = true;
            CommandPrompt.StartInfo.RedirectStandardInput = false;
            CommandPrompt.StartInfo.RedirectStandardOutput = false;
            CommandPrompt.StartInfo.RedirectStandardError = false;

            CommandPrompt.StartInfo.Domain = Domain;
            CommandPrompt.StartInfo.UserName = UserName;
            CommandPrompt.StartInfo.PasswordInClearText = Password;
            
            CommandPrompt.StartInfo.LoadUserProfile = true;         // Instructs the launcher to load the user's profile into HKEY_CURRENT_USER.
#else
            CommandPrompt = new TrimProcessLauncher();
#endif

            CommandPrompt.Start();
            //StandardOutput = new PipeReader(CommandPrompt.StandardOutput, "StdOut");
            //StandardError = new PipeReader(CommandPrompt.StandardError, "StdErr");
        }

        public void Dispose()
        {            
            if (CommandPrompt != null)
            {
                if (StandardOutput != null) { StandardOutput.Dispose(); StandardOutput = null; }
                if (StandardError != null) { StandardError.Dispose(); StandardError = null; }
                CommandPrompt.Kill();
                CommandPrompt = null;
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// SlaveCore implements the code that runs on the slave computer.  It is used by both the Slave_Tester app (a diagnostic tool) and the Slave_Terminal service and in both cases provides a slave
    /// for the master to talk to.  Also the parallel terminal will have one SlaveCore in it with HostName of "localhost".
    /// </summary>
    public class SlaveCore : CommonCore, IDisposable
    {        
        ILogger Log;
        Thread ClientThread;
        bool Stopping = false;
        bool IsLocalProvider;

        object ProcessLock = new object();
        Dictionary<string, SlaveProcess> Processes = new Dictionary<string, SlaveProcess>();        
        SlaveProcess ConnectedProcess = null;

        public SlaveCore(ILogger Log, bool IsLocalProvider)
        {
            this.Log = Log;
            this.IsLocalProvider = IsLocalProvider;

            ClientThread = new Thread(ClientThreadEntry);
            ClientThread.Name = "SlaveCore Client Worker";
            ClientThread.Start();

            Log.WriteLine("Client service started.", Severity.Information);
        }

        protected SslStream NetworkStream;

        protected void ClientThreadEntry()
        {
            try
            {
                TcpListener listenerV4 = new TcpListener(IPAddress.Any, PortNumber);
                TcpListener listenerV6 = new TcpListener(IPAddress.IPv6Any, PortNumber);
                listenerV4.Start(10);
                listenerV6.Start(10);

                // Start listening for connections.  
                
                Log.WriteLine("Listening for TCP connections on port " + PortNumber + "...\n", Severity.Debug);
                while (!Stopping)
                {
                    TcpClient ClientRequest;
                    try
                    {                        
                        if (listenerV4.Pending())
                            ClientRequest = listenerV4.AcceptTcpClient();
                        else if (listenerV6.Pending())
                            ClientRequest = listenerV6.AcceptTcpClient();
                        else { Thread.Sleep(250); continue; }

                        Log.WriteLine("TCP Client accepted.\n", Severity.Debug);

                        NetworkStream = new SslStream(ClientRequest.GetStream(), false);
                        NetworkStream.AuthenticateAsServer(GetRemoteDesktopCertificate(), false, SslProtocols.Tls12, true);
                        if (!NetworkStream.IsAuthenticated)
                        {
                            NetworkStream = null;
                            Log.WriteLine("Unable to authenticate incoming connection from " + ((IPEndPoint)ClientRequest.Client.RemoteEndPoint).Address.ToString() + ".", Severity.Warning);
                            ClientRequest.Close();
                            continue;
                        }
                        if (!NetworkStream.IsEncrypted)
                        {
                            NetworkStream = null;
                            Log.WriteLine("Unable to encrypt incoming connection from " + ((IPEndPoint)ClientRequest.Client.RemoteEndPoint).Address.ToString() + ".", Severity.Warning);
                            ClientRequest.Close();
                            continue;
                        }
                        // Display the properties and settings for the authenticated stream.
                        Log.WriteLine("Authentication successful [host].\n", Severity.Debug);
#if DEBUG
                        DisplaySecurityLevel(Log, NetworkStream, Severity.Debug);
                        DisplaySecurityServices(Log, NetworkStream, Severity.Debug);
                        DisplayCertificateInformation(Log, NetworkStream, Severity.Debug);
                        DisplayStreamProperties(Log, NetworkStream, Severity.Debug);
#endif

                        // Transmit our version info as a hello.
                        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                        string version = fvi.FileVersion;
                        string XmlMsg = "<Portal-Version Core-Library=\"" + fvi.FileVersion + "\" />";
                        byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                        if (NetworkStream != null) SendMessage(NetworkStream, RawMsg);
                    }
                    catch (Exception ex)
                    {
                        // Note: "The credentials supplied to the package were not recognized" is probably a sign that the code doesn't have Administrator access and can't access the subsystem or certificate it needs.
                        Log.WriteLine("Exception accepting new connection: " + ex.ToString(), Severity.Debug);
                        continue;
                    }

                    try
                    {
                        ServiceConnection(NetworkStream, ClientRequest, ClientRequest.GetStream());
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine("Exception servicing connection: " + ex.ToString(), Severity.Debug);
                    }

                    lock (ProcessLock)
                    {
                        // Setting ConnectedProcess to null before accepting a new connection is essential to security.  It prevents access to the console without a new authentication and Connect-Terminal.                    
                        ConnectedProcess = null;

                        // If we aren't attached to a console, FreeConsole() would return ERROR_INVALID_PARAMETER.
                        ConsoleApi.FreeConsole();

                        Log.WriteLine("Closing connection in SlaveCore (if it isn't already).", Severity.Debug);
                        if (ClientRequest != null) { ClientRequest.Dispose(); ClientRequest = null; }

                        if (Conin != null) { Conin.Dispose(); Conin = null; }
                        if (CT != null) { CT.Dispose(); CT = null; }
                    }
                }

                lock (ProcessLock)
                {
                    ConnectedProcess = null;
                    foreach (var kvp in Processes)
                    {
                        kvp.Value.Dispose();
                    }
                    Processes.Clear();
                }
            }
            catch (Exception e)
            {
                Log.WriteLine("Error: " + e.ToString(), Severity.Error);
            }
        }

        string LastCurrentLine = "";

        void ServiceConnection(SslStream Stream, TcpClient Client, NetworkStream BaseStream)
        {
            char[] buffer = new char[4096];
            //StringBuilder sb = new StringBuilder();

            while (!Stopping && IsStillConnected(Client) && BaseStream != null)
            {
                // PollConnection() checks and assembles incoming messages from the Stream (in this case, receiving from the master PC) and 
                // calls OnMessage() when one is completed.
                if (BaseStream.DataAvailable)
                {
                    List<byte[]> Messages = PollConnection(NetworkStream);
                    foreach (byte[] Message in Messages) OnMessageReceived(Message);
                }

                Thread.Sleep(50);

                // If a process is connected, service it now.
                lock (ProcessLock)
                {
                    if (ConnectedProcess != null)
                    {
                        if (CT != null)
                        {
                            // There is a 64KB message size limit imposed in SendMessage().  Unicode characters I think are 16-bits, but
                            // I think certain characters can be up to 4-bytes?  Well, maybe not.  Either way, there's not much cost here
                            // so I'll assume 4-bytes per character.  Base-64 expands the size of the data by 4/3rds (plus a handful).
                            // So if we max at 12000, that allows 48000 4-byte characters that expand to 64000, and our actual message
                            // limit is at 65536 allowing some margin plus the wrapping.
                            const int MaxSize = 12000;

                            // TODO: Should also add a spot check mechanism to ReadNew() that checks one or two random lines from earlier in the console.
                            // If they don't match, return stale and regenerate the whole thing.

                            bool Stale = false;
                            string NewText = CT.ReadNew(out Stale, true, MaxSize);
                            if (Stale)
                            {
                                NewText = CT.ReadWholeConsole(MaxSize);                                
                                string EncodedText = System.Convert.ToBase64String(Encoding.Unicode.GetBytes(NewText));
                                string XmlMsg = "<Whole-Console>" + EncodedText + "</Whole-Console>";
                                byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                                SendMessage(Stream, RawMsg);
                            }
                            else if (NewText.Length > 0 && NewText.Contains("\n"))
                            {                                
                                string EncodedText = System.Convert.ToBase64String(Encoding.Unicode.GetBytes(NewText));
                                //string XmlMsg = "<Console-New Last-X=\"" + CT.LastCursorPosition.X + "\" Last-Y=\"" + CT.LastCursorPosition.Y + "\">" + EncodedText + "</Console-New>";                                
                                string XmlMsg = "<Console-New>" + EncodedText + "</Console-New>";
                                byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);                                
                                SendMessage(Stream, RawMsg);
                            }
                            else
                            {
                                string CurrentLine = CT.PeekCurrentLine();
                                if (LastCurrentLine != CurrentLine)
                                {
                                    LastCurrentLine = CurrentLine;
                                    string EncodedText = System.Convert.ToBase64String(Encoding.Unicode.GetBytes(CurrentLine));
                                    string XmlMsg = "<Current-Console-Line>" + EncodedText + "</Current-Console-Line>";
                                    byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                                    SendMessage(Stream, RawMsg);
                                }
                            }
                        }
                    }
                }
            }
        }        

        public static String SlashEscapeASCII(byte[] data)
        {
            StringBuilder cbuf = new StringBuilder();
            foreach (byte b in data)
            {
                if (b >= 0x20 && b <= 0x7e)
                {
                    cbuf.Append((char)b);
                }
                else
                {
                    cbuf.Append(String.Format("\\0x{0:X}", b));
                }
            }
            return cbuf.ToString();
        }

        public static String SlashEscapeUnicode(string data)
        {
            StringBuilder cbuf = new StringBuilder();
            foreach (char b in data)
            {
                if (b >= 0x20 && b <= 0x7e)
                {
                    cbuf.Append((char)b);
                }
                else
                {
                    cbuf.Append(String.Format("\\0x{0:X}", b));
                }
            }
            return cbuf.ToString();
        }

        ConsoleTracker CT;

        void OnMessageReceived(byte[] RawMessage)
        {
            try
            {
                string Message = Encoding.Unicode.GetString(RawMessage);
                XElement xMsg = XElement.Parse(Message, LoadOptions.PreserveWhitespace);

                if (xMsg.Name.LocalName == "Connect-Terminal")
                {
                    try
                    {
                        string Domain = xMsg.Attribute("Domain").Value;
                        string UserName = xMsg.Attribute("UserName").Value;
                        string Password = xMsg.Attribute("Password").Value;

                        lock (ProcessLock)
                        {
                            // Remove any processes which have exited so that instead of reattaching we launch a new one...
                            List<string> ToRemove = new List<string>();                            
                            foreach (var kvp in Processes)
                            {
                                var TheProcess = kvp.Value;
                                if (TheProcess.CommandPrompt == null || !TheProcess.CommandPrompt.IsStillRunning) ToRemove.Add(kvp.Key);                                    
                            }
                            foreach (string Key in ToRemove) Processes.Remove(Key);

                            // Next check if we have this process already open, referenced by full username...
                            string FullUserName;
                            if (Domain.Length > 0)
                                FullUserName = Domain + "\\" + UserName;
                            else
                                FullUserName = UserName;                            
                            if (Processes.ContainsKey(FullUserName))
                            {
                                CredentialValidation.ValidateUser(Domain, UserName, Password);
                                ConnectedProcess = Processes[FullUserName];

                                if (!ConsoleApi.AttachConsole((uint)ConnectedProcess.CommandPrompt.ProcessId))
                                    throw new Win32Exception(Marshal.GetLastWin32Error());

                                if (Conin != null) { Conin.Dispose(); Conin = null; }
                                Conin = new ConsoleInput();

                                if (CT != null) { CT.Dispose(); CT = null; }
                                CT = new ConsoleTracker();

                                string XmlMsg = "<Terminal-Connected Reconnected=\"true\" full-user-name=\"" + FullUserName + "\" />";
                                byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                                SendMessage(NetworkStream, RawMsg);
                            }
                            else
                            {
                                ConnectedProcess = new SlaveProcess();
                                try
                                {
#if DEBUG && ShowCmdWindow
                                    ConnectedProcess.Start(Domain, UserName, Password, IsLocalProvider);
#else
                                    ConnectedProcess.Start(Domain, UserName, Password, false);
#endif

                                    Processes.Add(FullUserName, ConnectedProcess);

                                    Thread.Sleep(2500);         // TODO: WaitForInputIdle() would be preferred and more robust.

                                    //ConnectedProcess.CommandPrompt.WaitForInputIdle(15000);                                    

                                    if (!ConnectedProcess.CommandPrompt.IsStillRunning)
                                        throw new Exception("Newly launched terminal appears to have exited immediately or never actually launched.");

                                    if (!ConsoleApi.AttachConsole((uint)ConnectedProcess.CommandPrompt.ProcessId))
                                    {
                                        Debug.WriteLine("AttachConsole() failed.");
                                        throw new Win32Exception(Marshal.GetLastWin32Error());
                                    }

                                    Debug.WriteLine("Console attached.");

                                    if (Conin != null) { Conin.Dispose(); Conin = null; }
                                    Conin = new ConsoleInput();

                                    if (CT != null) { CT.Dispose(); CT = null; }
                                    CT = new ConsoleTracker();

                                    Debug.WriteLine("Console established without errors.");
                                }
                                finally
                                {
                                    if (ConnectedProcess.CommandPrompt.DebugLog.Length > 0)
                                    {
                                        string XmlMsg = "<Debug>" + ConnectedProcess.CommandPrompt.DebugLog.ToString() + "</Debug>";
                                        byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                                        SendMessage(NetworkStream, RawMsg);
                                        ConnectedProcess.CommandPrompt.DebugLog.Clear();
                                    }
                                }

                                {
                                    string XmlMsg = "<Terminal-Connected Reconnected=\"false\" full-user-name=\"" + FullUserName + "\" />";
                                    byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                                    SendMessage(NetworkStream, RawMsg);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        string EncodedText = System.Convert.ToBase64String(Encoding.Unicode.GetBytes(ex.ToString() + "\n"));
                        string XmlMsg = "<Error>" + EncodedText + "</Error>";
                        byte[] RawMsg = Encoding.Unicode.GetBytes(XmlMsg);
                        SendMessage(NetworkStream, RawMsg);
                    }
                }                

                if (xMsg.Name.LocalName == "Console-Input")
                {
                    // TODO: This xml format isn't very efficient.  Could at least string it all into one array of INPUT_RECORDs.
                    List<ConsoleApi.KEY_EVENT_RECORD> Keys = new List<ConsoleApi.KEY_EVENT_RECORD>();
                    foreach (var Key in xMsg.Elements())
                    {
                        if (Key.Name != "Key") throw new FormatException("Expected <Key> objects in <Console-Input>");
                        ConsoleApi.KEY_EVENT_RECORD KER = new ConsoleApi.KEY_EVENT_RECORD();
                        KER.bKeyDown = Boolean.Parse(Key.Attribute("down").Value);
                        KER.dwControlKeyState = (ConsoleApi.ControlKeyState)UInt32.Parse(Key.Attribute("ctrl").Value);
                        KER.UnicodeChar = (char)UInt32.Parse(Key.Attribute("char").Value);
                        KER.wRepeatCount = UInt16.Parse(Key.Attribute("repeat").Value);
                        KER.wVirtualKeyCode = UInt16.Parse(Key.Attribute("key").Value);
                        KER.wVirtualScanCode = UInt16.Parse(Key.Attribute("scan").Value);
                        Keys.Add(KER);
                    }

                    //byte[] Raw = System.Convert.FromBase64String(xMsg.Value);
                    //string Msg = Encoding.Unicode.GetString(Raw);
                    lock (ProcessLock)
                    {
                        if (ConnectedProcess != null)
                        {                            
                            StringBuilder Text = new StringBuilder();
                            foreach (var Key in Keys) Text.Append(Key.UnicodeChar);
                            //Debug.WriteLine("Writing to console as input: '" + Text + "'");                                                        

                            ConsoleApi.INPUT_RECORD[] irs = new ConsoleApi.INPUT_RECORD[1];
                            ConsoleApi.INPUT_RECORD ir = new ConsoleApi.INPUT_RECORD();
                            ir.EventType = (ushort)ConsoleApi.EventTypes.KEY_EVENT;
                            
                            foreach (var Key in Keys)
                            {                                
                                ir.KeyEvent = Key;
                                irs[0] = ir;
                                Conin.Write(irs);                                
                            }

                            //Thread.Sleep(50);
                            //CT.DebugWholeConsole();
                        }
                    }
                }

                if (xMsg.Name.LocalName == "Reload-Console")
                {
                    lock (ProcessLock) CT.PendingReload = true;
                }
            }
            catch (Exception ex)
            {
                Debug.Write("Exception detected in SlaveCore.OnMessageReceived(): " + ex.ToString());
                throw ex;
            }
        }

        wb.ConsoleInput Conin;

        public void Stop()
        {
            Stopping = true;
            ClientThread.Join();
            Log.WriteLine("Client service stopped.", Severity.Information);
        }
        
        void IDisposable.Dispose()
        {
            if (!Stopping)
            {
                Stop();
            }
            GC.SuppressFinalize(this);
        }
    }
}
