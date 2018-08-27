#define RunSelfTest

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Diagnostics;
using System.Globalization;

namespace Parallel_Terminal
{
    // TODO: copy-and-paste would be very nice.
    
    /// <summary>
    /// TerminalControl contains the primary visual/GUI work of this application in organizing the lines coming in from multiple hosts and "diff"-ing them, so to speak.  Actually the goal is more to merge common
    /// lines than to diff them.  The key data structures of TerminalControl are:
    /// 
    ///     Each Slave (host) has a SlaveBuffer in SlaveBuffers.
    ///     There is a CurrentDisplay set that references which hosts are currently allowed to display text in the TerminalControl.
    ///     There is a CurrentMarks set that references which hosts are currently allowed to be marked in common lines in the TerminalControl (more liberal than text as it only takes a tiny bit of space).
    ///     A CurrentLayout List maintains the current listing of all lines that are potentially display in the current state.  Updated by new lines and rebuilt by changes to CurrentDisplay.
    ///     Vertical scroll references the starting line in CurrentLayout to use for display.
    ///     Each SlaveBuffer has a CurrentLine that is treated separately from the comitted line listing.
    /// 
    /// There are multiple line indexing systems in play:
    ///     Each SlaveBuffer has a List of lines.
    ///     Each Line has a DisplaySequence assigned to it.  Each Line can reference sibling Lines as well.
    ///     The CurrentLayout list index identifies the on-screen position of lines currently being shown.
    ///     The SlaveBuffers each have a dictionary cross-referencing the "last common line" to all other SlaveBuffers.
    ///     As notation, DisplaySequence values are shown in angle brackets and line indices are shown in square brackets.
    /// 
    /// The data flow into TerminalControl is as follows:
    /// 
    ///     1. New console data comes in on a OnMessage event.
    ///     2. All text to be displayed runs through the AddText() function, which appends to the CurrentLine StringBuilder of the SlaveBuffer in question.    
    ///     3. When AddText() has found a "\n" character to be appended, it calls OnLine() with the constructed line.
    ///     4. OnLine() has primary responsibility for organizing new lines as they come in.
    ///         A. OnLine() constructs a new Line object and fills it with the text from the line.
    ///         B. A tentative DisplaySequence is assigned to the new line by incrementing from the previously-final line in the SlaveBuffer.
    ///         C. A sibling search is performed for the line by starting from the "last common line" of all other SlaveBuffers and looking for identical lines*.
    ///         D. If siblings are found and the new line appears in a SlaveBuffer with more content leading up to the new common line than sibling SlaveBuffers,
    ///            then those sibling SlaveBuffers have their DisplaySequence incremented to make room.  This requires incrementing all DisplaySequence numbers
    ///            in each affected sibling from the new common line and any that follow.  This will impart jumps in that SlaveBuffer's DisplaySequence but
    ///            each DisplaySequence value (between 0...max) is used somewhere, just not necessarily in each individual SlaveBuffer.  As the lines of
    ///            affected SlaveBuffers are being increased, additional sibling connections to SlaveBuffers not in the affected list are checked and if
    ///            they are being incremented the effect cascades into additional SlaveBuffers.
    ///     E. OnLine() calls BuildCurrentLayout() with the minimum display sequence number that has been affected by the new line.
    ///         A. BuildCurrentLayout() truncates the CurrentLayout list where the affected display sequence number (or any higher sequence number)
    ///            first appears and constructs a new layout from that sequence number on.
    ///     F. Whole-Console messages can trigger a clearing and complete rebuild of one SlaveBuffer and those it touches.
    ///     F. Paint and GUI events make use of the CurrentLayout.
    ///
    ///  * There is a special exemption for empty lines, as they cannot be used to initiate sync but can be used to continue sync.
    /// </summary>
    public partial class TerminalControl : Control
    {
        public void SelfTest()
        {
#if RunSelfTest && DEBUG
            lock (SlaveBuffers)
            {
                List<Slave> Slaves = new List<Slave>();
                foreach (var kvp in SlaveBuffers) Slaves.Add(kvp.Key);

                //ClearWholeConsole(Slaves[0]);
                //ClearWholeConsole(Slaves[1]);

                AddText(Slaves[0], "Debugging: 1st Common Test Line\n");
                AddText(Slaves[1], "Debugging: 1st Common Test Line\n");
                AddText(Slaves[1], "Debugging: Line from 2nd host only\n");
                AddText(Slaves[0], "Debugging: 2nd Common Test Line\n");
                AddText(Slaves[1], "Debugging: 2nd Common Test Line\n");
                AddText(Slaves[1], "Debugging: 3rd Common Test Line\n");
                AddText(Slaves[0], "Debugging: 3rd Common Test Line\n");
                AddText(Slaves[1], "Debugging: 4th Common Test Line\n");                                //  1   [4] -> [7]
                AddText(Slaves[0], "Debugging: 5th Common Test Line (but out-of-sequence)\n");          // 0    [4] should not update to [8] because 4th common line comes in after.
                AddText(Slaves[0], "Debugging: Line A from 1st host only\n");                           // 0    [5]
                AddText(Slaves[0], "Debugging: Line B from 1st host only\n");                           // 0    [6]   
                AddText(Slaves[0], "Debugging: 4th Common Test Line\n");                                // 0           [7]
                AddText(Slaves[1], "Debugging: 5th Common Test Line (but out-of-sequence)\n");          //  1                 [8]
                AddText(Slaves[0], "\n");
                AddText(Slaves[1], "\n");

                AddText(Slaves[0], "Sequence repeated with reversal of hosts:\n");
                AddText(Slaves[1], "Debugging: 1st Common Test Line\n");
                AddText(Slaves[0], "Debugging: 1st Common Test Line\n");
                AddText(Slaves[0], "Debugging: Line from 1st host only\n");
                AddText(Slaves[1], "Debugging: 2nd Common Test Line\n");
                AddText(Slaves[0], "Debugging: 2nd Common Test Line\n");
                AddText(Slaves[0], "Debugging: 3rd Common Test Line\n");
                AddText(Slaves[1], "Debugging: 3rd Common Test Line\n");
                AddText(Slaves[0], "Debugging: 4th Common Test Line\n");                                //  1   [4] -> [7]
                AddText(Slaves[1], "Debugging: 5th Common Test Line (but out-of-sequence)\n");          // 0    [4] should not update to [8] because 4th common line comes in after.
                AddText(Slaves[1], "Debugging: Line A from 2nd host only\n");                           // 0    [5]
                AddText(Slaves[1], "Debugging: Line B from 2nd host only\n");                           // 0    [6]   
                AddText(Slaves[1], "Debugging: 4th Common Test Line\n");                                // 0           [7]
                AddText(Slaves[0], "Debugging: 5th Common Test Line (but out-of-sequence)\n");          //  1                 [8]
                AddText(Slaves[0], "\n");
                AddText(Slaves[1], "\n");

                AddText(Slaves[0], "Third test set:\n");
                AddText(Slaves[1], "Third test set:\n");
                AddText(Slaves[0], "Debugging: Line A from 1st host only\n");
                AddText(Slaves[1], "Debugging: Line A from 2nd host only\n");
                AddText(Slaves[0], "Debugging: Line B from 1st host only\n");
                AddText(Slaves[1], "Debugging: Line B from 2nd host only\n");
                AddText(Slaves[0], "Debugging: 1st Common Test Line\n");
                AddText(Slaves[1], "Debugging: 1st Common Test Line\n");
                AddText(Slaves[1], "Debugging: Line C from 2nd host only\n");
                AddText(Slaves[0], "\n");
                AddText(Slaves[1], "\n");
            }
#endif
        }

        public TerminalControl()
        {            
            InitializeComponent();
            this.BackColor = Color.White;
            GUITimer.Enabled = true;            

            VScroll = new VScrollBar();
            VScroll.Dock = DockStyle.Right;
            VScroll.Scroll += OnVScroll;    
            this.Controls.Add(VScroll);

            MouseWheel += OnMouseWheel;

            // Invoke the set handler because it does a measurement.
            TextFontSizeInPoints = TextFontSizeInPoints;

            this.DoubleBuffered = true;
        }

#region "Painting/Display"

        int TextFontHeight;

        public float TextFontSizeInPoints
        {
            get { return TextFont.SizeInPoints; }
            set
            {                             
                TextFont = new Font(TextFont.FontFamily, value);

                using (Graphics g = CreateGraphics())
                {
                    SizeF sz = g.MeasureString("Yg", TextFont);
                    TextFontHeight = (int)Math.Round(sz.Height);
                }

                Invalidate();
            }
        }        

        class HoverObject
        {
            public Rectangle MousePosition;
            public string HoverText;

            public HoverObject(Rectangle MP, string HT) { MousePosition = MP;  HoverText = HT; }
        }

