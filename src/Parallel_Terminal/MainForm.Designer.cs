namespace Parallel_Terminal
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.MainSplitContainer = new System.Windows.Forms.SplitContainer();
            this.ComputerTree = new System.Windows.Forms.TreeView();
            this.Legend = new Parallel_Terminal.MarkerLegendControl();
            this.Terminal = new Parallel_Terminal.TerminalControl();
            this.GUITimer = new System.Windows.Forms.Timer(this.components);
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.hostsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addHostStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.consolesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reloadAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.addGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.MainSplitContainer)).BeginInit();
            this.MainSplitContainer.Panel1.SuspendLayout();
            this.MainSplitContainer.Panel2.SuspendLayout();
            this.MainSplitContainer.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainSplitContainer
            // 
            this.MainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MainSplitContainer.Location = new System.Drawing.Point(0, 24);
            this.MainSplitContainer.Name = "MainSplitContainer";
            // 
            // MainSplitContainer.Panel1
            // 
            this.MainSplitContainer.Panel1.Controls.Add(this.ComputerTree);
            // 
            // MainSplitContainer.Panel2
            // 
            this.MainSplitContainer.Panel2.Controls.Add(this.Legend);
            this.MainSplitContainer.Panel2.Controls.Add(this.Terminal);
            this.MainSplitContainer.Size = new System.Drawing.Size(854, 722);
            this.MainSplitContainer.SplitterDistance = 284;
            this.MainSplitContainer.TabIndex = 0;
            this.MainSplitContainer.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.MainSplitContainer_SplitterMoved);
            this.MainSplitContainer.Resize += new System.EventHandler(this.MainSplitContainer_Resize);
            // 
            // ComputerTree
            // 
            this.ComputerTree.CheckBoxes = true;
            this.ComputerTree.Location = new System.Drawing.Point(0, 0);
            this.ComputerTree.Name = "ComputerTree";
            this.ComputerTree.Size = new System.Drawing.Size(284, 746);
            this.ComputerTree.TabIndex = 0;
            this.ComputerTree.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.ComputerTree_AfterCheck);
            this.ComputerTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.ComputerTree_AfterSelect);
            this.ComputerTree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.ComputerTree_NodeMouseClick);
            // 
            // Legend
            // 
            this.Legend.BackColor = System.Drawing.Color.White;
            this.Legend.Location = new System.Drawing.Point(-1, 682);
            this.Legend.Name = "Legend";
            this.Legend.Size = new System.Drawing.Size(567, 40);
            this.Legend.TabIndex = 1;
            this.Legend.Text = "markerLegendControl1";
            // 
            // Terminal
            // 
            this.Terminal.BackColor = System.Drawing.Color.White;
            this.Terminal.Location = new System.Drawing.Point(-1, 0);
            this.Terminal.Name = "Terminal";
            this.Terminal.Size = new System.Drawing.Size(567, 676);
            this.Terminal.TabIndex = 0;
            this.Terminal.Text = "Terminal";
            this.Terminal.TextFontSizeInPoints = 14F;
            this.Terminal.Click += new System.EventHandler(this.Terminal_Click);
            // 
            // GUITimer
            // 
            this.GUITimer.Tick += new System.EventHandler(this.GUITimer_Tick);
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.hostsToolStripMenuItem,
            this.groupsToolStripMenuItem,
            this.consolesToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(854, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(92, 22);
            this.exitToolStripMenuItem.Text = "E&xit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // hostsToolStripMenuItem
            // 
            this.hostsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addHostStripMenuItem});
            this.hostsToolStripMenuItem.Name = "hostsToolStripMenuItem";
            this.hostsToolStripMenuItem.Size = new System.Drawing.Size(49, 20);
            this.hostsToolStripMenuItem.Text = "&Hosts";
            // 
            // addHostStripMenuItem
            // 
            this.addHostStripMenuItem.Name = "addHostStripMenuItem";
            this.addHostStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.addHostStripMenuItem.Text = "&Add...";
            this.addHostStripMenuItem.Click += new System.EventHandler(this.addHostStripMenuItem_Click);
            // 
            // consolesToolStripMenuItem
            // 
            this.consolesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.reloadAllToolStripMenuItem});
            this.consolesToolStripMenuItem.Name = "consolesToolStripMenuItem";
            this.consolesToolStripMenuItem.Size = new System.Drawing.Size(67, 20);
            this.consolesToolStripMenuItem.Text = "&Consoles";
            // 
            // reloadAllToolStripMenuItem
            // 
            this.reloadAllToolStripMenuItem.Name = "reloadAllToolStripMenuItem";
            this.reloadAllToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.reloadAllToolStripMenuItem.Text = "&Reload all";
            this.reloadAllToolStripMenuItem.Click += new System.EventHandler(this.reloadAllToolStripMenuItem_Click);
            // 
            // groupsToolStripMenuItem
            // 
            this.groupsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addGroupToolStripMenuItem});
            this.groupsToolStripMenuItem.Name = "groupsToolStripMenuItem";
            this.groupsToolStripMenuItem.Size = new System.Drawing.Size(57, 20);
            this.groupsToolStripMenuItem.Text = "&Groups";
            // 
            // addGroupToolStripMenuItem
            // 
            this.addGroupToolStripMenuItem.Name = "addGroupToolStripMenuItem";
            this.addGroupToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.addGroupToolStripMenuItem.Text = "&Add Group...";
            this.addGroupToolStripMenuItem.Click += new System.EventHandler(this.addGroupToolStripMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(854, 746);
            this.Controls.Add(this.MainSplitContainer);
            this.Controls.Add(this.menuStrip1);
            this.DoubleBuffered = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.Text = "Parallel Terminal";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.MainSplitContainer.Panel1.ResumeLayout(false);
            this.MainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.MainSplitContainer)).EndInit();
            this.MainSplitContainer.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer MainSplitContainer;
        private System.Windows.Forms.TreeView ComputerTree;
        private TerminalControl Terminal;
        private System.Windows.Forms.Timer GUITimer;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem hostsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addHostStripMenuItem;
        private MarkerLegendControl Legend;
        private System.Windows.Forms.ToolStripMenuItem consolesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem reloadAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem groupsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem addGroupToolStripMenuItem;
    }
}

