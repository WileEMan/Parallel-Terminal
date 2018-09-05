using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wb;
using System.Diagnostics;

namespace Core_Library
{
    using COORD = ConsoleApi.COORD;

    public class ConsoleTracker : IDisposable
    {
        public bool PendingReload = true;

        public COORD LastCursorPosition;

        public COORD CurrentCursorPosition
        {
            get {
                ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();
                return info.dwCursorPosition;
            }
        }

        public ConsoleScreenBuffer Console;

        public ConsoleTracker()
        {
            Console = new ConsoleScreenBuffer();
        }

        bool Disposed = false;
        public void Dispose()
        {
            if (!Disposed)
            {
                if (Console != null) { Console.Dispose(); Console = null; }
                Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Lines contains our most current memory of the console, excluding the current line (the one containing the cursor) and
        /// with each line stored after TrimEnd() to remove whitespace from the right.  Newlines are not stored in the Lines strings.
        /// </summary>
        public List<string> Lines = new List<string>();

        /// <summary>
        /// CheckForLineScroll() looks at the console's top line and tries to determine if any lines have vanished as the console has scrolled up.  It is an imperfect
        /// check in that it can be fooled, and a more thorough console validation would be required for certainty, but it is good for identifying some "delta" situations
        /// where the console can be re-synced simply by removing some lines at the top.
        /// </summary>
        /// <param name="Stale">Set to true if CheckForLineScroll() has determined that the console is sufficiently out-of-sync with the tracker that the
        /// line scroll check cannot be performed reliably.</param>
        /// <returns>The number of lines which have vanished from the top of the console.</returns>
        public int CheckForLineScroll(out bool Stale)
        {
            if (PendingReload) { PendingReload = false; Stale = true; return 0; }      // Force first read to always give Stale so we use GetWholeConsole().

            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();
            if (info.dwCursorPosition.Y < LastCursorPosition.Y) { Stale = true; return 0; }
            Stale = false;

            ConsoleApi.CHAR_INFO[] LineBuffer = new ConsoleApi.CHAR_INFO[info.dwSize.X];
            COORD LineBufferSize = new COORD();
            LineBufferSize.X = info.dwSize.X;
            LineBufferSize.Y = 1;
            COORD Origin = new COORD();
            Origin.X = Origin.Y = 0;

            ConsoleApi.SMALL_RECT ConsoleRect = new ConsoleApi.SMALL_RECT();
            ConsoleRect.Left = 0;
            ConsoleRect.Right = (short)(info.dwSize.X - 1);
            ConsoleRect.Top = (short)0;
            ConsoleRect.Bottom = (short)0;

            // Read the top of the console to compare against our current state
            Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);

            StringBuilder sbLine = new StringBuilder();
            for (int xx = 0; xx < info.dwSize.X; xx++) sbLine.Append(LineBuffer[xx].UnicodeChar);
            string FirstLine = sbLine.ToString().TrimEnd();
            sbLine.Clear();

            if (Lines.Count > 0 && Lines[0] != FirstLine)
            {
                for (int yy = 0; yy < Lines.Count; yy++)
                {
                    if (Lines[yy] == FirstLine)
                    {
                        Lines.RemoveRange(0, yy);
                        return yy;
                    }
                }
                Stale = true;
                return 0;
            }

            return 0;
        }

        /// <summary>
        /// ReadNew() is similar to CheckForLineScroll() in that it examines the console and looks for "delta" situations.  As with CheckForLineScroll(), it
        /// can be fooled, but is good for minimizing bandwidth use in most situations.
        /// </summary>
        /// <param name="Stale">Set to true if changes to the console have been detected that cannot be captured by ReadNew(), and therefore the console is
        /// sufficiently out-of-sync as to warrant a complete reload via ReadWholeConsole().</param>
        /// <param name="MaxedMessage">Set to true if the MaxSize constraint triggered.  This would indicate that the ConsoleTracker is not expected to be
        /// fully in sync as yet, except up to the line that we've so far retrieved.</param>
        /// <param name="IncludeCurrentLine"></param>
        /// <param name="MaxSize"></param>
        /// <returns>New content from the console to be appended.</returns>
        public string ReadNew(out bool Stale, out bool MaxedMessage, bool IncludeCurrentLine = false, int MaxSize = int.MaxValue)
        {
            MaxedMessage = false;
            if (PendingReload) { PendingReload = false; Stale = true; return ""; }      // Force first read to always give Stale so we use GetWholeConsole().

            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();            

            if (info.dwCursorPosition.X == LastCursorPosition.X && info.dwCursorPosition.Y == LastCursorPosition.Y) { Stale = false; return ""; }
            if (info.dwCursorPosition.Y < LastCursorPosition.Y) { Stale = true; return ""; }
            Stale = false;
            //System.Diagnostics.Debug.WriteLine("Console Cursor @ (" + info.dwCursorPosition.X.ToString() + "," + info.dwCursorPosition.Y.ToString() + ")");

            int Y1 = LastCursorPosition.Y;
            int Y2 = info.dwCursorPosition.Y;

            ConsoleApi.CHAR_INFO[] LineBuffer = new ConsoleApi.CHAR_INFO[info.dwSize.X];
            COORD LineBufferSize = new COORD();
            LineBufferSize.X = info.dwSize.X;
            LineBufferSize.Y = 1;
            COORD Origin = new COORD();
            Origin.X = Origin.Y = 0;

            ConsoleApi.SMALL_RECT ConsoleRect = new ConsoleApi.SMALL_RECT();
            ConsoleRect.Left = 0;
            ConsoleRect.Right = (short)(info.dwSize.X - 1);
            ConsoleRect.Top = (short)Y1;
            ConsoleRect.Bottom = (short)Y1;

            // Read the first line (which might be the only one changed)
            Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);

            StringBuilder sb = new StringBuilder();
            StringBuilder sbLine = new StringBuilder();

            // Find characters from the previous cursor to EOL
            int X2 = info.dwSize.X;
            if (Y1 == Y2) X2 = info.dwCursorPosition.X;
            int X1 = LastCursorPosition.X;
            if (IncludeCurrentLine) X1 = 0;

            for (int xx = X1; xx < X2; xx++)
            {
                sbLine.Append(LineBuffer[xx].UnicodeChar);
            }
            if (Y1 == Y2)
            {
                LastCursorPosition = info.dwCursorPosition;
                return sbLine.ToString().TrimEnd();
            }
            // Since Y1 doesn't equal Y2, at least one line has been committed to the console.
            sb.Append(sbLine.ToString().TrimEnd());
            Lines.Add(sbLine.ToString().TrimEnd());
            sbLine.Clear();
            sb.AppendLine();
            
            // Now lets work on any additional committed lines, but not the one with the cursor (yet)
            for (int yy = Y1 + 1; yy < Y2; yy++)
            {
                // Read and consume whole line
                ConsoleRect.Left = 0;
                ConsoleRect.Right = (short)(info.dwSize.X - 1);
                ConsoleRect.Top = (short)yy;
                ConsoleRect.Bottom = (short)yy;
                Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);
                for (int xx = 0; xx < info.dwSize.X; xx++)
                    sbLine.Append(LineBuffer[xx].UnicodeChar);
                sb.Append(sbLine.ToString().TrimEnd());
                Lines.Add(sbLine.ToString().TrimEnd());
                sbLine.Clear();
                sb.AppendLine();

                if (sb.Length > MaxSize)
                {
                    MaxedMessage = true;
                    LastCursorPosition.X = 0;
                    LastCursorPosition.Y = (short)(yy + 1);
                    return sb.ToString();
                }
            }

