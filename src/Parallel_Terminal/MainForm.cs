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

        List<HostGroup> Groups = new List<HostGroup>();        

        Stopwatch SinceStart = Stopwatch.StartNew();
        //Stopwatch SinceCredentials = new Stopwatch();

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

        TreeNode HostNodes = new TreeNode("All Hosts");
        TreeNode GroupNodes = new TreeNode("Groups");        

        public MainForm()
        {
            InitializeComponent();
            Terminal.Enabled = false;       // Disable keyboard input until ready.
            Legend.Source = Terminal;

            AcceptedCertificates = new RegistryCertificateStore(Registry.CurrentUser, AccCertRegSubKey);            
        }

        NetworkCredential UserCredentials;

        #if DEBUG
        bool StartupDebugging = false;
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

                lock (Slaves)
                {
                    lock (ComputerTree)
                    {
                        ComputerTree.Nodes.Add(HostNodes);
                        ComputerTree.Nodes.Add(GroupNodes);

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
                            SlaveNode.Checked = false;
                            HostNodes.Nodes.Add(SlaveNode);
                        }

                        // Find all groups we've previously had and restore them
                        key = Registry.CurrentUser.OpenSubKey(GroupsRegSubKey, false);
                        if (key != null)
                        {
                            foreach (var v in key.GetSubKeyNames())
                            {
                                var group_key = key.OpenSubKey(v, false);

                                HostGroup Group = new HostGroup(v);
                                Groups.Add(Group);
                                TreeNode GNode = new TreeNode(group_key + " (Disconnected)");
                                GNode.Tag = Group;

                                object HostList = group_key.GetValue("Includes-Hosts");
                                if (HostList as string != null)
                                {
                                    string[] HostNames = ((string)HostList).Split(new char[] { ';' });
                                    foreach (string HostName in HostNames)
                                    {
                                        Slave Match = null;
                                        foreach (Slave ss in Slaves)
                                        {
                                            if (ss.HostName == HostName) { Match = ss; break; }
                                        }
                                        if (Match != null)
                                        {
                                            Group.Slaves.Add(Match);
                                            TreeNode HNode = new TreeNode(Match.HostName + " (Disconnected)");
                                            HNode.Tag = Match;
                                            HNode.Checked = false;
                                            GNode.Nodes.Add(HNode);
                                        }
                                    }
                                }
                                GNode.Text = Group.GetDisplayText();
                                GroupNodes.Nodes.Add(GNode);
                            }
                        }

                        GUITimer.Enabled = true;                                                

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
        const string GroupsRegSubKey = AppRegSubKey + "\\Groups";
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

        void SaveGroupListToRegistry()
        {
            lock (Slaves)
            {
                lock (ComputerTree)
                {
                    // Clear any existing registry entries that are no longer present.
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(GroupsRegSubKey, true))
                    {
                        if (key != null)
                        {
                            string[] subkeys = key.GetSubKeyNames();
                            foreach (string subkey in subkeys)
                            {
                                bool StillPresent = false;
                                foreach (TreeNode tn in GroupNodes.Nodes)
                                {
                                    HostGroup ThatGroup = (HostGroup)tn.Tag;
                                    if (subkey == ThatGroup.Name) { StillPresent = true; break; }
                                }
                                if (!StillPresent)
                                    key.DeleteSubKey(subkey);
                            }
                        }
                    }

                    // Create the listing.
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(GroupsRegSubKey))
                    {
                        foreach (TreeNode tn in GroupNodes.Nodes)
                        {
                            HostGroup ThatGroup = (HostGroup)tn.Tag;
                            RegistryKey group_key = key.CreateSubKey(ThatGroup.Name);
                            string hosts = "";
                            foreach (Slave ss in ThatGroup.Slaves) {
                                if (hosts.Length > 0) hosts += ";";
                                hosts += ss.HostName;
                            }
                            group_key.SetValue("Includes-Hosts", hosts);
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

        List<TreeNode> FindAllInstancesOfSlave(string HostName, TreeNode Within = null)
        {
            List<TreeNode> ret = new List<TreeNode>();
            if (Within == null)
            {
                foreach (TreeNode tn in ComputerTree.Nodes)
                    ret.AddRange(FindAllInstancesOfSlave(HostName, tn));
                return ret;
            }
            foreach (TreeNode tn in Within.Nodes)
            {
                if (tn.Tag is Slave && ((Slave)(tn.Tag)).HostName == HostName) ret.Add(tn);
                ret.AddRange(FindAllInstancesOfSlave(HostName, tn));
            }
            return ret;
        }

        List<TreeNode> FindAllInstancesOfGroup(string GroupName, TreeNode Within = null)
        {
            List<TreeNode> ret = new List<TreeNode>();
            if (Within == null)
            {
                foreach (TreeNode tn in ComputerTree.Nodes)
                    ret.AddRange(FindAllInstancesOfGroup(GroupName, tn));
                return ret;
            }
            foreach (TreeNode tn in Within.Nodes)
            {
                if (tn.Tag is HostGroup && ((HostGroup)(tn.Tag)).Name == GroupName) ret.Add(tn);
                ret.AddRange(FindAllInstancesOfGroup(GroupName, tn));
            }
            return ret;
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
        //bool PendingSelfTest = true;
        #endif

        private void GUITimer_Tick(object sender, EventArgs e)
        {
            /** Process startup diagnostics when appropriate **/

            #if DEBUG
            /*
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
            */
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
                        foreach (TreeNode CurrentNode in FindAllInstancesOfSlave(CurrentSlave.HostName))
                        {
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

                            TreeNode tnGroup = CurrentNode.Parent;
                            if (!(tnGroup.Tag is HostGroup)) continue;
                            tnGroup.Text = ((HostGroup)tnGroup.Tag).GetDisplayText();
                        }
                    }
                }
            }
        }        

        private void ComputerTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            //e.Node.Checked = !e.Node.Checked;            
        }

        bool ComputerTree_AfterCheck_Executing = false;
        private void ComputerTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (ComputerTree_AfterCheck_Executing) return;          // Prevent recursion.
            ComputerTree_AfterCheck_Executing = true;
            try
            {
                lock (ComputerTree)
                {
                    if (e != null)
                    {
                        if (e.Node.Tag is Slave)
                        {
                            bool NewState = e.Node.Checked;
                            List<TreeNode> Copies = FindAllInstancesOfSlave(((Slave)e.Node.Tag).HostName);
                            foreach (TreeNode tn in Copies) tn.Checked = NewState;
                        }
                        else if (e.Node.Tag is HostGroup)
                        {
                            bool NewState = e.Node.Checked;
                            foreach (Slave ss in ((HostGroup)e.Node.Tag).Slaves)
                            {
                                List<TreeNode> Copies = FindAllInstancesOfSlave(ss.HostName);
                                foreach (TreeNode tn in Copies) tn.Checked = NewState;
                            }
                        }
                    }

                    List<Slave> Selected = new List<Slave>();
                    foreach (TreeNode Node in HostNodes.Nodes)
                    {
                        if (Node.Checked && Node.Tag is Slave) Selected.Add((Slave)Node.Tag);
                    }

                    Terminal.SetCurrentTextDisplay(Selected);
                }
            }
            finally
            {
                ComputerTree_AfterCheck_Executing = false;
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

        /// <summary>
        /// LastAdditions is used only by instantiations of AddHostForm to remember the user's last selections
        /// and pre-select those same ones again in case they are repeating their previous add.
        /// </summary>
        List<HostGroup> LastAdditions;

        private void addHostStripMenuItem_Click(object sender, EventArgs e)
        {
            AddHostForm ahf = new AddHostForm(Groups, LastAdditions);
            if (ahf.ShowDialog() != DialogResult.OK) return;
            LastAdditions = ahf.Membership;

            Slave NewSlave = new Slave(ahf.HostName, AcceptedCertificates);
            lock (Slaves)
            {
                NewSlave.Connection.OnMessage += Slave_Connection_OnMessage;
                NewSlave.Connection.OnConnectionState += Slave_Connection_OnState;
                Slaves.Add(NewSlave);

                lock (ComputerTree)
                {
                    Terminal.AddSlave(NewSlave);
                    string Display = NewSlave.HostName + " (Disconnected)";
                    TreeNode SlaveNode = new TreeNode(Display);
                    SlaveNode.Tag = NewSlave;
                    HostNodes.Nodes.Add(SlaveNode);                    

                    foreach (HostGroup Group in LastAdditions)
                    {
                        Group.Slaves.Add(NewSlave);
                        List<TreeNode> Instances = FindAllInstancesOfGroup(Group.Name);
                        foreach (TreeNode tn in Instances)
                        {
                            TreeNode GNode = new TreeNode(Display);
                            GNode.Tag = NewSlave;
                            tn.Nodes.Add(GNode);
                            tn.Text = Group.GetDisplayText();
                        }
                    }

                    Legend.Invalidate();
                }
            }

            SaveHostListToRegistry();
            SaveGroupListToRegistry();
        }

        TreeNode ContextNode = null;

        private void OnEditMembership_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;
            Slave ss = ContextNode.Tag as Slave;
            if (ss == null) return;

            List<HostGroup> Memberships = new List<HostGroup>();
            lock (Slaves)
            {
                lock (ComputerTree)
                {
                    foreach (TreeNode v in GroupNodes.Nodes)
                    {
                        HostGroup Group = v.Tag as HostGroup;
                        if (Group.Slaves.Contains(ss)) Memberships.Add(Group);
                    }
                }
            }

            AddHostForm ahf = new AddHostForm(Groups, Memberships);
            ahf.HostName = ss.HostName;
            if (ahf.ShowDialog() != DialogResult.OK) return;
            Memberships = ahf.Membership;

            lock (Slaves)
            {
                lock (ComputerTree)
                {
                    List<TreeNode> ExistingInstances = FindAllInstancesOfSlave(ss.HostName);
                    foreach (TreeNode tnGroup in GroupNodes.Nodes)
                    {
                        HostGroup Group = (HostGroup)tnGroup.Tag;
                        if (Memberships.Contains(Group))
                        {
                            if (!Group.Slaves.Contains(ss))
                            {
                                // New membership added...                    
                                Group.Slaves.Add(ss);
                                TreeNode GNode = new TreeNode(ContextNode.Text);
                                GNode.Tag = ss;
                                tnGroup.Nodes.Add(GNode);
                                tnGroup.Text = Group.GetDisplayText();
                            }
                        }
                        else
                        {
                            if (Group.Slaves.Contains(ss))
                            {
                                // Existing membership revoked...
                                Group.Slaves.Remove(ss);
                                List<TreeNode> hits = new List<TreeNode>();
                                foreach (TreeNode tnHosts in tnGroup.Nodes)
                                {
                                    if (((Slave)tnHosts.Tag).HostName == ss.HostName) hits.Add(tnHosts);
                                }
                                foreach (TreeNode tn in hits) tnGroup.Nodes.Remove(tn);
                                tnGroup.Text = Group.GetDisplayText();
                            }
                        }
                    }

                    Legend.Invalidate();
                }
            }

            SaveHostListToRegistry();
            SaveGroupListToRegistry();
        }

        private void addGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddGroupForm agf = new AddGroupForm();
            for (;;)
            { 
                if (agf.ShowDialog() != DialogResult.OK) return;

                bool NameExists = false;
                lock (Slaves)
                {
                    lock (ComputerTree)
                    {                        
                        foreach (HostGroup Existing in Groups)
                        {
                            if (Existing.Name.ToLower() == agf.GroupName)
                            {
                                NameExists = true;
                                break;
                            }
                        }

                        if (!NameExists)
                        {
                            HostGroup NewGroup = new HostGroup(agf.GroupName);
                            Groups.Add(NewGroup);

                            TreeNode GroupNode = new TreeNode(NewGroup.GetDisplayText());
                            GroupNode.Tag = NewGroup;
                            GroupNodes.Nodes.Add(GroupNode);

                            Legend.Invalidate();
                        }
                    }
                }

                if (NameExists)
                {
                    if (MessageBox.Show("A group by that name already exists.") != DialogResult.OK) return;
                    continue;
                }

                break;
            }

            SaveHostListToRegistry();
            SaveGroupListToRegistry();
        }

        void RemoveHost(Slave ss)
        {
            lock (Slaves)
            {
                lock (ComputerTree)
                {
                    List<TreeNode> Instances = FindAllInstancesOfSlave(ss.HostName);

                    Slaves.Remove(ss);
                    Terminal.RemoveSlave(ss);
                    foreach (TreeNode tn in Instances)
                    {
                        tn.Parent.Nodes.Remove(tn);
                        tn.Parent.Text = ((HostGroup)tn.Parent.Tag).GetDisplayText();
                    }
                    Legend.Invalidate();
                }
            }
        }

        private void OnRemoveNode_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;            
            if (ContextNode.Tag is Slave)
            {
                Slave ss = ContextNode.Tag as Slave;

                // Removing a slave...            
                if (MessageBox.Show("Remove the host '" + ss.HostName + "'?", "Please confirm", MessageBoxButtons.OKCancel) != DialogResult.OK) return;

                lock (ComputerTree)
                {
                    List<TreeNode> Instances = FindAllInstancesOfSlave(ss.HostName);
                    if (Instances.Count > 1)
                        if (MessageBox.Show("The host '" + ss.HostName + "' is a member of " + (Instances.Count - 1).ToString() + " groups.  Are you sure you want to remove it from all listings?", "Please confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                }

                RemoveHost(ss);
            }
            else
            {
                HostGroup Group = ContextNode.Tag as HostGroup;
                if (Group == null) return;

                // Removing a group...            
                if (MessageBox.Show("Remove the group '" + Group.Name + "'?", "Please confirm", MessageBoxButtons.OKCancel) != DialogResult.OK) return;

                bool AlsoHosts = false;
                switch (MessageBox.Show("Do you also want to remove all hosts that are part of the group '" + Group.Name + "'?", "Please confirm", MessageBoxButtons.YesNoCancel))
                {
                    case DialogResult.Yes: AlsoHosts = true; break;
                    case DialogResult.No: AlsoHosts = false; break;
                    case DialogResult.Cancel: return;
                    default: return;
                }                

                lock (Slaves)
                {
                    lock (ComputerTree)
                    {
                        if (AlsoHosts)
                        {
                            foreach (Slave ss in Group.Slaves) RemoveHost(ss);
                        }

                        List<TreeNode> Instances = FindAllInstancesOfGroup(Group.Name);

                        foreach (TreeNode tnGroup in Instances)                        
                            tnGroup.Parent.Nodes.Remove(tnGroup);

                        Groups.Remove(Group);
                        Legend.Invalidate();
                    }
                }
            }

            SaveHostListToRegistry();
            SaveGroupListToRegistry();
        }

        private void OnConnectNode_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;
            try
            {
                if (ContextNode.Tag is Slave)
                {
                    Slave ss = ContextNode.Tag as Slave;
                    ss.Connect(false, UserCredentials);
                }
                else if (ContextNode.Tag is HostGroup)
                {
                    HostGroup Group = ContextNode.Tag as HostGroup;
                    foreach (Slave ss in Group.Slaves) ss.Connect(false, UserCredentials);
                }
                else return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to connect: " + ex.ToString());
            }
        }

        private void OnDisconnectNode_MenuItemClick(object sender, EventArgs e)
        {
            if (ContextNode == null) return;
            try
            {
                if (ContextNode.Tag is Slave)
                {
                    Slave ss = ContextNode.Tag as Slave;
                    ss.Disconnect();
                }
                else if (ContextNode.Tag is HostGroup)
                {
                    HostGroup Group = ContextNode.Tag as HostGroup;
                    foreach (Slave ss in Group.Slaves) ss.Disconnect();
                }
                else return;
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
            Slave ContextSlave = ContextNode.Tag as Slave;
            HostGroup ContextGroup = ContextNode.Tag as HostGroup;

            MenuItem miConnect = new MenuItem("&Connect", OnConnectNode_MenuItemClick);
            MenuItem miDisconnect = new MenuItem("&Disconnect", OnDisconnectNode_MenuItemClick);
            MenuItem miSep = new MenuItem("-");
            MenuItem miEditGroups = new MenuItem("Change &Group Memberships...", OnEditMembership_MenuItemClick);
            MenuItem miRemove = new MenuItem("&Remove Host", OnRemoveNode_MenuItemClick);

            MenuItem[] menuItems;
            if (ContextSlave != null)
            {
                miConnect.Enabled = ContextSlave.IsDisconnected;
                miDisconnect.Enabled = !ContextSlave.IsDisconnected;

                menuItems = new MenuItem[]{
                    miConnect,
                    miDisconnect,
                    miSep,
                    miEditGroups,
                    miRemove
                };
            }
            else if (ContextGroup != null)
            {
                miConnect.Enabled = ContextGroup.CanConnect;
                miDisconnect.Enabled = ContextGroup.CanDisconnect;

                menuItems = new MenuItem[]{
                    miConnect,
                    miDisconnect,
                    miSep,
                    miRemove
                };
            }
            else if (e.Node == HostNodes)
            {
                MenuItem miAdd = new MenuItem("&Add Host", addHostStripMenuItem_Click);

                menuItems = new MenuItem[]{
                    miAdd
                };
            }
            else if (e.Node == GroupNodes)
            {
                MenuItem miAdd = new MenuItem("&Add Group", addGroupToolStripMenuItem_Click);

                menuItems = new MenuItem[]{
                    miAdd
                };
            }
            else return;

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
