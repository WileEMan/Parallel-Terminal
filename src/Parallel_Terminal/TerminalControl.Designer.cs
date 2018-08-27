namespace Parallel_Terminal
{
    partial class TerminalControl
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

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.GUITimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // GUITimer
            // 
            this.GUITimer.Tick += new System.EventHandler(this.GUITimer_Tick);
            // 
            // TerminalControl
            // 
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TerminalControl_KeyDown);
            this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.TerminalControl_KeyPress);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.TerminalControl_KeyUp);
            this.MouseLeave += new System.EventHandler(this.TerminalControl_MouseLeave);
            this.MouseHover += new System.EventHandler(this.TerminalControl_MouseHover);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.TerminalControl_MouseMove);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.TerminalControl_PreviewKeyDown);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer GUITimer;
    }
}