            // Final line (contains the cursor) 
            ConsoleRect.Left = 0;
            ConsoleRect.Right = (short)(info.dwSize.X - 1);
            ConsoleRect.Top = (short)Y2;
            ConsoleRect.Bottom = (short)Y2;
            Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);
            for (int xx = 0; xx < info.dwCursorPosition.X; xx++)
                sbLine.Append(LineBuffer[xx].UnicodeChar);
            sb.Append(sbLine.ToString().TrimEnd());

            LastCursorPosition = info.dwCursorPosition;
            return sb.ToString();
        }

        Random rng = new Random();

        /// <summary>
        /// SpotCheckState() looks at a handful of random lines in the console and compares them to ConsoleTracker's current expectations.  If they match, then we can assume we are ok doing the
        /// usual delta analysis we do.  If something mismatches, then the delta analysis got tripped up- as can happen normally- and we need to refresh the entire console to get back in sync.
        /// This can happen with something as simple as the "cls" command, or a CheckForLineScroll() that incorrectly lined up the top line with the wrong line in our records because the same
        /// line appears more than once, or if the cursor bounces around and makes changes that we don't see.
        /// </summary>
        /// <param name="PartialOnly">If true, we only expect to be sync'd up to the end of the current Lines list.  In this case, we do not need to trigger Stale for any content beyond the
        /// current Lines list.  If false, then we expect our Lines list to be comprehensive (except the current line) and can freely check anywhere.</param>
        /// <param name="Stale"></param>
        public void SpotCheckState(bool PartialOnly, out bool Stale)
        {
            if (PendingReload) { PendingReload = false; Stale = true; return; }      // Force first read to always give Stale so we use GetWholeConsole().

            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();            
            if (info.dwCursorPosition.Y < LastCursorPosition.Y) { Stale = true; return; }

            Stale = false;

            ConsoleApi.CHAR_INFO[] LineBuffer = new ConsoleApi.CHAR_INFO[info.dwSize.X];
            COORD LineBufferSize = new COORD();
            LineBufferSize.X = info.dwSize.X;
            LineBufferSize.Y = 1;
            COORD Origin = new COORD();
            Origin.X = Origin.Y = 0;

            ConsoleApi.SMALL_RECT ConsoleRect = new ConsoleApi.SMALL_RECT();
            ConsoleRect.Left = 0;
            ConsoleRect.Right = (short)(info.dwSize.X - 1);

            for (int ii = 0; ii < 3; ii++)
            {
                int yy = rng.Next() % info.dwSize.Y;
                if (yy == info.dwCursorPosition.Y) continue;            // We don't track the current line in the Lines listing.
                if (PartialOnly && yy >= Lines.Count) continue;         // In this mode, we only compare the part we've so far sync'd.
                
                ConsoleRect.Top = (short)yy;
                ConsoleRect.Bottom = (short)yy;

                // Read the line 
                Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);
                
                StringBuilder sbLine = new StringBuilder();                
                for (int xx = 0; xx < info.dwSize.X; xx++)                
                    sbLine.Append(LineBuffer[xx].UnicodeChar);
                string ConsoleLine = sbLine.ToString().TrimEnd();

                if (yy >= Lines.Count) {
                    if (ConsoleLine.Length == 0) continue;      // Ok, saw a blank line where we expected it - beyond the end of our listing.
                    else { Stale = true; return; }              // Not ok, saw something out where we didn't expect it.
                }
                if (ConsoleLine != Lines[yy]) { Stale = true; return; }     // Found a difference in our spot check.
            }

            return;
        }

        /// <summary>
        /// PeekCurrentLine() retrieves the contents of the line under the cursor, with cursor position as it was during the last call to ReadNew().
        /// Any whitespace at the end of the line is trimmed.
        /// </summary>
        public string PeekCurrentLine()
        {
            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();

            ConsoleApi.CHAR_INFO[] LineBuffer = new ConsoleApi.CHAR_INFO[info.dwSize.X];
            COORD LineBufferSize = new COORD();
            LineBufferSize.X = info.dwSize.X;
            LineBufferSize.Y = 1;
            COORD Origin = new COORD();
            Origin.X = Origin.Y = 0;

            ConsoleApi.SMALL_RECT ConsoleRect = new ConsoleApi.SMALL_RECT();
            ConsoleRect.Left = 0;
            ConsoleRect.Right = (short)(info.dwSize.X - 1);
            ConsoleRect.Top = (short)LastCursorPosition.Y;
            ConsoleRect.Bottom = (short)LastCursorPosition.Y;
            
            Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);

            StringBuilder sb = new StringBuilder();
            for (int xx = 0; xx < info.dwSize.X; xx++)            
                sb.Append(LineBuffer[xx].UnicodeChar);
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Also refreshes Lines.
        /// </summary>
        /// <param name="MaxSize"></param>
        /// <returns></returns>
        public string ReadWholeConsole(int MaxSize = int.MaxValue)
        {
            StringBuilder sb = new StringBuilder();

            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();

            ConsoleApi.CHAR_INFO[] LineBuffer = new ConsoleApi.CHAR_INFO[info.dwSize.X];
            COORD LineBufferSize = new COORD();
            LineBufferSize.X = info.dwSize.X;
            LineBufferSize.Y = 1;
            COORD Origin = new COORD();
            Origin.X = Origin.Y = 0;

            ConsoleApi.SMALL_RECT ConsoleRect = new ConsoleApi.SMALL_RECT();
            ConsoleRect.Left = 0;
            ConsoleRect.Right = (short)(info.dwSize.X - 1);

            Lines.Clear();

            for (int yy = 0; yy <= info.dwCursorPosition.Y; yy++)
            {
                if (sb.Length > MaxSize)
                {
                    LastCursorPosition.X = 0;
                    LastCursorPosition.Y = (short)yy;
                    return sb.ToString();
                }

                ConsoleRect.Top = (short)yy;
                ConsoleRect.Bottom = (short)yy;

                Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);

                StringBuilder sbLine = new StringBuilder();
                for (int xx = 0; xx < info.dwSize.X; xx++)
                    sbLine.Append(LineBuffer[xx].UnicodeChar);
                sb.Append(sbLine.ToString().TrimEnd());
                if (yy < info.dwCursorPosition.Y)
                {
                    Lines.Add(sbLine.ToString().TrimEnd());
                    sb.AppendLine();
                }
            }
            LastCursorPosition = info.dwCursorPosition;
            return sb.ToString();
        }

