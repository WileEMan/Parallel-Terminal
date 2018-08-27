#if false

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;

namespace wb
{
    public class TrimProcessLauncher
    {
        private static void CreatePipeWithSecurityAttributes(out SafePipeHandle hReadPipe, out SafePipeHandle hWritePipe, NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
        {
            bool ret = NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);
            if (!ret || hReadPipe.IsInvalid || hWritePipe.IsInvalid) throw new Win32Exception();
        }

#if true
        private void CreatePipe(out SafePipeHandle parentHandle, out SafePipeHandle childHandle, bool parentInputs)
        {
            NativeMethods.SECURITY_ATTRIBUTES securityAttributesParent = new NativeMethods.SECURITY_ATTRIBUTES();
            securityAttributesParent.bInheritHandle = true;

            SafePipeHandle hTmp = null;
            try
            {
                if (parentInputs)
                {
                    CreatePipeWithSecurityAttributes(out childHandle, out hTmp, securityAttributesParent, 0);
                }
                else
                {
                    CreatePipeWithSecurityAttributes(out hTmp,
                                                          out childHandle,
                                                          securityAttributesParent,
                                                          0);
                }
                // Duplicate the parent handle to be non-inheritable so that the child process 
                // doesn't have access. This is done for correctness sake, exact reason is unclear.
                // One potential theory is that child process can do something brain dead like 
                // closing the parent end of the pipe and there by getting into a blocking situation
                // as parent will not be draining the pipe at the other end anymore. 
                if (!NativeMethods.DuplicateHandle(new HandleRef(this, NativeMethods.GetCurrentProcess()),
                                                                   hTmp,
                                                                   new HandleRef(this, NativeMethods.GetCurrentProcess()),
                                                                   out parentHandle,
                                                                   0,
                                                                   false,
                                                                   NativeMethods.DUPLICATE_SAME_ACCESS))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                if (hTmp != null && !hTmp.IsInvalid)
                {
                    hTmp.Close();
                }
            }
        }
#else
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(ref SafePipeHandle hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);

        [Flags]
        enum HANDLE_FLAGS : uint
        {
            None = 0,
            INHERIT = 1,
            PROTECT_FROM_CLOSE = 2
        }        

        private void CreatePipe(out SafePipeHandle hReadPipe, out SafePipeHandle hWritePipe)
        {
            NativeMethods.SECURITY_ATTRIBUTES securityAttributesParent = new NativeMethods.SECURITY_ATTRIBUTES();
            securityAttributesParent.nLength = Marshal.SizeOf(securityAttributesParent);
            securityAttributesParent.bInheritHandle = true;
            securityAttributesParent.lpSecurityDescriptor = null;
            
            CreatePipeWithSecurityAttributes(out hReadPipe, out hWritePipe, securityAttributesParent, 0);
        }

