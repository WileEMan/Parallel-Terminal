using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Drawing;

namespace wb
{
    public static class CreateFileApi
    {
        #region "Native API Definitions"
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccess access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
            [MarshalAs(UnmanagedType.U4)] CreationDisposition creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            IntPtr templateFile);

        [Flags]
        public enum FileAccess : uint
        {
            //
            // Standard Section
            //
            AccessSystemSecurity = 0x1000000,   // AccessSystemAcl access type
            MaximumAllowed = 0x2000000,     // MaximumAllowed access type

            Delete = 0x10000,
            ReadControl = 0x20000,
            WriteDAC = 0x40000,
            WriteOwner = 0x80000,
            Synchronize = 0x100000,

            StandardRightsRequired = 0xF0000,
            StandardRightsRead = ReadControl,
            StandardRightsWrite = ReadControl,
            StandardRightsExecute = ReadControl,
            StandardRightsAll = 0x1F0000,
            SpecificRightsAll = 0xFFFF,

            FILE_READ_DATA = 0x0001,        // file & pipe
            FILE_LIST_DIRECTORY = 0x0001,       // directory
            FILE_WRITE_DATA = 0x0002,       // file & pipe
            FILE_ADD_FILE = 0x0002,         // directory
            FILE_APPEND_DATA = 0x0004,      // file
            FILE_ADD_SUBDIRECTORY = 0x0004,     // directory
            FILE_CREATE_PIPE_INSTANCE = 0x0004, // named pipe
            FILE_READ_EA = 0x0008,          // file & directory
            FILE_WRITE_EA = 0x0010,         // file & directory
            FILE_EXECUTE = 0x0020,          // file
            FILE_TRAVERSE = 0x0020,         // directory
            FILE_DELETE_CHILD = 0x0040,     // directory
            FILE_READ_ATTRIBUTES = 0x0080,      // all
            FILE_WRITE_ATTRIBUTES = 0x0100,     // all

            //
            // Generic Section
            //
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,

            SPECIFIC_RIGHTS_ALL = 0x00FFFF,

            FILE_ALL_ACCESS =
                StandardRightsRequired |
                Synchronize |
                0x1FF,

            FILE_GENERIC_READ =
                StandardRightsRead |
                FILE_READ_DATA |
                FILE_READ_ATTRIBUTES |
                FILE_READ_EA |
                Synchronize,

            FILE_GENERIC_WRITE =
                StandardRightsWrite |
                FILE_WRITE_DATA |
                FILE_WRITE_ATTRIBUTES |
                FILE_WRITE_EA |
                FILE_APPEND_DATA |
                Synchronize,

            FILE_GENERIC_EXECUTE =
                StandardRightsExecute |
                FILE_READ_ATTRIBUTES |
                FILE_EXECUTE |
                Synchronize
        }

        [Flags]
        public enum FileShare : uint
        {
            /// <summary>
            /// 
            /// </summary>
            None = 0x00000000,

            /// <summary>
            /// Enables subsequent open operations on an object to request read access.
            /// Otherwise, other processes cannot open the object if they request read access. 
            /// If this flag is not specified, but the object has been opened for read access, the function fails.
            /// </summary>
            Read = 0x00000001,

            /// <summary>
            /// Enables subsequent open operations on an object to request write access. 
            /// Otherwise, other processes cannot open the object if they request write access. 
            /// If this flag is not specified, but the object has been opened for write access, the function fails.        
            /// </summary>
            Write = 0x00000002,

            /// <summary>
            /// Enables subsequent open operations on an object to request delete access. 
            /// Otherwise, other processes cannot open the object if they request delete access.
            /// If this flag is not specified, but the object has been opened for delete access, the function fails.
            /// </summary>
            Delete = 0x00000004
        }

        public enum CreationDisposition : uint
        {
            /// <summary>
            /// Creates a new file. The function fails if a specified file exists.
            /// </summary>
            New = 1,

            /// <summary>
            /// Creates a new file, always. 
            /// If a file exists, the function overwrites the file, clears the existing attributes, combines the specified file attributes, 
            /// and flags with FILE_ATTRIBUTE_ARCHIVE, but does not set the security descriptor that the SECURITY_ATTRIBUTES structure specifies.
            /// </summary>
            CreateAlways = 2,

            /// <summary>
            /// Opens a file. The function fails if the file does not exist. 
            /// </summary>
            OpenExisting = 3,

