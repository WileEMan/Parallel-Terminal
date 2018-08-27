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

        public string ReadNew(out bool Stale, bool IncludeCurrentLine = false, int MaxSize = int.MaxValue)
        {
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
            sb.Append(sbLine.ToString().TrimEnd());
            sbLine.Clear();
            sb.AppendLine();
            
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
                sbLine.Clear();
                sb.AppendLine();

                if (sb.Length > MaxSize)
                {
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
                if (yy < info.dwCursorPosition.Y) sb.AppendLine();                
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