        // Information generated during painting for handling other events
        List<HoverObject> CurrentHoverObjects = new List<HoverObject>();
        bool ShowingCurrentLine = true;

        Font TextFont = new Font(FontFamily.GenericMonospace, 14.0f);
        Brush TextBrush = new SolidBrush(Color.Black);
        Font SlaveLabelFont = new Font(FontFamily.GenericSansSerif, 12.0f);
        Pen BorderPen = new Pen(Color.Black, 1.0f);
        Pen BorderPenFocus = new Pen(Color.Black, 4.0f);

        const int LeftMargin = 5 /*pixels*/;
        const int VBetweenLines = 1;
        const int SlaveLabelMarkerHeight = 2 /*pixels*/;
        const int HPaddingBetweenSlaveLabelMarkers = 10 /*pixels*/;

#if false       // Old style, underline
        void PaintSlaveMarkers(Graphics g, int yLine, List<Slave> Markers)
        {
            // Order them by their ID just so that it is always consistent ordering on the screen.
            Markers.Sort(delegate (Slave x, Slave y) { return (x.ID < y.ID) ? -1 : 1; });

            // Draw them
            int xx = LeftMargin;
            for (int ii = 0; ii < Markers.Count; ii++)
            {
                SizeF szLabel = g.MeasureString(Markers[ii].HostName, SlaveLabelFont);
                Rectangle rr = new Rectangle(xx, yLine + (int)szLabel.Height + 1, (int)szLabel.Width, SlaveLabelMarkerHeight);
                g.FillRectangle(new SolidBrush(Markers[ii].DarkColor), rr);
                xx += (int)szLabel.Width + HPaddingBetweenSlaveLabelMarkers;
                CurrentHoverObjects.Add(new HoverObject(rr, Markers[ii].HostName));
            }
        }
#else           // New style, on the left
        int PaintSlaveMarkers(Graphics g, int yLine, int yLineHeight, List<Slave> Slaves)
        {            
            // Draw them
            int xx = LeftMargin;
            int MarkerWidth = 4;
            for (int ii = 0; ii < CurrentMarks.Count; ii++, xx += MarkerWidth)
            {
                Slave ss;
                if (Slaves.Contains(CurrentMarks[ii].Slave)) ss = CurrentMarks[ii].Slave;
                else continue;

                Rectangle rr = new Rectangle(xx, yLine, MarkerWidth, yLineHeight);
                g.FillRectangle(new SolidBrush(ss.DarkColor), rr);
                CurrentHoverObjects.Add(new HoverObject(rr, ss.HostName));
            }
            return xx + MarkerWidth;
        }
#endif

        public List<Slave> GetMarkerLegend()
        {
            lock (SlaveBuffers)
            {
                var Slaves = new List<Slave>();
                foreach (var sb in CurrentMarks) Slaves.Add(sb.Slave);
                return Slaves;
            }
        }

        void PaintCurrentLine(Graphics g, int yy, int LineHeight)
        {
            List<bool> Covered = new List<bool>();
            foreach (SlaveBuffer sb in CurrentDisplay) Covered.Add(false);
            for (int iSB = 0; iSB < CurrentDisplay.Count; iSB++)
            {
                if (Covered[iSB]) continue;         // If true, then we've already shown this line as a sibling of a previous line.                                

                // Determine if there are any other CurrentLines in the CurrentDisplay that are identical and can be shown together.  If so,
                // we can A) not show them again and B) mark their colors on this line.
                List<Slave> SiblingSlaves = new List<Slave>();
                SiblingSlaves.Add(CurrentDisplay[iSB].Slave);
                for (int jSB = iSB + 1; jSB < CurrentDisplay.Count; jSB++)
                {
                    if (CurrentDisplay[iSB].CurrentLine.ToString() == CurrentDisplay[jSB].CurrentLine.ToString())
                    {
                        Covered[jSB] = true;
                        SiblingSlaves.Add(CurrentDisplay[jSB].Slave);
                    }
                }

                int xx = PaintSlaveMarkers(g, yy, LineHeight, SiblingSlaves);

                g.DrawString(CurrentDisplay[iSB].CurrentLine.ToString(), TextFont, TextBrush, new PointF(xx, yy));

                yy += LineHeight;
            }
        }

        int LinesFittingClientHeight
        {
            get { return (int)Math.Ceiling((double)ClientRectangle.Height / (TextFontHeight + VBetweenLines)); }
        }

        /// <summary>
        /// This list contains the final summary of the current display layout.  Each index of CurrentLayout corresponds to one line being independently displayed on-screen,
        /// with the exception of the CurrentLine displays that are excluded.  (The CurrentLine displays are those where the user can still type and \n hasn't come up yet).
        /// 
        /// CurrentLayout can be updated when a new line comes in or is committed (although this update should only happen toward the end) or when setting changes require a
        /// rewrite of the layout, such as if a checkbox is changed affecting CurrentDisplay.
        /// 
        /// This list is protected with the SlaveBuffers lock.
        /// </summary>
        List<Line> CurrentLayout = new List<Line>();