            /// <summary>
            /// Opens a file, always. 
            /// If a file does not exist, the function creates a file as if dwCreationDisposition is CREATE_NEW.
            /// </summary>
            OpenAlways = 4,

            /// <summary>
            /// Opens a file and truncates it so that its size is 0 (zero) bytes. The function fails if the file does not exist.
            /// The calling process must open the file with the GENERIC_WRITE access right. 
            /// </summary>
            TruncateExisting = 5
        }

        [Flags]
        public enum FileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }
        #endregion
    }

    public static class ConsoleApi
    {
        #region "Native API Definitions"

        #region "Console Functions"

        // http://pinvoke.net/default.aspx/kernel32/AddConsoleAlias.html
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool AddConsoleAlias(
            string Source,
            string Target,
            string ExeName
            );

        // http://pinvoke.net/default.aspx/kernel32/AllocConsole.html
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool AllocConsole();

        // http://pinvoke.net/default.aspx/kernel32/AttachConsole.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(
            uint dwProcessId
            );

        // http://pinvoke.net/default.aspx/kernel32/CreateConsoleScreenBuffer.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateConsoleScreenBuffer(
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwFlags,
            IntPtr lpScreenBufferData
            );

        // http://pinvoke.net/default.aspx/kernel32/FillConsoleOutputAttribute.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FillConsoleOutputAttribute(
            SafeFileHandle hConsoleOutput,
            ushort wAttribute,
            uint nLength,
            COORD dwWriteCoord,
            out uint lpNumberOfAttrsWritten
            );

        // http://pinvoke.net/default.aspx/kernel32/FillConsoleOutputCharacter.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FillConsoleOutputCharacter(
            SafeFileHandle hConsoleOutput,
            char cCharacter,
            uint nLength,
            COORD dwWriteCoord,
            out uint lpNumberOfCharsWritten
            );

        // http://pinvoke.net/default.aspx/kernel32/FlushConsoleInputBuffer.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FlushConsoleInputBuffer(
            IntPtr hConsoleInput
            );

        // http://pinvoke.net/default.aspx/kernel32/FreeConsole.html
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool FreeConsole();

        // http://pinvoke.net/default.aspx/kernel32/GenerateConsoleCtrlEvent.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GenerateConsoleCtrlEvent(
            uint dwCtrlEvent,
            uint dwProcessGroupId
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleAlias.html
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool GetConsoleAlias(
            string Source,
            out StringBuilder TargetBuffer,
            uint TargetBufferLength,
            string ExeName
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleAliases.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleAliases(
            StringBuilder[] lpTargetBuffer,
            uint targetBufferLength,
            string lpExeName
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleAliasesLength.html
        [DllImport("kernel32", SetLastError = true)]
        public static extern uint GetConsoleAliasesLength(
            string ExeName
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleAliasExes.html
        [DllImport("kernel32", SetLastError = true)]
        public static extern uint GetConsoleAliasExes(
            out StringBuilder ExeNameBuffer,
            uint ExeNameBufferLength
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleAliasExesLength.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleAliasExesLength();

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleCP.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleCP();

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleCursorInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleCursorInfo(
            SafeFileHandle hConsoleOutput,
            out CONSOLE_CURSOR_INFO lpConsoleCursorInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleDisplayMode.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleDisplayMode(
            out uint ModeFlags
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleFontSize.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern COORD GetConsoleFontSize(
            SafeFileHandle hConsoleOutput,
            Int32 nFont
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleHistoryInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleHistoryInfo(
            out CONSOLE_HISTORY_INFO ConsoleHistoryInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleMode.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleMode(
            SafeFileHandle hConsoleHandle,
            out uint lpMode
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleOriginalTitle.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleOriginalTitle(
            out StringBuilder ConsoleTitle,
            uint Size
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleOutputCP.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleOutputCP();

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleProcessList.html
        // TODO: Test - what's an out uint[] during interop? This probably isn't quite right, but provides a starting point:
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleProcessList(
            out uint[] ProcessList,
            uint ProcessCount
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleScreenBufferInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleScreenBufferInfo(
            SafeFileHandle hConsoleOutput,
            out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleScreenBufferInfoEx.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleScreenBufferInfoEx(
            SafeFileHandle hConsoleOutput,
            ref CONSOLE_SCREEN_BUFFER_INFO_EX ConsoleScreenBufferInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleSelectionInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetConsoleSelectionInfo(
            CONSOLE_SELECTION_INFO ConsoleSelectionInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleTitle.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetConsoleTitle(
            [Out] StringBuilder lpConsoleTitle,
            uint nSize
            );

        // http://pinvoke.net/default.aspx/kernel32/GetConsoleWindow.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetConsoleWindow();

        // http://pinvoke.net/default.aspx/kernel32/GetCurrentConsoleFont.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetCurrentConsoleFont(
            SafeFileHandle hConsoleOutput,
            bool bMaximumWindow,
            out CONSOLE_FONT_INFO lpConsoleCurrentFont
            );

#if false
        // http://pinvoke.net/default.aspx/kernel32/GetCurrentConsoleFontEx.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetCurrentConsoleFontEx(
            IntPtr ConsoleOutput,
            bool MaximumWindow,
            out CONSOLE_FONT_INFO_EX ConsoleCurrentFont
            );
#endif

        // http://pinvoke.net/default.aspx/kernel32/GetLargestConsoleWindowSize.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern COORD GetLargestConsoleWindowSize(
            SafeFileHandle hConsoleOutput
            );

        // http://pinvoke.net/default.aspx/kernel32/GetNumberOfConsoleInputEvents.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetNumberOfConsoleInputEvents(
            IntPtr hConsoleInput,
            out uint lpcNumberOfEvents
            );

        // http://pinvoke.net/default.aspx/kernel32/GetNumberOfConsoleMouseButtons.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetNumberOfConsoleMouseButtons(
            ref uint lpNumberOfMouseButtons
            );

        // http://pinvoke.net/default.aspx/kernel32/GetStdHandle.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetStdHandle(
            int nStdHandle
            );

        // http://pinvoke.net/default.aspx/kernel32/HandlerRoutine.html
        // Delegate type to be used as the Handler Routine for SCCH
        public delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        // http://pinvoke.net/default.aspx/kernel32/PeekConsoleInput.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool PeekConsoleInput(
            IntPtr hConsoleInput,
            [Out] INPUT_RECORD[] lpBuffer,
            uint nLength,
            out uint lpNumberOfEventsRead
            );

        // http://pinvoke.net/default.aspx/kernel32/ReadConsole.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadConsole(
            IntPtr hConsoleInput,
            [Out] StringBuilder lpBuffer,
            uint nNumberOfCharsToRead,
            out uint lpNumberOfCharsRead,
            IntPtr lpReserved
            );

        // http://pinvoke.net/default.aspx/kernel32/ReadConsoleInput.html
        [DllImport("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
        public static extern bool ReadConsoleInput(
            IntPtr hConsoleInput,
            [Out] INPUT_RECORD[] lpBuffer,
            uint nLength,
            out uint lpNumberOfEventsRead
            );

        // http://pinvoke.net/default.aspx/kernel32/ReadConsoleOutput.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadConsoleOutput(
            SafeFileHandle hConsoleOutput,
            [Out] CHAR_INFO[] lpBuffer,
            COORD dwBufferSize,
            COORD dwBufferCoord,
            ref SMALL_RECT lpReadRegion
            );

        // http://pinvoke.net/default.aspx/kernel32/ReadConsoleOutputAttribute.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadConsoleOutputAttribute(
            SafeFileHandle hConsoleOutput,
            [Out] ushort[] lpAttribute,
            uint nLength,
            COORD dwReadCoord,
            out uint lpNumberOfAttrsRead
            );

        // http://pinvoke.net/default.aspx/kernel32/ReadConsoleOutputCharacter.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadConsoleOutputCharacter(
            SafeFileHandle hConsoleOutput,
            [Out] StringBuilder lpCharacter,
            uint nLength,
            COORD dwReadCoord,
            out uint lpNumberOfCharsRead
            );

        // http://pinvoke.net/default.aspx/kernel32/ScrollConsoleScreenBuffer.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ScrollConsoleScreenBuffer(
            SafeFileHandle hConsoleOutput,
           [In] ref SMALL_RECT lpScrollRectangle,
            IntPtr lpClipRectangle,
           COORD dwDestinationOrigin,
            [In] ref CHAR_INFO lpFill
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleActiveScreenBuffer.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleActiveScreenBuffer(
            SafeFileHandle hConsoleOutput
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleCP.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCP(
            uint wCodePageID
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleCtrlHandler.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(
            ConsoleCtrlDelegate HandlerRoutine,
            bool Add
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleCursorInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCursorInfo(
            SafeFileHandle hConsoleOutput,
            [In] ref CONSOLE_CURSOR_INFO lpConsoleCursorInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleCursorPosition.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCursorPosition(
            SafeFileHandle hConsoleOutput,
           COORD dwCursorPosition
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleDisplayMode.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleDisplayMode(
            IntPtr ConsoleOutput,
            uint Flags,
            out COORD NewScreenBufferDimensions
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleHistoryInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleHistoryInfo(
            CONSOLE_HISTORY_INFO ConsoleHistoryInfo
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleMode.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleMode(
            SafeFileHandle hConsoleHandle,
            uint dwMode
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleOutputCP.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleOutputCP(
            uint wCodePageID
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleScreenBufferInfoEx.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleScreenBufferInfoEx(
            IntPtr ConsoleOutput,
            CONSOLE_SCREEN_BUFFER_INFO_EX ConsoleScreenBufferInfoEx
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleScreenBufferSize.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleScreenBufferSize(
            SafeFileHandle hConsoleOutput,
            COORD dwSize
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleTextAttribute.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleTextAttribute(
            SafeFileHandle hConsoleOutput,
           ushort wAttributes
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleTitle.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleTitle(
            string lpConsoleTitle
            );

        // http://pinvoke.net/default.aspx/kernel32/SetConsoleWindowInfo.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleWindowInfo(
            SafeFileHandle hConsoleOutput,
            bool bAbsolute,
            [In] ref SMALL_RECT lpConsoleWindow
            );

#if false
        // http://pinvoke.net/default.aspx/kernel32/SetCurrentConsoleFontEx.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetCurrentConsoleFontEx(
            SafeFileHandle ConsoleOutput,
            bool MaximumWindow,
            CONSOLE_FONT_INFO_EX ConsoleCurrentFontEx
            );
#endif

        // http://pinvoke.net/default.aspx/kernel32/SetStdHandle.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetStdHandle(
            uint nStdHandle,
            IntPtr hHandle
            );

        // http://pinvoke.net/default.aspx/kernel32/WriteConsole.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteConsole(
            SafeFileHandle hConsoleOutput,
            string lpBuffer,
            uint nNumberOfCharsToWrite,
            out uint lpNumberOfCharsWritten,
            IntPtr lpReserved
            );

        // http://pinvoke.net/default.aspx/kernel32/WriteConsoleInput.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteConsoleInput(
            SafeFileHandle hConsoleInput,
            INPUT_RECORD[] lpBuffer,
            uint nLength,
            out uint lpNumberOfEventsWritten
            );

        // http://pinvoke.net/default.aspx/kernel32/WriteConsoleOutput.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteConsoleOutput(
            SafeFileHandle hConsoleOutput,
            CHAR_INFO[] lpBuffer,
            COORD dwBufferSize,
            COORD dwBufferCoord,
            ref SMALL_RECT lpWriteRegion
            );

        // http://pinvoke.net/default.aspx/kernel32/WriteConsoleOutputAttribute.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteConsoleOutputAttribute(
            SafeFileHandle hConsoleOutput,
            ushort[] lpAttribute,
            uint nLength,
            COORD dwWriteCoord,
            out uint lpNumberOfAttrsWritten
            );

        // http://pinvoke.net/default.aspx/kernel32/WriteConsoleOutputCharacter.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteConsoleOutputCharacter(
            SafeFileHandle hConsoleOutput,
            string lpCharacter,
            uint nLength,
            COORD dwWriteCoord,
            out uint lpNumberOfCharsWritten
            );

#endregion

#region "Type definitions"

        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {

            public short X;
            public short Y;

        }

        public struct SMALL_RECT
        {

            public short Left;
            public short Top;
            public short Right;
            public short Bottom;

        }

        public struct CONSOLE_SCREEN_BUFFER_INFO
        {

            public COORD dwSize;
            public COORD dwCursorPosition;
            public short wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SCREEN_BUFFER_INFO_EX
        {
            public uint cbSize;
            public COORD dwSize;
            public COORD dwCursorPosition;
            public short wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;

            public ushort wPopupAttributes;
            public bool bFullscreenSupported;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public COLORREF[] ColorTable;

            public static CONSOLE_SCREEN_BUFFER_INFO_EX Create()
            {
                return new CONSOLE_SCREEN_BUFFER_INFO_EX { cbSize = 96 };
            }
        }

        //[StructLayout(LayoutKind.Sequential)]
        //struct COLORREF
        //{
        //    public byte R;
        //    public byte G;
        //    public byte B;
        //}

        [StructLayout(LayoutKind.Sequential)]
        public struct COLORREF
        {
            public uint ColorDWORD;

            public COLORREF(System.Drawing.Color color)
            {
                ColorDWORD = (uint)color.R + (((uint)color.G) << 8) + (((uint)color.B) << 16);
            }

            public System.Drawing.Color GetColor()
            {
                return System.Drawing.Color.FromArgb((int)(0x000000FFU & ColorDWORD),
                   (int)(0x0000FF00U & ColorDWORD) >> 8, (int)(0x00FF0000U & ColorDWORD) >> 16);
            }

            public void SetColor(System.Drawing.Color color)
            {
                ColorDWORD = (uint)color.R + (((uint)color.G) << 8) + (((uint)color.B) << 16);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_FONT_INFO
        {
            public int nFont;
            public COORD dwFontSize;
        }

#if false
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct CONSOLE_FONT_INFO_EX
        {
            public uint cbSize;
            public uint nFont;
            public COORD dwFontSize;
            public ushort FontFamily;
            public ushort FontWeight;
            fixed char FaceName[LF_FACESIZE];

            const uint LF_FACESIZE = 32;
        }
#endif

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT_RECORD
        {
            [FieldOffset(0)]
            public ushort EventType;
            [FieldOffset(4)]
            public KEY_EVENT_RECORD KeyEvent;
            [FieldOffset(4)]
            public MOUSE_EVENT_RECORD MouseEvent;
            [FieldOffset(4)]
            public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
            [FieldOffset(4)]
            public MENU_EVENT_RECORD MenuEvent;
            [FieldOffset(4)]
            public FOCUS_EVENT_RECORD FocusEvent;
        };

        public enum EventTypes
        {
            /// <summary>
            /// The Event member contains a FOCUS_EVENT_RECORD structure.These events are used internally and should be ignored.
            /// </summary>
            FOCUS_EVENT = 0x0010,

            /// <summary>
            /// The Event member contains a KEY_EVENT_RECORD structure with information about a keyboard event.
            /// </summary>
            KEY_EVENT = 0x0001,

            /// <summary>
            /// The Event member contains a MENU_EVENT_RECORD structure. These events are used internally and should be ignored.
            /// </summary>
            MENU_EVENT = 0x0008,

            /// <summary>
            /// The Event member contains a MOUSE_EVENT_RECORD structure with information about a mouse movement or button press event.
            /// </summary>
            MOUSE_EVENT = 0x0002,

            /// <summary>
            /// The Event member contains a WINDOW_BUFFER_SIZE_RECORD structure with information about the new size of the console screen buffer.
            /// </summary>
            WINDOW_BUFFER_SIZE_EVENT = 0x0004
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct KEY_EVENT_RECORD
        {
            [FieldOffset(0), MarshalAs(UnmanagedType.Bool)]
            public bool bKeyDown;
            [FieldOffset(4), MarshalAs(UnmanagedType.U2)]
            public ushort wRepeatCount;
            [FieldOffset(6), MarshalAs(UnmanagedType.U2)]
            //public VirtualKeys wVirtualKeyCode;
            public ushort wVirtualKeyCode;
            [FieldOffset(8), MarshalAs(UnmanagedType.U2)]
            public ushort wVirtualScanCode;
            [FieldOffset(10)]
            public char UnicodeChar;
            [FieldOffset(12), MarshalAs(UnmanagedType.U4)]
            public ControlKeyState dwControlKeyState;            
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSE_EVENT_RECORD
        {
            public COORD dwMousePosition;
            public uint dwButtonState;
            public uint dwControlKeyState;
            public uint dwEventFlags;
        }

        public struct WINDOW_BUFFER_SIZE_RECORD
        {
            public COORD dwSize;

            public WINDOW_BUFFER_SIZE_RECORD(short x, short y)
            {
                dwSize = new COORD();
                dwSize.X = x;
                dwSize.Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MENU_EVENT_RECORD
        {
            public uint dwCommandId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FOCUS_EVENT_RECORD
        {
            public uint bSetFocus;
        }

        //CHAR_INFO struct, which was a union in the old days
        // so we want to use LayoutKind.Explicit to mimic it as closely
        // as we can
        [StructLayout(LayoutKind.Explicit)]
        public struct CHAR_INFO
        {
            [FieldOffset(0)]
            public char UnicodeChar;
            [FieldOffset(0)]
            public char AsciiChar;
            [FieldOffset(2)] //2 bytes seems to work properly
            public UInt16 Attributes;
        }

        [Flags]
        public enum ControlKeyState
        {
            RIGHT_ALT_PRESSED = 0x1,
            LEFT_ALT_PRESSED = 0x2,
            RIGHT_CTRL_PRESSED = 0x4,
            LEFT_CTRL_PRESSED = 0x8,
            SHIFT_PRESSED = 0x10,
            NUMLOCK_ON = 0x20,
            SCROLLLOCK_ON = 0x40,
            CAPSLOCK_ON = 0x80,
            ENHANCED_KEY = 0x100
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_CURSOR_INFO
        {
            uint Size;
            bool Visible;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_HISTORY_INFO
        {
            ushort cbSize;
            ushort HistoryBufferSize;
            ushort NumberOfHistoryBuffers;
            uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONSOLE_SELECTION_INFO
        {
            uint Flags;
            COORD SelectionAnchor;
            SMALL_RECT Selection;

            // Flags values:
            const uint CONSOLE_MOUSE_DOWN = 0x0008; // Mouse is down
            const uint CONSOLE_MOUSE_SELECTION = 0x0004; //Selecting with the mouse
            const uint CONSOLE_NO_SELECTION = 0x0000; //No selection
            const uint CONSOLE_SELECTION_IN_PROGRESS = 0x0001; //Selection has begun
            const uint CONSOLE_SELECTION_NOT_EMPTY = 0x0002; //Selection rectangle is not empty
        }

        // Enumerated type for the control messages sent to the handler routine
        public enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }    

#endregion

#endregion
    }
    
    public class ConsoleScreenBuffer : IDisposable
    {
        bool Disposed = false;
        SafeFileHandle hConOut;

        public ConsoleScreenBuffer()
        {
            hConOut = CreateFileApi.CreateFile(
                "CONOUT$",
                CreateFileApi.FileAccess.GenericRead | CreateFileApi.FileAccess.GenericWrite,
                CreateFileApi.FileShare.Write,
                IntPtr.Zero,
                CreateFileApi.CreationDisposition.OpenExisting,
                CreateFileApi.FileAttributes.Normal,
                IntPtr.Zero
                );
            if (hConOut.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                if (hConOut != null) { hConOut.Dispose(); hConOut = null; }
                Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO GetInfo()
        {
            ConsoleApi.CONSOLE_SCREEN_BUFFER_INFO ret;
            if (!ConsoleApi.GetConsoleScreenBufferInfo(hConOut, out ret))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return ret;
        }

        public void SetSize(ConsoleApi.COORD Size)
        {
            if (!ConsoleApi.SetConsoleScreenBufferSize(hConOut, Size))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        
        public void ReadOutput(ConsoleApi.CHAR_INFO[] Buffer, ConsoleApi.COORD BufferSize, ConsoleApi.COORD BufferCoord, ref ConsoleApi.SMALL_RECT Region)
        {
            if (!ConsoleApi.ReadConsoleOutput(hConOut, Buffer, BufferSize, BufferCoord, ref Region))
                throw new Win32Exception(Marshal.GetLastWin32Error());            
        }
    }

    public class ConsoleInput : IDisposable
    {
        bool Disposed = false;
        SafeFileHandle hConIn;

        public ConsoleInput()
        {
            hConIn = CreateFileApi.CreateFile(
                "CONIN$", 
                CreateFileApi.FileAccess.GenericRead | CreateFileApi.FileAccess.GenericWrite,
                CreateFileApi.FileShare.Read, 
                IntPtr.Zero, 
                CreateFileApi.CreationDisposition.OpenExisting,
                CreateFileApi.FileAttributes.Normal, 
                IntPtr.Zero
                );
            if (hConIn.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            uint Mode;
            ConsoleApi.GetConsoleMode(hConIn, out Mode);
            ConsoleApi.SetConsoleMode(hConIn, (uint)(Mode & ~0x0001));      // Disable ENABLE_PROCESSED_INPUT so that Ctrl+C passes through.
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                if (!hConIn.IsClosed) { hConIn.Close(); hConIn.Dispose(); }
                Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public void Write(ConsoleApi.INPUT_RECORD[] Inputs)
        {
            UInt32 nWritten = 0;
            if (!ConsoleApi.WriteConsoleInput(hConIn, Inputs, (uint)Inputs.Length, out nWritten))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (nWritten != Inputs.Length)
                throw new Exception("Not all inputs could be written to console.");
        }
    }
}
