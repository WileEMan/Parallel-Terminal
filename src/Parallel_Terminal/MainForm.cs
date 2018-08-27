// TODO: currently the slave accepts any authentication.  I'm not even sure it does that properly.  Needs to be checked before being a secure application.  But,
// even besides that, once it accepts the authentication, it launches the command prompt process under the service's account.  If you authenticate as someone
// with wimpy priviledges, then you gain access to the service's account, be it an administrator or anything else.  Totally insecure setup.  Keep away from
// non-admins until fixed.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Core_Library;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace Parallel_Terminal
{
    public partial class MainForm : Form, ILogger
    {
        /// <summary>
        /// Maybe this should just be a property that grabs all the tags in the ComputerTree nodes.
        /// </summary>
        List<Slave> Slaves = new List<Slave>();

        Stopwatch SinceStart = Stopwatch.StartNew();
        Stopwatch SinceCredentials = new Stopwatch();

        SlaveCore LocalHostSlave;
        object ConsoleLock = new object();
        void ILogger.WriteLine(string Message, Severity Severity)
        {
            lock (ConsoleLock)
            {
                string FullMsg;
                switch (Severity)
                {
                    case Severity.Debug: FullMsg = Message; break;
                    case Severity.Information: FullMsg = Message; break;
                    case Severity.Warning: FullMsg = "[WARNING] " + Message; break;
                    default:
                    case Severity.Error: FullMsg = "[ERROR] " + Message; break;
                }
                System.Diagnostics.Debug.WriteLine(FullMsg);
            }
        }

        TreeNode HostNodes = new TreeNode("Computers");

        DateTime AutomaticConnectionRequestsAt = DateTime.MinValue;

        public MainForm()
        {
            InitializeComponent();
            Terminal.Enabled = false;       // Disable keyboard input until ready.
            Legend.Source = Terminal;

            AcceptedCertificates = new RegistryCertificateStore(Registry.CurrentUser, AccCertRegSubKey);            
        }

        NetworkCredential UserCredentials;

        #if DEBUG
        bool StartupDebugging = true;
        #endif

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(AppRegSubKey);
                Left = (int)key.GetValue("MainForm_Left", Left);
                Top = (int)key.GetValue("MainForm_Top", Top);
                Width = (int)key.GetValue("MainForm_Width", Width);
                Height = (int)key.GetValue("MainForm_Height", Height);
                MainSplitContainer.SplitterDistance = (int)key.GetValue("MainForm_SplitterDistance", MainSplitContainer.SplitterDistance);
                Terminal.TextFontSizeInPoints = (int)key.GetValue("Terminal_TextFontSize", (int)Terminal.TextFontSizeInPoints);
            }
            catch (Exception)
            {
                MainSplitContainer_Resize(null, null);
            }

            try
            {
                Show();

                CredentialsDialog cd = new CredentialsDialog(this);
                cd.Caption = "Please provide the account to use upon connecting.";
                cd.Message = "Enter credentials.";
                if (cd.ShowDialog() != DialogResult.OK)
                {
                    Close();
                    return;
                }
                UserCredentials = cd.Credentials;
                SinceCredentials.Start();

                lock (Slaves)
                {
                    lock (ComputerTree)
                    {
                        ComputerTree.Nodes.Add(HostNodes);

                        // Add a LocalHost slave running in this process.  Looks just like the service, but runs whenever parallel terminal is running- just in case
                        // the user wants to run something to include localhost.
                        LocalHostSlave = new SlaveCore(this, true);

                        // Add all slaves and only start connecting them after they've all been added.  This ensures proper semantics in the TerminalControl.
                        Slaves.Add(new Slave("localhost", AcceptedCertificates));

                        // Find all other hosts that we've previously opened and use                    
                        RegistryKey key = Registry.CurrentUser.OpenSubKey(HostRegSubKey, false);
                        if (key != null)
                        {
                            foreach (var v in key.GetSubKeyNames())
                            {
                                Slave NewSlave = new Slave(v, AcceptedCertificates);
                                Slaves.Add(NewSlave);
                            }
                        }

                        foreach (Slave ss in Slaves)
                        {
                            ss.Connection.OnMessage += Slave_Connection_OnMessage;
                            ss.Connection.OnConnectionState += Slave_Connection_OnState;

                            Terminal.AddSlave(ss);
                            TreeNode SlaveNode = new TreeNode(ss.HostName + " (Disconnected)");
                            SlaveNode.Tag = ss;
                            SlaveNode.Checked = true;
                            HostNodes.Nodes.Add(SlaveNode);
                        }

                        GUITimer.Enabled = true;

                        // Can now start connecting them with a high accomodation for failures (since this wasn't a user-initiated connect).
                        // The exceptions will be received in the GUITimer, however, so we will check the connection time marker to know
                        // if it was an automatically initiated attempt.
                        foreach (Slave ss in Slaves)
                        {
                            try
                            {                                
                                ss.Connect(true, UserCredentials);
                            }
                            catch (Exception) { ss.Disconnect(); }
                        }                        

                        AutomaticConnectionRequestsAt = DateTime.Now;

                        ComputerTree.ExpandAll();

                        Legend.Invalidate();

                        Terminal.Focus();
                        Terminal.Select();                        
                    }
                }

                ComputerTree_AfterCheck(null, null);                

                Application.DoEvents();         // Clear out any keyboard events.
                Terminal.Enabled = true;        // Enable keyboard input after dialog box.
            }
            catch (Exception ex) {
                MessageBox.Show(ex.ToString());
                Close();
            }            
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Slaves != null)
            {
                foreach (Slave ss in Slaves)
                {
                    ss.Dispose();
                }
                Slaves.Clear();
                Slaves = null;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (LocalHostSlave != null)
            {
                LocalHostSlave.Stop();
                LocalHostSlave = null;
            }

            SaveSettingsToRegistry();
        }

        const string AppRegSubKey = "Software\\Wiley Black's Software\\Parallel Terminal";
        const string HostRegSubKey = AppRegSubKey + "\\Hosts";
        const string AccCertRegSubKey = AppRegSubKey + "\\Accepted_Certificates";

        RegistryCertificateStore AcceptedCertificates;

        void SaveHostListToRegistry()
        {
            lock (Slaves)
            {
                lock (ComputerTree)
                {
                    // Clear any existing registry entries that are no longer present.
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(HostRegSubKey, true))
                    {
                        if (key != null)
                        {
                            string[] subkeys = key.GetSubKeyNames();
                            foreach (string subkey in subkeys)
                            {
                                bool StillPresent = false;
                                foreach (TreeNode tn in HostNodes.Nodes)
                                {
                                    Slave ThatSlave = (Slave)tn.Tag;
                                    if (subkey == ThatSlave.HostName) { StillPresent = true; break; }
                                }
                                if (!StillPresent)
                                    key.DeleteSubKey(subkey);
                            }
                        }
                    }

                    // Create the listing.
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(HostRegSubKey))
                    {
                        foreach (TreeNode tn in HostNodes.Nodes)
                        {
                            Slave ThatSlave = (Slave)tn.Tag;
                            if (ThatSlave.HostName == "localhost") continue;

                            key.CreateSubKey(ThatSlave.HostName);
                        }
                    }
                }
            }
        }

        void SaveSettingsToRegistry()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(AppRegSubKey);
            key.SetValue("MainForm_Left", Left);
            key.SetValue("MainForm_Top", Top);
            key.SetValue("MainForm_Width", Width);
            key.SetValue("MainForm_Height", Height);
            key.SetValue("MainForm_SplitterDistance", MainSplitContainer.SplitterDistance);
            key.SetValue("Terminal_TextFontSize", (int)Terminal.TextFontSizeInPoints);
        }

        List<TreeNode> GetAllSlaveNodes()
        {
            List<TreeNode> ret = new List<TreeNode>();
            foreach (TreeNode tn in HostNodes.Nodes) ret.Add(tn);
            return ret;
        }

        TreeNode FindSlaveNode(TreeNodeCollection FromNodes, Slave ForSlave)
        {
            foreach (TreeNode tn in FromNodes)
            {
                if (tn.Tag == ForSlave) return tn;
            }
            return null;
        }

        Slave FindSlave(Slave_Connection With_Connection)
        {
            foreach (Slave ss in Slaves)
            {
                if (ss.Connection == With_Connection) return ss;
            }
            return null;
        }

        object StateQueueLock = new object();
        Queue<Tuple<Slave_Connection, Slave_Connection.Connection_State>> StateChanges = new Queue<Tuple<Slave_Connection, Slave_Connection.Connection_State>>();

        private void Slave_Connection_OnState(Slave_Connection SC, Slave_Connection.Connection_State NewState)
        {                        
            lock (StateQueueLock) StateChanges.Enqueue(new Tuple<Slave_Connection, Slave_Connection.Connection_State>(SC, NewState));            
        }

        #if DEBUG
        Queue<Tuple<Slave_Connection, System.Xml.Linq.XElement>> DebugQueue = new Queue<Tuple<Slave_Connection, System.Xml.Linq.XElement>>();
        void FinishDebugTests()
        {
            lock (this)
            {
                while (DebugQueue.Count > 0)
                {
                    var entry = DebugQueue.Dequeue();
                    Slave_Connection_OnMessage(entry.Item1, entry.Item2);
                }
            }
        }
        #endif

        void Slave_Connection_OnMessage(Slave_Connection SC, System.Xml.Linq.XElement xMsg)
        {
            // This event is called from an independent thread belonging to the particular slave.

            #if DEBUG
            // In the startup debugging phase, intercept anything and queue it for later.
            if (StartupDebugging) { lock (this) { DebugQueue.Enqueue(new Tuple<Slave_Connection, System.Xml.Linq.XElement>(SC, xMsg)); } return; }
            #endif

            Slave found = null;
            lock (Slaves)
            {
                foreach (Slave ss in Slaves)
                {
                    if (ss.Connection == SC)
                    {
                        found = ss;
                        break;
                    }
                }
            }

            if (found != null)
            //lock (found)
            {
                Terminal.OnMessage(found, xMsg);
                return;
            }

            throw new Exception("Slave not found in current connections list.");
        }

        private void MainSplitContainer_Resize(object sender, EventArgs e)
        {
            ComputerTree.Width = MainSplitContainer.Panel1.ClientSize.Width;
            ComputerTree.Height = MainSplitContainer.Panel1.ClientSize.Height;
            Terminal.Width = MainSplitContainer.Panel2.ClientSize.Width;
            Terminal.Height = MainSplitContainer.Panel2.ClientSize.Height - Legend.Height - 5;
            Legend.Width = MainSplitContainer.Panel2.ClientSize.Width;
            Legend.Top = Terminal.Bottom + 5;
            Terminal.Invalidate();
        }

        private void MainSplitContainer_SplitterMoved(object sender, SplitterEventArgs e)
        {
            MainSplitContainer_Resize(null, null);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            MainSplitContainer.Width = ClientSize.Width;
            MainSplitContainer.Height = ClientSize.Height;
        }

        #if DEBUG
        bool PendingSelfTest = true;
        #endif

        private void GUITimer_Tick(object sender, EventArgs e)
        {
            /** Process startup diagnostics when appropriate **/

            #if DEBUG
            if (PendingSelfTest && SinceCredentials.ElapsedMilliseconds > 3000)
            {
                Terminal.SelfTest();
                PendingSelfTest = false;
            }
            else if (StartupDebugging && SinceCredentials.ElapsedMilliseconds > 3200)
            {
                StartupDebugging = false;
                FinishDebugTests();
            }
            #endif

            /** Process any exceptions that happened on slave worker threads **/

            foreach (Slave ss in Slaves)
            {
                Exception LastError = ss.Connection.GetLastError();
                if (LastError != null)
                {
                    if (LastError is Slave_Connection.ConnectionException)
                    {
                        MessageBox.Show(LastError.Message);
                    }
                    else
                    {
                        MessageBox.Show("Error processing connection to " + ss.HostName + ": " + LastError.ToString());
                    }
                    ss.Disconnect();
                }
            }

            /** Process any queued state changes **/

            lock (StateQueueLock)
            {
                while (StateChanges.Count > 0)
                {
                    var SCI = StateChanges.Dequeue();
                    Slave CurrentSlave = FindSlave(SCI.Item1);
                    if (CurrentSlave == null) throw new ArgumentException("Connection state changed for host not found in listing.");

                    lock (ComputerTree)
                    {
                        foreach (TreeNode CurrentNode in GetAllSlaveNodes())
                        {
                            if (CurrentNode.Tag != CurrentSlave) continue;

                            switch (SCI.Item2)
                            {
                                case Slave_Connection.Connection_State.Connecting:
                                    {
                                        CurrentNode.Text = CurrentSlave.HostName + " (Connecting)";
                                        break;
                                    }

                                case Slave_Connection.Connection_State.EncryptionEstablished:
                                    {
                                        CurrentNode.Text = CurrentSlave.HostName + " (Connecting Terminal)";
                                        break;
                                    }

                                case Slave_Connection.Connection_State.Connected:
                                    {
                                        CurrentNode.Text = CurrentSlave.HostName;
                                        break;
                                    }

                                case Slave_Connection.Connection_State.Disconnected:
                                    {
                                        CurrentNode.Text = CurrentSlave.HostName + " (Disconnected)";
                                        break;
                                    }

                                default: throw new NotSupportedException();
                            }
                        }
                    }
                }
            }
        }        

        private void ComputerTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            //e.Node.Checked = !e.Node.Checked;            
        }

        private void ComputerTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            lock (ComputerTree)
            {
                List<Slave> Selected = new List<Slave>();
                foreach (TreeNode Node in HostNodes.Nodes)
                {
                    if (Node.Checked) Selected.Add((Slave)Node.Tag);                    
                }

                Terminal.SetCurrentTextDisplay(Selected);
            }
        }

        private void Terminal_Click(object sender, EventArgs e)
        {
            if (!Terminal.Focused)
            {
                Terminal.Select();
                Terminal.Invalidate();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void addHostStripMenuItem_Click(object sender, EventArgs e)
        {
            AddHostForm ahf = new AddHostForm();
            if (ahf.ShowDialog() != DialogResult.OK) return;

            Slave NewSlave = new Slave(ahf.Hostname, AcceptedCertificates);
            lock (Slaves)
            {
                NewSlave.Connection.OnMessage += Slave_Connection_OnMessage;
                NewSlave.Connection.OnConnectionState += Slave_Connection_OnState;
                Slaves.Add(NewSlave);

                lock (ComputerTree)
                {
                    Terminal.AddSlave(NewSlave);
                    TreeNode SlaveNode = new TreeNode(NewSlave.HostName + " (Disconnected)");
                    SlaveNode.Tag = NewSlave;
                    HostNodes.Nodes.Add(SlaveNode);
                    Legend.Invalidate();
                }
            }

            SaveHostListToRegistry();
        }

        TreeNode ContextNode = null;

        private void OnRemoveNode_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;
            Slave ss = ContextNode.Tag as Slave;
            if (ss == null) return;
            if (MessageBox.Show("Remove the host '" + ss.HostName + "'?", "Please confirm", MessageBoxButtons.OKCancel) != DialogResult.OK) return;

            lock (Slaves)
            {
                lock (ComputerTree)
                {
                    Slaves.Remove(ss);
                    Terminal.RemoveSlave(ss);
                    HostNodes.Nodes.Remove(ContextNode);
                    Legend.Invalidate();
                }
            }

            SaveHostListToRegistry();
        }

        private void OnConnectNode_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;
            Slave ss = ContextNode.Tag as Slave;
            if (ss == null) return;
            try
            {
                ss.Connect(false, UserCredentials);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to connect: " + ex.ToString());
            }
        }

        private void OnDisconnectNode_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;
            Slave ss = ContextNode.Tag as Slave;
            if (ss == null) return;
            try
            {
                ss.Disconnect();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while disconnecting: " + ex.ToString());
            }
        }        

        private void ComputerTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            Point MousePos = e.Location;
            ContextNode = e.Node;
            Slave ContextSlave = (Slave)ContextNode.Tag;

            MenuItem miConnect = new MenuItem("&Connect", OnConnectNode_MenuItemClick);
            MenuItem miDisconnect = new MenuItem("&Disconnect", OnDisconnectNode_MenuItemClick);
            MenuItem miSep = new MenuItem("-");
            MenuItem miRemove = new MenuItem("&Remove Host", OnRemoveNode_MenuItemClick);
            miConnect.Enabled = ContextSlave.IsDisconnected;
            miDisconnect.Enabled = !ContextSlave.IsDisconnected;

            MenuItem[] menuItems = new MenuItem[]{
                miConnect,
                miDisconnect,
                miSep,
                miRemove
            };

            ContextMenu buttonMenu = new ContextMenu(menuItems);
            buttonMenu.Show(ComputerTree, MousePos);
        }

        private void reloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lock (Slaves)
            {
                foreach (Slave ss in Slaves)
                {
                    if (ss.IsConnected) ss.Connection.SendReloadConsoleRequest();
                }
            }
        }
    }
}