#if DEBUG
        public void DebugWholeConsole()
        {
            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO info = Console.GetInfo();

            Debug.WriteLine("");
            Debug.WriteLine("------------------------------ Complete Current Console [Cursor = " + info.dwCursorPosition.X.ToString() + "," + info.dwCursorPosition.Y.ToString() + "] ----------------------------------");
            for (int yy = 0; yy <= info.dwCursorPosition.Y; yy++)
            {
                ConsoleApi.CHAR_INFO[] LineBuffer = new ConsoleApi.CHAR_INFO[info.dwSize.X];
                COORD LineBufferSize = new COORD();
                LineBufferSize.X = info.dwSize.X;
                LineBufferSize.Y = 1;
                COORD Origin = new COORD();
                Origin.X = Origin.Y = 0;

                ConsoleApi.SMALL_RECT ConsoleRect = new ConsoleApi.SMALL_RECT();
                ConsoleRect.Left = 0;
                ConsoleRect.Right = (short)(info.dwSize.X - 1);
                ConsoleRect.Top = (short)yy;
                ConsoleRect.Bottom = (short)yy;

                Console.ReadOutput(LineBuffer, LineBufferSize, Origin, ref ConsoleRect);

                StringBuilder sb = new StringBuilder();
                for (int xx = 0; xx < info.dwSize.X; xx++)
                    sb.Append(LineBuffer[xx].UnicodeChar);
                string sLine = sb.ToString().TrimEnd();
                Debug.WriteLine(sLine);
            }
            Debug.WriteLine("------------------------------");
        }
#endif
    }
}