        private void SetNoInherit(ref SafePipeHandle hPipe)
        {
            if (!SetHandleInformation(ref hPipe, HANDLE_FLAGS.INHERIT, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
#endif

        [StructLayout(LayoutKind.Sequential)]
        public struct TrimSTARTUPINFO
        {
            public Int32 cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public SafePipeHandle hStdInput;
            public SafePipeHandle hStdOutput;
            public SafePipeHandle hStdError;
        }

        [DllImport("kernel32.dll")]
        static extern bool SetStdHandle(int nStdHandle, SafePipeHandle hHandle);
        const int STD_INPUT_HANDLE = -10;
        const int STD_OUTPUT_HANDLE = -11;
        const int STD_ERROR_HANDLE = -12;

        public void Start()
        {
            StringBuilder commandLine = new StringBuilder();
            //commandLine.Append("cmd.exe");
            commandLine.Append("C:\\Projects\\OpenSource\\Parallel_Terminal\\Debug\\StdOutShow.exe");

            string workingDirectory = Environment.CurrentDirectory;

            int creationFlags = NativeMethods.CREATE_NO_WINDOW;
            IntPtr environmentPtr = IntPtr.Zero;

            NativeMethods.STARTUPINFO startupInfo = new NativeMethods.STARTUPINFO();
            startupInfo.cb = Marshal.SizeOf(startupInfo);
            startupInfo.lpReserved = null;
            startupInfo.cbReserved2 = 0;
            startupInfo.lpReserved2 = IntPtr.Zero;
            startupInfo.lpDesktop = null;
            startupInfo.lpTitle = null;
            startupInfo.dwFlags = 0;
            startupInfo.wShowWindow = 0;

            NativeMethods.PROCESS_INFORMATION processInfo = new NativeMethods.PROCESS_INFORMATION();
            SafeProcessHandle procSH = new SafeProcessHandle();
            SafeThreadHandle threadSH = new SafeThreadHandle();
            bool retVal;
            int errorCode = 0;

            SafePipeHandle standardInputWritePipeHandle = null;
            SafePipeHandle standardOutputReadPipeHandle = null;
            SafePipeHandle standardErrorReadPipeHandle = null;

#if true
            CreatePipe(out standardInputWritePipeHandle, out startupInfo.hStdInput, true);
            CreatePipe(out standardOutputReadPipeHandle, out startupInfo.hStdOutput, false);
            CreatePipe(out standardErrorReadPipeHandle, out startupInfo.hStdError, false);
#else
            CreatePipe(out standardOutputReadPipeHandle, out startupInfo.hStdOutput);
            SetNoInherit(ref standardOutputReadPipeHandle);
            CreatePipe(out standardErrorReadPipeHandle, out startupInfo.hStdError);
            SetNoInherit(ref standardErrorReadPipeHandle);
            CreatePipe(out startupInfo.hStdInput, out standardInputWritePipeHandle);
            SetNoInherit(ref standardInputWritePipeHandle);
#endif

            startupInfo.dwFlags = NativeMethods.STARTF_USESTDHANDLES;            

            SetStdHandle(STD_INPUT_HANDLE, startupInfo.hStdInput);
            SetStdHandle(STD_OUTPUT_HANDLE, startupInfo.hStdOutput);
            SetStdHandle(STD_ERROR_HANDLE, startupInfo.hStdError);            

            retVal = NativeMethods.CreateProcess(
                                        null,               // we don't need this since all the info is in commandLine
                                        commandLine,        // pointer to the command line string
                                        null,               // pointer to process security attributes, we don't need to inheriat the handle
                                        null,               // pointer to thread security attributes
                                        true,               // handle inheritance flag
                                        creationFlags,      // creation flags
                                        environmentPtr,     // pointer to new environment block
                                        workingDirectory,   // pointer to current directory name
                                        ref startupInfo,        // pointer to STARTUPINFO
                                        out processInfo         // pointer to PROCESS_INFORMATION
                                    );
            if (!retVal)
                errorCode = Marshal.GetLastWin32Error();
            if (processInfo.hProcess != (IntPtr)0 && processInfo.hProcess != (IntPtr)NativeMethods.INVALID_HANDLE_VALUE)
                procSH.InitialSetHandle(processInfo.hProcess);
            if (processInfo.hThread != (IntPtr)0 && processInfo.hThread != (IntPtr)NativeMethods.INVALID_HANDLE_VALUE)
                threadSH.InitialSetHandle(processInfo.hThread);
            if (!retVal) throw new Win32Exception(errorCode);

            // Q DisposeOf(startupInfo) ???? doesn't this close the pipes?

            //StandardInput = new AnonymousPipeClientStream(PipeDirection.Out, standardInputWritePipeHandle);
            //StandardOutput = new AnonymousPipeClientStream(PipeDirection.In, standardOutputReadPipeHandle);
            //StandardError = new AnonymousPipeClientStream(PipeDirection.In, standardErrorReadPipeHandle);
            StandardInput = new WritePipe(standardInputWritePipeHandle);
            StandardOutput = new ReadPipe(standardOutputReadPipeHandle);
            StandardError = new ReadPipe(standardErrorReadPipeHandle);

            /*
            Debug.WriteLine("StdOut: " + ReadLine(StandardOutput));
            Debug.WriteLine("StdOut: " + ReadLine(StandardOutput));
            Debug.WriteLine("StdOut: " + ReadLine(StandardOutput));            
            WriteOneAtATime(StandardInput, "echo hello\bworld\n");
            Debug.WriteLine("StdOut: " + ReadLine(StandardOutput));
            Debug.WriteLine("StdOut: " + ReadLine(StandardOutput));
            */
        }

        //public AnonymousPipeClientStream StandardInput;
        //public AnonymousPipeClientStream StandardOutput;
        //public AnonymousPipeClientStream StandardError;

        public WritePipe StandardInput;
        public ReadPipe StandardOutput;
        public ReadPipe StandardError;

        /// <summary>Stops the associated process immediately.</summary>                
        public void Kill()
        {
            //if (!NativeMethods.TerminateProcess(ProcessHandle, -1)) throw new Win32Exception();
        }

        string ReadLine(ReadPipe Pipe)
        {
            byte[] buffer = new byte[1];
            StringBuilder sb = new StringBuilder();
            for (;;)
            {
                int BytesRead = Pipe.NonBlockingRead(buffer, 1);
                if (BytesRead == 1)
                {
                    sb.Append(ConsoleEncoding.GetString(buffer, 0, 1));
                    if (buffer[0] == '\n')
                    {
                        return sb.ToString();
                    }
                }
                Thread.Sleep(1);
            }
        }

        void WriteLine(WritePipe Stream, string line)
        {
            byte[] msg = ConsoleEncoding.GetBytes(line);
            Stream.Write(msg, msg.Length);
        }

        void WriteOneAtATime(WritePipe Stream, string line)
        {
            byte[] msg = ConsoleEncoding.GetBytes(line);
            for (int ii = 0; ii < msg.Length; ii++)
            {
                byte[] ch = new byte[1];
                ch[0] = msg[ii];
                Stream.Write(ch, 1);
                Thread.Sleep(1);
            }
        }

        Encoding ConsoleEncoding = Encoding.GetEncoding(437);
    }
}
#endif