        /// <summary>
        /// BuildCurrentLayout() constructs or updates the CurrentLayout list.  It does not alter the underlying SlaveBuffers or Lines at all, but only constructs the
        /// CurrentLayout cross-reference into the SlaveBuffers.  If FromSequenceNumber is zero then the entire CurrentLayout is constructed, whereas if it is non-zero
        /// then only CurrentLayout entries with that DisplaySequence number or higher are rebuilt.
        /// </summary>
        /// <param name="FromSequenceNumber"></param>
        void BuildCurrentLayout(int FromSequenceNumber = 0)
        {
            lock (SlaveBuffers)
            {
                int InitialLength = CurrentLayout.Count;
                if (InitialLength == 0) ShowingCurrentLine = true;

                List<int> iInBuffer = new List<int>();  // Contains the current index in each of the CurrentDisplay line buffers, initially zero.
                foreach (SlaveBuffer sb in CurrentDisplay) iInBuffer.Add(0);

                // Find the MaxDisplaySequence value to know when we can stop.
                int MaxDisplaySequence = 0;
                foreach (var kv in SlaveBuffers)
                {
                    SlaveBuffer sb = kv.Value;
                    int ThisSBMaxDisplaySequence = 0;
                    if (sb.Lines.Count > 0) ThisSBMaxDisplaySequence = sb.Lines[sb.Lines.Count - 1].DisplaySequence;
                    if (ThisSBMaxDisplaySequence > MaxDisplaySequence) MaxDisplaySequence = ThisSBMaxDisplaySequence;
                }

                int VerticalSeq = FromSequenceNumber;         // Contains the current DisplaySequence value to start the top of the display from, and will increase as we go down.                

                // If we are rebuilding the entire CurrentLayout or doing the initial build, this is easy.  But, if we are just updating something toward the end, i.e.
                // when a new line comes in, this gets a little more complicated.  We need to find the first reference to this sequence number and rebuild all layout
                // that follows that line.  We need to ensure that any state is consistent whether we are starting from the middle or rebuilding the whole thing, although
                // fortunately this is pretty easy at this stage because iInBuffer is the only state so far and it automatically moves forward to find the sequence number
                // of VerticalSeq.  MaxDisplaySequence isn't effected because we are only changing the layout and not the underlying buffers.
                if (FromSequenceNumber == 0)
                {
                    CurrentLayout.Clear();
                }
                else 
                {
                    // Lob off all entries after DisplaySequence shows up from CurrentLayout...
                    // Unfortunately, when new lines come in the DisplaySequence of existing lines can be changed as a result of sibling connections and renumbers.
                    // This is already handled in the individual SlaveBuffers before BuildCurrentLayout() gets called, but it can break the rule that the CurrentLayout
                    // has ascending DisplaySequence order after the renumbering.  For example, if host A sends lines Alpha and Beta then host B sends lines Phi and Alpha, then
                    // host A's Alpha+Beta were originally numbered [0]+[1] but becomes [1]+[2] to make room for B's Phi ahead of A's Alpha (B lines become [0]+[1]).  We must
                    // truncate starting at the first occurrance of FromSequenceNumber as a DisplaySequence, and because of the possible reordering this means we can't assume
                    // always-ascending DisplaySequence with CurrentLayout index at this time.                    
                    // TODO: There is probably an optimization opportunity here by tracking where the re-ordering happened.
                    for (int ii = 0; ii < CurrentLayout.Count; ii++)
                    {
                        if (CurrentLayout[ii].DisplaySequence >= FromSequenceNumber)
                        {
                            CurrentLayout.RemoveRange(ii, CurrentLayout.Count - ii);
                            break;
                        }
                    }

                    // If the FromSequenceNumber is greater than the highest DisplaySequence found in the CurrentLayout, then we do not need to remove anything from the 
                    // CurrentLayout because we are appending.  We also must not remove anything from the layout, because we are starting our rebuild at VerticalSeq = FromSequenceNumber.
                }

                if (CurrentDisplay == null) return;

                while (VerticalSeq <= MaxDisplaySequence)
                {
                    // We're going to step through each of the CurrentDisplay buffers and find lines at this DisplaySequence.  Some may be siblings to other lines from the
                    // current CurrentDisplay set, and therefore they'll be shown together.  Others may have a common DisplaySequence number but not be siblings.  We'll need
                    // to avoid re-displaying the siblings later in the set that we've found earlier, so we track which ones we've hit.
                    bool[] Shown = new bool[CurrentDisplay.Count];
                    for (int ii = 0; ii < CurrentDisplay.Count; ii++) Shown[ii] = false;

                    // Iterate through all the CurrentDisplay buffers and see if we can find the current DisplaySequence (VerticalSeq) in any of them.  As we do it, push our
                    // line indices in each buffer higher so we don't repeat the search over entries later (because the Lines within each SlaveBuffer have an always-increasing
                    // DisplaySequence number.)
                    for (int iSB = 0; iSB < CurrentDisplay.Count; iSB++)
                    {
                        Line ln = null;

                        if (Shown[iSB]) continue;

                        // Iterate forward through lines in the buffer to see if we can find one with VerticalSeq as the DisplaySequence.
                        SlaveBuffer sb = CurrentDisplay[iSB];
                        for (; iInBuffer[iSB] < sb.Lines.Count; iInBuffer[iSB]++)
                        {
                            if (sb.Lines[iInBuffer[iSB]].DisplaySequence == VerticalSeq) { ln = sb.Lines[iInBuffer[iSB]]; break; }

                            // If we've found a DisplaySequence past what we're looking for, then we can stop looking at this buffer for now because we aren't going to find
                            // it (and don't want to move any further and miss a DisplaySequence we need next time).
                            if (sb.Lines[iInBuffer[iSB]].DisplaySequence > VerticalSeq) break;
                        }

                        // If we didn't find a line in this particular buffer then there's nothing to show.  Move along.
                        if (ln == null) continue;

                        // Figure out which siblings we are simultaneously showing by the above DrawString command and mark them.
                        foreach (Line lnSibling in ln.Siblings)
                        {
                            int Index = CurrentDisplay.IndexOf(lnSibling.Container);
                            if (Index < 0) continue;
                            Shown[Index] = true;
                        }

                        // Commit a line to the layout
                        CurrentLayout.Add(ln);
                    }

                    VerticalSeq++;
                }                
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {            
            base.OnPaint(pe);

            CurrentHoverObjects.Clear();

            Graphics g = pe.Graphics;
            //SizeF sz = g.MeasureString("Yg", TextFont);

            //if (CanFocus)
                //g.DrawString("Hello!", TextFont, TextBrush, new PointF(10, 10));

            if (ContainsFocus)
            {
                g.DrawRectangle(BorderPenFocus, ClientRectangle);                
            }
            else
                g.DrawRectangle(BorderPen, ClientRectangle);            

            // Precondition: BuildCurrentLayout() has been called on all committed lines and has constructed CurrentLayout.

            lock (SlaveBuffers)
            {
                ShowingCurrentLine = false;

                int LayoutIndex = VScroll.Value + PendingVScrollLines;
                if (LayoutIndex < 0) LayoutIndex = 0;
                int yy = 0;
                while (yy <= ClientRectangle.Height)
                {
                    if (LayoutIndex >= CurrentLayout.Count)
                    {
                        PaintCurrentLine(g, yy, TextFontHeight + VBetweenLines);
                        // If we get here before we exceed the height of the client area, then the current line is at least partially being shown.
                        ShowingCurrentLine = true;
                        break;
                    }

                    Line ln = CurrentLayout[LayoutIndex];
                    
                    /** Draw the sibling list **/

                    // First, convert from a list of lines to a list of Slave objects.
                    List<Slave> Slaves = new List<Slave>();
                    foreach (Line lnSibling in ln.Siblings) Slaves.Add(lnSibling.Container.Slave);
                    Slaves.Add(ln.Container.Slave);

                    int xx = PaintSlaveMarkers(g, yy, TextFontHeight + VBetweenLines, Slaves);                        

#if DEBUG
                    g.DrawString("<" + ln.DisplaySequence + "> " + ln.Text, TextFont, TextBrush, new PointF(xx, yy));
#else
                    g.DrawString(ln.Text, TextFont, TextBrush, new PointF(xx, yy));
#endif

                    yy += TextFontHeight + VBetweenLines;
                    LayoutIndex++;
                }
            }
        }

#endregion

#region "Slave/Host Management"

        class Line
        {
            // Fixed upon receipt from slave:
            public SlaveBuffer Container;
            public string Text;
            public int TextHash;

            // Can be updated as new lines come in:
            public List<Line> Siblings = new List<Line>();
            public int DisplaySequence;

            public Line(SlaveBuffer Container, string Text)
            {
                this.Container = Container;
                this.Text = Text;
                this.TextHash = this.Text.GetHashCode();
            }

            public override string ToString()
            {
                return "[" + DisplaySequence.ToString() + "] " + "\"" + Text + "\" in " + Container.ToString();
            }
        }

        class SlaveBuffer
        {
            public int SequenceStart = 0;
            public Slave Slave;
            public List<Line> Lines = new List<Line>();
            public StringBuilder CurrentLine = new StringBuilder();
            public Dictionary<SlaveBuffer, int> LastCommon = new Dictionary<SlaveBuffer, int>();

            public SlaveBuffer(Slave Slave, int DisplaySequenceStart)
            {
                this.Slave = Slave;
                this.SequenceStart = DisplaySequenceStart;
            }

            public override string ToString()
            {
                return "SlaveBuffer for " + Slave.HostName;
            }
        }

        Dictionary<Slave, SlaveBuffer> SlaveBuffers = new Dictionary<Slave, SlaveBuffer>();
        List<SlaveBuffer> CurrentDisplay = new List<SlaveBuffer>();

        /// <summary>
        /// CurrentMarks indicates the set and order of marker display.  It can be identical to CurrentDisplay, but can also include more hosts.  CurrentDisplay will show lines of text from
        /// the marked hosts, but CurrentMarks can additionally show sibling/common text to hosts that the user hasn't highlighted but may be in and out of reviewing.  The marks are small so
        /// the user can usually handle seeing them even if they are limiting their text display.
        /// 
        /// For now, all slaves get marks, but group selection would be better.
        /// </summary>
        List<SlaveBuffer> CurrentMarks = new List<SlaveBuffer>();

        public void SetCurrentTextDisplay(List<Slave> ToSlaves)
        {
            CurrentDisplay.Clear();
            foreach (Slave ss in ToSlaves) CurrentDisplay.Add(SlaveBuffers[ss]);
            BuildCurrentLayout();
            Invalidate();
        }

        public void SetCurrentMarkerDisplay(List<Slave> ToSlaves)
        {
            CurrentMarks.Clear();
            foreach (Slave ss in ToSlaves) CurrentMarks.Add(SlaveBuffers[ss]);
            Invalidate();
        }

        /// <summary>
        /// When adding slaves simultaneously, first add all Slave objects via AddSlave().  After all slaves have been
        /// added here, start connecting them and allowing data flow.
        /// </summary>
        /// <param name="NewSlave"></param>
        public void AddSlave(Slave NewSlave)
        {
            lock (SlaveBuffers)
            {
                int MaxDisplaySequence = 0;
                foreach (var kp in SlaveBuffers)
                {
                    SlaveBuffer sb = kp.Value;
                    if (sb.Lines.Count > 0 && sb.Lines[sb.Lines.Count - 1].DisplaySequence > MaxDisplaySequence)
                        MaxDisplaySequence = sb.Lines[sb.Lines.Count - 1].DisplaySequence;
                }

                SlaveBuffer NewSB = new SlaveBuffer(NewSlave, MaxDisplaySequence);
                SlaveBuffers.Add(NewSlave, NewSB);
                CurrentMarks.Add(NewSB);
            }
        }

        public void RemoveSlave(Slave RemoveSlave)
        {
            lock (SlaveBuffers)
            {
                // Before we remove this SlaveBuffer, we need to disconnect it from all sibling listings.
                SlaveBuffer SlaveBuffer = SlaveBuffers[RemoveSlave];
                for (int ii=0; ii < SlaveBuffer.Lines.Count; ii++)
                {
                    Line ln = SlaveBuffer.Lines[ii];
                    if (ln.Siblings.Count > 0)
                    {
                        foreach (Line lnSibling in ln.Siblings) lnSibling.Siblings.Remove(ln);
                    }
                }

                if (CurrentDisplay.Contains(SlaveBuffer)) CurrentDisplay.Remove(SlaveBuffer);
                if (CurrentMarks.Contains(SlaveBuffer)) CurrentMarks.Remove(SlaveBuffer);
                SlaveBuffers.Remove(RemoveSlave);
                BuildCurrentLayout();
            }
        }

        //Encoding ConsoleEncoding = Encoding.GetEncoding(437);

        public void OnMessage(Slave FromSlave, XElement xMsg)
        {
            if (xMsg.Name.LocalName == "Portal-Version")
            {
                string Version = xMsg.Attribute("Core-Library").Value;
                AddText(FromSlave, "\nConnection established to host portal with Core-Library version " + Version + "...\n");
            }

            if (xMsg.Name == "Terminal-Connected")
            {
                bool Reconnect = false;
                string FullUserName = xMsg.Attribute("full-user-name").Value;
                if (xMsg.Attribute("Reconnected") != null) Reconnect = (bool)xMsg.Attribute("Reconnected");
                if (!Reconnect)
                    AddText(FromSlave, "Terminal connected as user '" + FullUserName + "'.\n");
                else
                    AddText(FromSlave, "Terminal reconnected as user '" + FullUserName + "'.\n");
            }

            if (xMsg.Name.LocalName == "Console-New")
            {
                //int lastx = int.Parse(xMsg.Attribute("Last-X").Value);
                //int lasty = int.Parse(xMsg.Attribute("Last-Y").Value);
                //Debug.WriteLine("Received from host '" + FromSlave.HostName + "' Console-New @ " + lastx + ", " + lasty);

                string DecodedText = Encoding.Unicode.GetString(System.Convert.FromBase64String(xMsg.Value));
                AddText(FromSlave, DecodedText, false, true);
            }

            if (xMsg.Name.LocalName == "Current-Console-Line")
            {
                string DecodedText = Encoding.Unicode.GetString(System.Convert.FromBase64String(xMsg.Value));
                UpdateCurrentLine(FromSlave, DecodedText);                
            }

            if (xMsg.Name.LocalName == "Whole-Console")
            {
                string DecodedText = Encoding.Unicode.GetString(System.Convert.FromBase64String(xMsg.Value));
                ClearWholeConsole(FromSlave);
                AddText(FromSlave, DecodedText, true);
                BuildCurrentLayout();           // Could perhaps optimize and use the minimum affected sequence from either AddText or ClearWholeConsole...but will usually be near zero anyway.
            }

            if (xMsg.Name.LocalName == "Error")
            {
                string DecodedText = Encoding.Unicode.GetString(System.Convert.FromBase64String(xMsg.Value));
                AddText(FromSlave, DecodedText);
            }

            if (xMsg.Name.LocalName == "Debug")
            {
                string msg = xMsg.Value;
                Debug.WriteLine("Debug from '" + FromSlave.HostName + "': ");
                Debug.WriteLine(msg);
            }
        }

#endregion

#region "Line Management"
        
        int PendingVScrollLines = 0;        // Protected by SlaveBuffers lock.  A latent command to scroll generated from other threads (i.e. when new lines are added and ShowingCurrentLine is true).

        public void AddText(Slave FromSlave, string NewText, bool WithoutScroll = false, bool OverwriteCurrentLine = false)
        {
            lock (SlaveBuffers)
            {
                StringBuilder CurrentLine = SlaveBuffers[FromSlave].CurrentLine;
                if (OverwriteCurrentLine) CurrentLine.Clear();
                for (int ii = 0; ii < NewText.Length; ii++)
                {
                    CurrentLine.Append(NewText[ii]);
                    if (NewText[ii] == '\n')
                    {
                        int InitialLayoutSize = CurrentLayout.Count;
                        OnLine(FromSlave, CurrentLine);

                        // Create an autoscroll effect when we are showing the current line and new lines come in.
                        if (ShowingCurrentLine && !WithoutScroll) PendingVScrollLines += (CurrentLayout.Count - InitialLayoutSize);
                    }
                }
            }
            Invalidate();
        }

        public void UpdateCurrentLine(Slave FromSlave, string CurrentConsoleLine)
        {
            if (CurrentConsoleLine.Contains("\n")) throw new Exception("Cannot call UpdateCurrentLine() with a completed line, only a line that contains no newlines.");
            lock (SlaveBuffers)
            {
                SlaveBuffers[FromSlave].CurrentLine.Clear();
                SlaveBuffers[FromSlave].CurrentLine.Append(CurrentConsoleLine);
            }
            Invalidate();
        }

        public void ClearWholeConsole(Slave FromSlave)
        {
            lock (SlaveBuffers)
            {
                SlaveBuffer IntoBuffer = SlaveBuffers[FromSlave];
                IntoBuffer.CurrentLine.Clear();

                // First pass- disconnect all siblings connected to this slave buffer.                
                for (int ii = 0; ii < IntoBuffer.Lines.Count; ii++)
                {
                    if (IntoBuffer.Lines[ii].Siblings.Count > 0)
                    {
                        foreach (Line ln in IntoBuffer.Lines[ii].Siblings)
                            ln.Siblings.Remove(IntoBuffer.Lines[ii]);
                    }
                }

                // Second- actually remove all lines
                IntoBuffer.Lines.Clear();

                // We'll need to be able to index against SlaveBuffers...
                List<SlaveBuffer> OtherBuffers = new List<SlaveBuffer>();
                foreach (var kvp in SlaveBuffers)
                {
                    if (kvp.Value == IntoBuffer) continue;
                    OtherBuffers.Add(kvp.Value);
                }

                // Third, second pass- renumber display sequence where any gaps exist up to MaxDisplaySequence, in case we've now created
                // holes in the DisplaySequence sequence.
                int[] Index = new int[OtherBuffers.Count];
                for (int ii = 0; ii < Index.Length; ii++) Index[ii] = 0;
                int Delta = 0;
                for (int iSeq = 0; ;)
                {
                    // See if this DisplaySequence still exists anywhere...
                    bool Found = false;
                    bool End = true;
                    for (int iSB = 0; iSB < OtherBuffers.Count; iSB++)
                    {
                        for (; Index[iSB] < OtherBuffers[iSB].Lines.Count;)
                        {
                            End = false;
                            Line ln = OtherBuffers[iSB].Lines[Index[iSB]];
                            if (ln.DisplaySequence + Delta > iSeq) break;           // Avoid modifying the DisplaySequence of any line more than once by predicting here if we are ahead of our current iSeq.
                            ln.DisplaySequence += Delta;
                            if (ln.DisplaySequence == iSeq) { Found = true; break; }
                            Index[iSB]++;
                        }
                        // We cannot break early if Found because we are also renumbering as we go (applying Delta).
                    }
                    if (End) break;
                    if (Found) { iSeq++; continue; }

                    // This iSeq was not found in any of the other buffers (and IntoBuffer is now empty).  So we have found a gap.  Renumber every display sequence higher than this one.
                    // To accomplish this, we simply decrement Delta.  This is applied to all lines going forward and renumbers them.  We avoid incrementing iSeq here because we have now
                    // reduced all further DisplaySequence values by one more.
                    Delta--;
                }

                // Lastly, reset this optimization cross-reference index
                foreach (var Buffer in OtherBuffers)
                    if (Buffer.LastCommon.ContainsKey(IntoBuffer)) Buffer.LastCommon[IntoBuffer] = -1;
                IntoBuffer.LastCommon.Clear();
            }
        }

        void ValidateSiblingList(Line NewLine)
        {            
            #if DEBUG            
            // Verify that all siblings are also each others' siblings, but only once.
            for (int jj = 0; jj < NewLine.Siblings.Count; jj++)
            {
                Line jSibling = NewLine.Siblings[jj];
                Debug.Assert(jSibling.Siblings.Count == NewLine.Siblings.Count);        // Assert that jSibling has the same number of siblings as NewLine.
                int[] Matches = new int[NewLine.Siblings.Count];
                for (int nn = 0; nn < Matches.Length; nn++) Matches[nn] = 0;
                for (int kk = 0; kk < jSibling.Siblings.Count; kk++)
                {
                    if (jSibling.Siblings[kk] == NewLine) Matches[jj]++;
                    else
                    {
                        for (int nn = 0; nn < NewLine.Siblings.Count; nn++)
                            if (jSibling.Siblings[kk] == NewLine.Siblings[nn]) Matches[nn]++;
                    }
                }
                for (int nn = 0; nn < Matches.Length; nn++)
                    Debug.Assert(Matches[nn] == 1);             // Assertion: One or more of the siblings has a different siblings list (aside from exchanging NewLine for jSibling).
            }
            // Verify that there is no more than one line per SlaveBuffer in any sibling listing.
            var SBCount = new Dictionary<string, int>();            
            for (int jj = 0; jj < NewLine.Siblings.Count; jj++) SBCount[NewLine.Siblings[jj].Container.Slave.HostName] = 0;
            SBCount[NewLine.Container.Slave.HostName] = 1;
            for (int jj = 0; jj < NewLine.Siblings.Count; jj++) SBCount[NewLine.Siblings[jj].Container.Slave.HostName]++;
            foreach (string Key in SBCount.Keys)
            {
                // Assertion: there should be no more than 1 instance of a SlaveBuffer making an appearance in the sibling list.
                // If this triggers, we are seeing 2 lines within the same SlaveBuffer end up sibling-upped.
                Debug.Assert(SBCount[Key] == 1);                
            }
            // Verify that all siblings share the same DisplaySequence number.
            for (int jj = 1; jj < NewLine.Siblings.Count; jj++)
            {
                Debug.Assert(NewLine.Siblings[jj].DisplaySequence == NewLine.Siblings[0].DisplaySequence);
            }

            // Verify that for all sibling lines, the LastCommon index is at least as high as this pairing for the pair of SlaveBuffers.
            List<Line> AllLines = new List<Line>();
            AllLines.Add(NewLine);
            foreach (Line lnSibling in NewLine.Siblings) AllLines.Add(lnSibling);
            for (int ii = 0; ii < AllLines.Count; ii++)
            {
                SlaveBuffer iSB = AllLines[ii].Container;
                int iLineIndex = iSB.Lines.IndexOf(AllLines[ii]);
                for (int jj = ii + 1; jj < AllLines.Count; jj++)
                {
                    SlaveBuffer jSB = AllLines[jj].Container;
                    int jLineIndex = jSB.Lines.IndexOf(AllLines[jj]);

                    // Assertions: if either of these trigger, then the LastCommon mapping is not accurate between these two SlaveBuffers.  We know it's not accurate because we're
                    // looking at a sibling list and checking the LastCommon values between the sibling containers, and the LastCommon indices aren't at least as high as the sibling
                    // lines we're looking at right now.
                    Debug.Assert(iSB.LastCommon[jSB] >= iLineIndex);
                    Debug.Assert(jSB.LastCommon[iSB] >= jLineIndex);
                }
            }
            #endif
        }

        void DebugAllSlaveBuffers()
        {
            foreach (var kvp in SlaveBuffers)
            {
                SlaveBuffer SlaveBuffer = kvp.Value;
                Debug.WriteLine("Complete SlaveBuffer for '" + SlaveBuffer.Slave.HostName + "': ----------------------");
                for (int iLine = 0; iLine < SlaveBuffer.Lines.Count; iLine++)
                {
                    Line ln = SlaveBuffer.Lines[iLine];
                    Debug.Write("<" + ln.DisplaySequence + "> ");
                    if (ln.Siblings.Count > 0)
                    {
                        foreach (Line lnSibling in ln.Siblings) Debug.Write("<" + lnSibling.Container.Slave.HostName + ":" + lnSibling.DisplaySequence + ">");
                        Debug.Write(" ");
                    }
                    Debug.Write(ln.Text);
                }
                foreach (var kvpLC in SlaveBuffer.LastCommon)
                {
                    Debug.WriteLine(" * LastCommon -> [" + kvpLC.Value + "] for " + kvpLC.Key.Slave.HostName);
                }
            }
            Debug.WriteLine("---------------------------------------------");
        }

        bool GetLastLoopAux(SlaveBuffer Origin, SlaveBuffer Scan, ref int ScanIndex, ref List<SlaveBuffer> Considered)
        {            
            if (Scan.LastCommon.ContainsKey(Origin) && Scan.LastCommon[Origin] >= ScanIndex) { ScanIndex = Scan.LastCommon[Origin]; return true; }
            Considered.Add(Scan);

            for (int iLine = ScanIndex; iLine < Scan.Lines.Count; iLine++)
            {
                foreach (Line lnSibling in Scan.Lines[iLine].Siblings)
                {
                    if (!Considered.Contains(lnSibling.Container))
                    {
                        SlaveBuffer jScan = lnSibling.Container;
                        int jIndex = jScan.LastCommon[Scan];
                        if (jIndex < 0)
                        {
                            DebugAllSlaveBuffers();
                        }
                        Debug.Assert(jIndex >= 0);      // Assertion: since we have found a sibling connection between Scan and jScan, having a LastCommon of -1 should be impossible.
                        if (GetLastLoopAux(Origin, lnSibling.Container, ref jIndex, ref Considered))
                        {
                            // We found a loop through recursion.
                            ScanIndex = iLine;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Loops formed between multiple SlaveBuffers can be a problem because they can form an impossible display sequence.  We check for loops as a last step
        /// before forming a sibling bond between two lines.  GetLastLoop() performs a hypothetical search for a loop between Origin (where the new line is coming
        /// in on) and Scan, a potential sibling SlaveBuffer, beginning at index ScanIndex.  A loop is defined as any connection back to Origin occurring at an
        /// index past ScanIndex in the Scan buffer.  The search necessarily expands to include any indirect loops made through additional sibling connections
        /// into other SlaveBuffers.
        /// 
        /// Precondition: GetLastLoop() assumes no actual loops exist.  It instead only answers the question, "would this form a new loop"?
        /// </summary>        
        /// <param name="Origin">The SlaveBuffer to watch for a loop formation back to.</param>
        /// <param name="Scan">The SlaveBuffer to scan for any connections (direct or indirect) into Origin.</param>
        /// <param name="ScanIndex">The first line index in Scan where loops should be identified and reported.  If true is returned, this index is updated
        /// to the line index in Scan where the loop happens.</param>
        /// <returns>False if no loop was found at ScanIndex or later.  True if a loop exists, and in that case the ScanIndex is updated to provide the line index
        /// in Scan where the loop forms at or from.  No further sibling connections between Origin and Scan will be possible anywhere at or lower than this
        /// retrieved ScanIndex, and so it can be updated as the new "LastCommon" value between the two buffers in order to save search time next time.</returns>
        bool GetLastLoop(SlaveBuffer Origin, SlaveBuffer Scan, ref int ScanIndex)
        {
            List<SlaveBuffer> Considered = new List<SlaveBuffer>();
            return GetLastLoopAux(Origin, Scan, ref ScanIndex, ref Considered);
        }

        public void OnLine(Slave FromSlave, StringBuilder CurrentLine)
        {
            SlaveBuffer IntoBuffer = SlaveBuffers[FromSlave];
            Line NewLine = new Line(IntoBuffer, CurrentLine.ToString());
            CurrentLine.Clear();
            int iNewLine = IntoBuffer.Lines.Count;            

            // Assign a default DisplaySequence, which will stick if there are no siblings found.
            if (IntoBuffer.Lines.Count > 0)
                NewLine.DisplaySequence = IntoBuffer.Lines[IntoBuffer.Lines.Count - 1].DisplaySequence + 1;
            else            
                NewLine.DisplaySequence = IntoBuffer.SequenceStart;

            IntoBuffer.Lines.Add(NewLine);

            Debug.WriteLine("Adding line at index " + iNewLine + " from host '" + NewLine.Container.Slave.HostName + "': " + NewLine.Text);

            // We must determine siblings for the new line as we add it to the slave buffers.

            // A sequence/search to consider:
            //  A hello
            //  B no
            //  C yes
            //  C no -> sibling B no
            //  A want
            //  A no -> sibling B, C no        

            // Should form into a display as:
            //  A hello
            //  A want
            //  ABC no
            //  C yes 
            // Which requires merging the ABC no, but also requires migrating the A want above where it landing temporally (it only landed there temporally in terms of arriving at the parallel terminal, not necessarily
            // in actual time at the slave).

            // Now add more coming in from the slaves:
            //  B hello
            //  C no
            // We want B hello to not match up with A because there is a common line in between.  Too late to merge them, this "hello" is not that "hello" because one went as "hello-no" and the other as "no-hello".
            // We also want to be careful that C no doesn't merge up with ABC no, having the same text but already containing that line from C.

            // All searching should terminate at the last common line.  But what of:
            //  A hello
            //  BC no
            //  C yes
            //  A yes
            //  A no
            // Now there is no perfect answer because we could either pair A yes with C yes or pair A no with BC no.  We cannot pair both because A went as "hello-yes-no" and C went as "no-yes".  Sequential is simplest,
            // so we choose to display this as:
            //  A hello
            //  BC no
            //  AC yes
            //  A no
            // The above example also illustrates complexity with how far we search up the list.  In the previous examples we were able to search only up to the last line that had siblings because they were all included,
            // but what if a node D also exists and after the above we receive a "D hello" from it?  We can't stop at the last sibling of A, the AC yes, because there is a better fit at A hello for node D.  Consider also
            // the case where we receive a "D no".  There are two "no" lines to match to and nothing locking the sequence of D.  There is again no perfect answer, but we go for the "BC no" to match with it.  This illustrates
            // that we can safely stop at the last time that D had a sibling, because that locks the order for any new message from D.                          

            // First we identify siblings (identical lines from other slaves), but don't act yet.

            // Interestingly, although SlaveBuffers is a Dictionary and order may not be guaranteed, we don't care.  We are forming a list in NewLine.Siblings that will impart an order corresponding to
            // the order of iFromLine.
            List<int> iFromLine = new List<int>();            

            foreach (var SlaveAndBuffer in SlaveBuffers)
            {
                SlaveBuffer Buffer = SlaveAndBuffer.Value;
                if (Buffer == IntoBuffer) continue;

                // We need to go through each slave buffer from the last common line with "IntoBuffer" and until we reach the end.  We've kept track of our last common line between the two,
                // unless is the first line from one of them and we haven't set the index yet, in which case we setup as if the new slave joined late.  This is a speed optimization, but also
                // functional in enforcing sequencing to common line orders.

                bool FoundSibling = false;
                //if (!Buffer.LastCommon.ContainsKey(IntoBuffer)) Buffer.LastCommon.Add(IntoBuffer, Math.Max(0, IntoBuffer.Lines.Count - 1));
                //if (!IntoBuffer.LastCommon.ContainsKey(Buffer)) IntoBuffer.LastCommon.Add(Buffer, Math.Max(0, Buffer.Lines.Count - 1));
                if (!Buffer.LastCommon.ContainsKey(IntoBuffer)) Buffer.LastCommon.Add(IntoBuffer, -1);
                if (!IntoBuffer.LastCommon.ContainsKey(Buffer)) IntoBuffer.LastCommon.Add(Buffer, -1);
                int iLine = Buffer.LastCommon[IntoBuffer] + 1;
                for (; iLine < Buffer.Lines.Count; iLine++)
                {
                    if (Buffer.Lines[iLine].TextHash != NewLine.TextHash) continue;
                    if (Buffer.Lines[iLine].Text == NewLine.Text)
                    {
                        // Special case: we want to be hesitant to sibling-up empty lines that are nothing but a newline.  Because we impart sequencing requirements, and aren't doing anything so smart as "looking for a
                        // better match" later, we want to avoid locking in a sequence based on a newline.  Really this could use some thought for any repeating patterns in the stream (it's analogous to trying to line
                        // up two sine waves, if you just look for matching values you might meet up at zero crossings/newlines but examining period might lead to a better match overall).
                        // For now, we avoid mating up newlines unless we are already lock-step between the two.  In other words, newlines can continue a sync between two or more hosts but cannot initiate it.
                        if (NewLine.Text == "\n" || NewLine.Text == "\r\n" || NewLine.Text == "\n\r" || NewLine.Text == "")
                        {
                            // An exemption from the exemption: if the newline is the first line in either buffer, it can be used for sync because there is no sync established.
                            if (iNewLine != 0 && iLine != 0)
                            {
                                // So we don't get the first line exemption and our new and test lines are nothing but a \n.  The last thing to check is whether a previous sync exists, because if it does
                                // we are allowed to include an empty line in the sync.  Otherwise, move along in our search.
                                if (Buffer.LastCommon[IntoBuffer] >= 0 && Buffer.LastCommon[IntoBuffer] != iLine - 1) continue;
                                if (IntoBuffer.LastCommon[Buffer] >= 0 && IntoBuffer.LastCommon[Buffer] != iNewLine - 1) continue;
                            }
                        }

                        // Block: we disallow connecting up to a line that is already connected to a different line in this (IntoBuffer) buffer.
                        bool Disallow = false;
                        foreach (Line lnSibling in Buffer.Lines[iLine].Siblings)
                            if (lnSibling.Container == IntoBuffer) { Disallow = true; break; }
                        if (Disallow) continue;

                        // The final and most expensive consideration: we can't create a circular loop between multiple SlaveBuffers that forms an impossible order.  For example, let's say buffer A
                        // has a sibling to buffer B at display sequence of 90.  Buffer C has a sibling pair with buffer B at display sequence 45.  We might then try to connect a sibling from buffer A
                        // to buffer C at a lower display sequence than 45, say 30.  This would be an impossible order because buffer A has a sibling pair first at display sequence 90 but then at a sibling
                        // pair at 30, and the sibling pairing at 45 cannot be incremented without altering (affecting) the connection to buffer B's 90 that buffer A connected to earlier.  As a diagram:
                        //
                        //      A           B           C
                        //                              [30]
                        //                  [45]\      /
                        //      <B:90> \         -----/-<B:45>
                        //      <C:30> -\-------------
                        //               \--[90]
                        //
                        // The above diagram becomes impossible at the addition of the <C:30> connection in buffer A.  Adding this line would require increasing [30] and above in C.  Increasing display sequences
                        // in C past the [30] would increase the sibling pair at [45] to B.  Increasing the sibling pair from 45+ in B would increase the [90] in B.  Increasing the [90] in B would increase the
                        // sibling pairing to A and because this is *earlier* in index than the <C:30> connection, a loop would form.
                        //
                        // So we must prevent loops.  This could become computationally expensive, but we can increment LastCommon to keep it manageable.
                        int LastCommon = iLine;
                        if (GetLastLoop(IntoBuffer, Buffer, ref LastCommon))
                        {                            
                            Debug.Assert(LastCommon >= iLine);          // Assertion: should only be possible to advance.  If this fires, then we could get stuck in an infinite loop searching.
                            Buffer.LastCommon[IntoBuffer] = LastCommon;
                            iLine = LastCommon + 1;
                            continue;
                        }

                        // Alright, we're a go on adding this sibling.
                        Debug.Assert(NewLine.Siblings.Count == 0);
                        NewLine.Siblings.Add(Buffer.Lines[iLine]);
                        foreach (Line ln in Buffer.Lines[iLine].Siblings) NewLine.Siblings.Add(ln);

                        foreach (Line ln in NewLine.Siblings)
                        {
                            int jLine = ln.Container.Lines.IndexOf(ln);
                            Debug.Assert(jLine >= 0);           // Assertion: since the line is contained in Lines, it should be found here...
                            ln.Siblings.Add(NewLine);
                            iFromLine.Add(jLine);
                            ln.Container.LastCommon[IntoBuffer] = jLine;
                            IntoBuffer.LastCommon[ln.Container] = iNewLine;
                        }
                        
                        FoundSibling = true;
                        break;
                    }
                }
                if (FoundSibling) break;
            }            

            if (NewLine.Siblings.Count == 0) {
                Debug.WriteLine("\tNo siblings.");
                BuildCurrentLayout(NewLine.DisplaySequence);
                return;
            }

            // Now we've found all the siblings, but we need to decide on a display sequence.  This can go 2 ways:
            //  1. The other buffers all have more text than this one, so their DisplaySequence number is higher.  This one is easy,
            //      we just skip some display lines in this buffer by upping our DisplaySequence to match.  We don't even need to
            //      rebuild CurrentLayout because this line will display identically to others and only the markers will change.

            ValidateSiblingList(NewLine);

            int BaseDelta = NewLine.DisplaySequence - NewLine.Siblings[0].DisplaySequence;
            //Debug.WriteLine("\tBaseDelta = " + BaseDelta);
            if (BaseDelta <= 0) {
                NewLine.DisplaySequence = NewLine.Siblings[0].DisplaySequence;
                //Debug.WriteLine("[" + NewLine.DisplaySequence + "] has found " + NewLine.Siblings.Count + " siblings.");
                Debug.WriteLine("\tFound " + NewLine.Siblings.Count + " siblings.  Their DisplaySequence number is already higher, so absorbing their DisplaySequence of " + NewLine.DisplaySequence);
                BuildCurrentLayout(NewLine.DisplaySequence);
                return;
            }

            Debug.WriteLine("\n\n\n");
            DebugAllSlaveBuffers();
            Debug.WriteLine("\tApplying a delta of " + BaseDelta + " to DisplaySequences in the sibling list.");

            //  2. This buffer has more text ahead of the new common line.  This is more work, we need to insert some empty display
            //      lines in the other buffers by upping their DisplaySequence.  This can have a cascade effect if there were
            //      other siblings later in the buffer.  We can handle cascades by adding them to the affected list with the index
            //      where the cascade starts, if they're not being incremented yet.
            //            

            int MinimumSequenceAffected = NewLine.DisplaySequence - BaseDelta;            

            List<int> Delta = new List<int>();
            List<SlaveBuffer> Affected = new List<SlaveBuffer>();
            for (int ii=0; ii < NewLine.Siblings.Count; ii++)
            {
                Affected.Add(NewLine.Siblings[ii].Container);
                Delta.Add(BaseDelta);
                // iFromLine should have the same format as these two and can use the same indexing.
                Debug.WriteLine("\tAffected '" + NewLine.Siblings[ii].Container.Slave.HostName + "' with delta = " + BaseDelta + " from line index " + iFromLine[ii]);
            }

            for (int ii=0; ii < Affected.Count; ii++)
            {
                for (int jLine = iFromLine[ii]; jLine < Affected[ii].Lines.Count; jLine++)
                {
                    Affected[ii].Lines[jLine].DisplaySequence += Delta[ii];

                    // Logically, if each NewDelta is equal to BaseDelta, then MinimumSequenceAffected should be as calculated above and should hold.  Only if there were variable deltas going on
                    // here should this be false.
                    Debug.Assert(Affected[ii].Lines[jLine].DisplaySequence >= MinimumSequenceAffected);

                    if (Affected[ii].Lines[jLine].Siblings.Count > 0)
                    {
                        foreach (Line ln in Affected[ii].Lines[jLine].Siblings)
                        {
                            if (!Affected.Contains(ln.Container) && ln.Container != NewLine.Container)
                            {
                                Affected.Add(ln.Container);
                                iFromLine.Add(ln.Container.Lines.IndexOf(ln));
                                int NewDelta = Affected[ii].Lines[jLine].DisplaySequence - ln.DisplaySequence;
                                Debug.WriteLine("\tAt line [" + ln.DisplaySequence + "] of '" + Affected[ii].Slave.HostName + "': Cascade effect from '" + ln.Container.Slave.HostName + "' with delta = " + NewDelta);
                                System.Diagnostics.Debug.Assert(NewDelta >= 0);     // Less than zero should be logically impossible - we've only added to Affected[ii].Lines[jLine].DisplaySequence, and they should have previously been equal.
                                // I believe, logically, all deltas are going to be the same and I don't need the array.  Verify and simplify.
                                System.Diagnostics.Debug.Assert(NewDelta == BaseDelta);
                                Delta.Add(NewDelta);
                            }
                        }
                    }
                }
            }

#if DEBUG
            // Verify that all DisplaySequence values are common between siblings...
            bool Mismatch = false;
            foreach (var kvp in SlaveBuffers)
            {
                SlaveBuffer SlaveBuffer = kvp.Value;
                for (int iLine = 0; iLine < SlaveBuffer.Lines.Count; iLine++)
                {
                    if (SlaveBuffer.Lines[iLine].Siblings.Count > 0)
                    {
                        foreach (Line ln in SlaveBuffer.Lines[iLine].Siblings)
                        {
                            if (ln.DisplaySequence != SlaveBuffer.Lines[iLine].DisplaySequence)
                            {
                                Debug.WriteLine("Mismatch in DisplaySequence value between siblings.");
                                Debug.WriteLine("\tSlaveBuffer for " + SlaveBuffer.Slave.HostName + " line index " + iLine + " has DisplaySequence of " + SlaveBuffer.Lines[iLine].DisplaySequence);
                                Debug.WriteLine("\tSlaveBuffer for " + ln.Container.Slave.HostName + " has a sibling with DisplaySequence of " + ln.DisplaySequence);
                                Mismatch = true;
                            }
                        }
                    }
                }
            }
            if (Mismatch)
            {                
                Debug.WriteLine("After OnLine(S '" + FromSlave.HostName + "', '" + NewLine.Text + "')");
                Debug.WriteLine("Where NewLine.DisplaySequence = " + NewLine.DisplaySequence + " and line index is " + iNewLine);
                DebugAllSlaveBuffers();
            }
            Debug.Assert(!Mismatch);

            ValidateSiblingList(NewLine);
#endif

            Debug.WriteLine("[" + NewLine.DisplaySequence + "] has found " + NewLine.Siblings.Count + " siblings.");

            BuildCurrentLayout(MinimumSequenceAffected);
        }

        #endregion

        #region "GUI Event Handling"

        //StringBuilder KeysPressed = new StringBuilder();
        List<wb.ConsoleApi.KEY_EVENT_RECORD> KeysPressed = new List<wb.ConsoleApi.KEY_EVENT_RECORD>();

        private void TerminalControl_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!Enabled) return;
            //lock (KeysPressed)
            {
                //if (e.KeyChar == '\r')
                    //KeysPressed.Append('\n');
                //else if (e.KeyChar == '\b')
                    //KeysPressed.Append("\b \b");
                //else
                    //KeysPressed.Append(e.KeyChar);                
            }
            e.Handled = true;
        }

        void OnKey(KeyEventArgs e, bool bDown)
        {
            if (!Enabled) return;
            e.Handled = true;
            lock (KeysPressed)
            {
                if (e.KeyCode == Keys.Capital) return;
                //if (e.KeyCode == Keys.Shift || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey || e.KeyCode == Keys.ShiftKey) return;
                if (e.KeyCode == Keys.ShiftKey) return;
                if (e.KeyCode == Keys.ControlKey) return;
                if (e.KeyCode == Keys.Menu) return;

                wb.ConsoleApi.KEY_EVENT_RECORD KER = new wb.ConsoleApi.KEY_EVENT_RECORD();
                KER.bKeyDown = bDown;
                KER.dwControlKeyState = 0;
                if (e.Alt) KER.dwControlKeyState |= wb.ConsoleApi.ControlKeyState.LEFT_ALT_PRESSED;
                if (e.Control) KER.dwControlKeyState |= wb.ConsoleApi.ControlKeyState.LEFT_CTRL_PRESSED;
                if (e.Shift) KER.dwControlKeyState |= wb.ConsoleApi.ControlKeyState.SHIFT_PRESSED;
                if (IsKeyLocked(Keys.CapsLock)) KER.dwControlKeyState |= wb.ConsoleApi.ControlKeyState.CAPSLOCK_ON;
                if (IsKeyLocked(Keys.NumLock)) KER.dwControlKeyState |= wb.ConsoleApi.ControlKeyState.NUMLOCK_ON;
                if (IsKeyLocked(Keys.Scroll)) KER.dwControlKeyState |= wb.ConsoleApi.ControlKeyState.SCROLLLOCK_ON;

                switch ((Keys)e.KeyValue)
                {
                    case Keys.OemSemicolon: KER.UnicodeChar = ';'; break;
                    case (Keys)187: KER.UnicodeChar = '='; break;       // OemPlus
                    case (Keys)188: KER.UnicodeChar = ','; break;       // OemComma                    
                    case Keys.OemMinus: KER.UnicodeChar = '-'; break;
                    case Keys.OemPeriod: KER.UnicodeChar = '.'; break;
                    case Keys.OemQuestion: KER.UnicodeChar = '/'; break;
                    case (Keys)192: KER.UnicodeChar = '`'; break;       // OemTilde
                    case Keys.OemOpenBrackets: KER.UnicodeChar = '['; break;
                    case Keys.OemPipe: KER.UnicodeChar = '\\'; break;
                    case Keys.OemCloseBrackets: KER.UnicodeChar = ']'; break;
                    case Keys.OemQuotes: KER.UnicodeChar = '\''; break;
                    case Keys.OemBackslash: KER.UnicodeChar = '\\'; break;
                    default: KER.UnicodeChar = (char)e.KeyValue; break;
                }

                if (!e.Shift && !IsKeyLocked(Keys.CapsLock))
                {
                    if (Char.IsLetter(KER.UnicodeChar))
                        KER.UnicodeChar = Char.ToLower((char)e.KeyValue, CultureInfo.CurrentCulture);
                }

                if (e.Shift)
                {
                    switch (KER.UnicodeChar)
                    {
                        case '`': KER.UnicodeChar = '~'; break;
                        case '1': KER.UnicodeChar = '!'; break;
                        case '2': KER.UnicodeChar = '@'; break;
                        case '3': KER.UnicodeChar = '#'; break;
                        case '4': KER.UnicodeChar = '$'; break;
                        case '5': KER.UnicodeChar = '%'; break;
                        case '6': KER.UnicodeChar = '^'; break;
                        case '7': KER.UnicodeChar = '&'; break;
                        case '8': KER.UnicodeChar = '*'; break;
                        case '9': KER.UnicodeChar = '('; break;
                        case '0': KER.UnicodeChar = ')'; break;
                        case '-': KER.UnicodeChar = '_'; break;
                        case '=': KER.UnicodeChar = '+'; break;
                        case '[': KER.UnicodeChar = '{'; break;
                        case ']': KER.UnicodeChar = '}'; break;
                        case '\\': KER.UnicodeChar = '|'; break;                        
                        case ';': KER.UnicodeChar = ':'; break;
                        case '\'': KER.UnicodeChar = '\"'; break;
                        case ',': KER.UnicodeChar = '<'; break;
                        case '.': KER.UnicodeChar = '>'; break;
                        case '/': KER.UnicodeChar = '?'; break;
                    }
                }

                KER.wRepeatCount = 1;
                KER.wVirtualKeyCode = (UInt16)e.KeyCode;
                KER.wVirtualScanCode = (UInt16)e.KeyData;
                KeysPressed.Add(KER);
            }            
        }

        private void TerminalControl_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (!Enabled) return;
            e.IsInputKey = true;
        }

        private void TerminalControl_KeyUp(object sender, KeyEventArgs e)
        {
            OnKey(e, false);
        }

        private void TerminalControl_KeyDown(object sender, KeyEventArgs e)
        {
            OnKey(e, true);
        }

        static List<T> ShallowCopy<T>(List<T> Src)
        {
            List<T> ret = new List<T>();
            ret.AddRange(Src);
            return ret;
        }

        void ShowMessageBox(string Msg)
        {
            bool WasEnabled = Enabled;
            Enabled = false;
            try
            {
                MessageBox.Show(Msg);
            }
            finally
            {
                Application.DoEvents();
                Enabled = WasEnabled;
            }
        }

        bool GUITimer_Tick_InProgress = false;
        private void GUITimer_Tick(object sender, EventArgs e)
        {
            if (GUITimer_Tick_InProgress) return;
            GUITimer_Tick_InProgress = true;

            try
            {
                /** Check for keystrokes that need to be transmitted **/

                //string NewText;
                List<wb.ConsoleApi.KEY_EVENT_RECORD> NewKeys;
                lock (KeysPressed)
                {
                    NewKeys = ShallowCopy(KeysPressed);
                    //NewText = KeysPressed.ToString();
                    //if (NewText.Contains("\n"))         // Buffer until line.
                    KeysPressed.Clear();
                    //else
                    //NewText = "";
                }

                //if (NewText.Length > 0)
                if (NewKeys.Count > 0)
                {
                    bool DisconnectedCheckbox = false;
                    lock (SlaveBuffers)
                    {
                        foreach (var sb in CurrentDisplay)
                        {
                            if (sb.Slave.Connection.CurrentState != Slave_Connection.Connection_State.Connected)
                            {
                                DisconnectedCheckbox = true;
                                break;
                            }
                        }

                        if (!DisconnectedCheckbox)
                        {
                            foreach (var sb in CurrentDisplay)
                            {                                
                                // Potential deadlocks as Slave will lock its SlaveConnection ClientStream (network tunnel) here.  Should be safe in this order, unless changes are made.
                                try
                                {                                    
                                    sb.Slave.Connection.SendConsoleInput(NewKeys);
                                }
                                catch (Exception ex)
                                {
                                    // Another reason this can come up is if we try to send to a terminal that has been disconnected and we know it - if the user has the checkbox for that terminal
                                    // still checked.

                                    // It's possible that a disconnect has happened on another thread and we haven't yet processed the event in the MainForm.  That can happen and SendConsoleInput()
                                    // will throw an exception as we try to access the disconnected stream.  We can check if that's the situation, and if it is we should receive the disconnected
                                    // message momentarily.
                                    if (sb.Slave.IsDisconnected)
                                    {
                                        Debug.WriteLine("Ignoring exception in GUITimer_Tick because it appears that the slave has diconnected and we haven't processed the notification yet.  Exception: " + ex.Message);
                                        continue;
                                    }
                                    throw ex;
                                }
                                //AddText(sb.Slave, NewText);
                            }
                        }
                    }
                    if (DisconnectedCheckbox)
                    {
                        ShowMessageBox("One or most hosts currently selected is not connected.  Cannot send commands or text.");
                    }
                }

                /** Check if the mouse has been hovering **/

                const int Duration = 2000 /*ms*/;
                const int HoverTime = 1000 /*ms*/;
                if (SinceLastTooltip.ElapsedMilliseconds > Duration && SinceMouseMovement.ElapsedMilliseconds > HoverTime)
                {
                    var MousePos = LastMouseLocation;
                    if (MousePos.X >= 0 && MousePos.Y >= 0 && MousePos.X < ClientRectangle.Width && MousePos.Y < ClientRectangle.Height)
                    {
                        bool Hit = false;
                        foreach (HoverObject ho in CurrentHoverObjects)
                        {
                            Rectangle rr = ho.MousePosition;
                            rr.Inflate(2, 2);
                            if (rr.Contains(MousePos))
                            {
                                SinceLastTooltip.Restart();
                                //tip.ToolTipTitle = ho.HoverText;
                                LastTooltipLocation = MousePos;
                                tip.Show(ho.HoverText, this, LastTooltipLocation.X, LastTooltipLocation.Y, Duration);
                                //g.DrawString(ho.HoverText, TextFont, TextBrush, new PointF(50, 800));
                                TooltipShowing = true;
                                Hit = true;
                                break;
                            }
                        }
                        if (!Hit) { LastMouseLocation = new Point(-100, -100); }        // Once the timer expires, avoid spending any significant time processing this on the next timer event.
                    }
                }

                /** Update derived properties **/

                // Lines get added on the other thread, so we can't update the GUI vertical scrollbar there, but can here.            
                lock (SlaveBuffers)
                {
                    /*
                    int MaxDisplaySeq = 0;
                    foreach (SlaveBuffer sb in CurrentDisplay)
                    {
                        if (sb.Lines.Count > 0 && sb.Lines[sb.Lines.Count - 1].DisplaySequence > MaxDisplaySeq)
                            MaxDisplaySeq = sb.Lines[sb.Lines.Count - 1].DisplaySequence;
                    }
                    */
                    VScroll.Minimum = 0;
                    //VScroll.Maximum = Math.Max(MaxDisplaySeq + CurrentDisplay.Count + 5 - LinesFittingClientHeight, 0);
                    VScroll.Maximum = Math.Max(0, CurrentLayout.Count + CurrentDisplay.Count + 4 - LinesFittingClientHeight);
                    //VScrollBar.Maximum = 3000;

                    if (PendingVScrollLines > 0)
                    {
                        int NewValue = VScroll.Value + PendingVScrollLines;
                        PendingVScrollLines = 0;
                        if (NewValue < VScroll.Minimum) NewValue = VScroll.Minimum;
                        if (NewValue > VScroll.Maximum) NewValue = VScroll.Maximum;
                        VScroll.Value = NewValue;
                        Invalidate();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessageBox("Error during GUI timer processing: " + ex.ToString());
            }
            finally
            {
                GUITimer_Tick_InProgress = false;
            }
        }

        Stopwatch SinceMouseMovement = new Stopwatch();
        Point LastMouseLocation;
        Stopwatch SinceLastTooltip = Stopwatch.StartNew();
        ToolTip tip = new ToolTip();
        Point LastTooltipLocation = new Point(int.MaxValue, int.MaxValue);
        bool TooltipShowing = false;

        private void TerminalControl_MouseHover(object sender, EventArgs e)
        {
        }

        private void TerminalControl_MouseMove(object sender, MouseEventArgs e)
        {
            SinceMouseMovement.Restart();
            LastMouseLocation = e.Location;
            if (TooltipShowing)
            {
                // It may not actually be showing because it can also expire from time.  But we haven't manually hidden it yet.
                if (Math.Abs(e.Location.X - LastTooltipLocation.X) > 3 || Math.Abs(e.Location.Y - LastTooltipLocation.Y) > 3)
                {
                    tip.Hide(this);
                    TooltipShowing = false;
                }
            }
        }

        private void TerminalControl_MouseLeave(object sender, EventArgs e)
        {
            LastMouseLocation = new Point(-100, -100);
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (Control.ModifierKeys == Keys.Control)
            {
                // Change font size

                float delta = (e.Delta > 0 ? 2f : -2f);
                float NewSize = TextFontSizeInPoints + delta;
                if (NewSize < 2.0f) NewSize = 2.0f;
                if (NewSize > 30.0f) NewSize = 30.0f;
                TextFontSizeInPoints = NewSize;
            }
            else
            {
                // Vertical scroll

                int Delta = (e.Delta >= 0) ? ((e.Delta > 0) ? 1 : 0) : -1;
                Delta = -Delta;         // For some reason, just seems to be counter-intuitive definitions.
                int NewValue = VScroll.Value + Delta;
                if (NewValue < VScroll.Minimum) NewValue = VScroll.Minimum;
                if (NewValue > VScroll.Maximum) NewValue = VScroll.Maximum;
                VScroll.Value = NewValue;
                OnVScroll(null, null);
            }
        }

        private void OnVScroll(object sender, ScrollEventArgs e)
        {
            Invalidate();
        }

        VScrollBar VScroll;

#endregion
    }
}
