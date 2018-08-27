#if false
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace wb
{
#if false
    public class NativeMethods
    {
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        public struct SECURITY_ATTRIBUTES
        {
            public int length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [DllImport("kernel32.dll")]
        public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
                            bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment,
                            string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
    }
#endif

    /// <summary>
    /// Since the System.Diagnostics.Process class has buffering issues with asynchronous read/write (stdout, stderr, stdin) where it never flushed until a newline, and has a bug in the Peek() operation for synchronous operations
    /// where it blocks, we have to roll our own.  Using Win32 API calls.
    /// </summary>
    public class Win32Process : IDisposable
    {        
        public class ProcessStartInfo
        {
            public bool CreateNoWindow = false;
            public string FileName;
            public string Arguments;
            public bool RedirectStandardInput;
            public bool RedirectStandardOutput;
            public bool RedirectStandardError;
            // UseShellExecute not supported in this implementation.
        }

        public ProcessStartInfo StartInfo;
                
        public bool Start()
        {
            Close();
            ProcessStartInfo startInfo = StartInfo;
            if (startInfo.FileName.Length == 0) throw new InvalidOperationException("File name is missing");
            
            return StartWithCreateProcess(startInfo);
        }

#region "Resource Management and Cleanup"

        /// <summary>
        ///     Opens a long-term handle to the process, with all access.  If a handle exists,
        ///     then it is reused.  If the process has exited, it throws an exception.
        /// </summary>
        SafeProcessHandle OpenProcessHandle()
        {
            return OpenProcessHandle(NativeMethods.PROCESS_ALL_ACCESS);
        }

        SafeProcessHandle OpenProcessHandle(Int32 access)
        {
            if (!haveProcessHandle)
            {
                //Cannot open a new process handle if the object has been disposed, since finalization has been suppressed.            
                if (this.disposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                SetProcessHandle(GetProcessHandle(access));
            }
            return m_processHandle;
        }

        bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            
            if (disposing)
            {
                //Dispose managed and unmanaged resources
                Close();
            }
            this.disposed = true;
        }

        /// <devdoc>
        ///    <para>
        ///       Frees any resources associated with this component.
        ///    </para>
        /// </devdoc>
        public void Close()
        {
            if (Associated)
            {
                if (haveProcessHandle)
                {
                    StopWatchingForExit();
                    Debug.WriteLineIf(processTracing.TraceVerbose, "Process - CloseHandle(process) in Close()");
                    m_processHandle.Close();
                    m_processHandle = null;
                    haveProcessHandle = false;
                }
                haveProcessId = false;
                isRemoteMachine = false;
                machineName = ".";
                raisedOnExited = false;

                //Don't call close on the Readers and writers
                //since they might be referenced by somebody else while the 
                //process is still alive but this method called.
                standardOutput = null;
                standardInput = null;
                standardError = null;

                output = null;
                error = null;


                Refresh();
            }
        }

#endregion

        private bool StartWithCreateProcess(ProcessStartInfo startInfo)
        {
            //Cannot start a new process and store its handle if the object has been disposed, since finalization has been suppressed.            
            if (this.disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            NativeMethods.STARTUPINFO startupInfo = new NativeMethods.STARTUPINFO();
            startupInfo.cb = (uint)Marshal.SizeOf(startupInfo);
            NativeMethods.PROCESS_INFORMATION pi = new NativeMethods.PROCESS_INFORMATION();
            // handles used in parent process
            SafeFileHandle standardInputWritePipeHandle = null;
            SafeFileHandle standardOutputReadPipeHandle = null;
            SafeFileHandle standardErrorReadPipeHandle = null;

            try
            {
                // set up the streams
                if (startInfo.RedirectStandardInput || startInfo.RedirectStandardOutput || startInfo.RedirectStandardError)
                {
                    if (startInfo.RedirectStandardInput)
                    {
                        CreatePipe(out standardInputWritePipeHandle, out startupInfo.hStdInput, true);
                    }
                    else
                    {
                        startupInfo.hStdInput = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE), false);
                    }

                    if (startInfo.RedirectStandardOutput)
                    {
                        CreatePipe(out standardOutputReadPipeHandle, out startupInfo.hStdOutput, false);
                    }
                    else
                    {
                        startupInfo.hStdOutput = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE), false);
                    }

                    if (startInfo.RedirectStandardError)
                    {
                        CreatePipe(out standardErrorReadPipeHandle, out startupInfo.hStdError, false);
                    }
                    else
                    {
                        startupInfo.hStdError = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_ERROR_HANDLE), false);
                    }

                    startupInfo.dwFlags = NativeMethods.STARTF_USESTDHANDLES;
                }

                NativeMethods.CreateProcess("C:\\WINDOWS\\SYSTEM32\\Calc.exe", null, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi);
        }

        // Using synchronous Anonymous pipes for process input/output redirection means we would end up 
        // wasting a worker threadpool thread per pipe instance. Overlapped pipe IO is desirable, since 
        // it will take advantage of the NT IO completion port infrastructure. But we can't really use 
        // Overlapped I/O for process input/output as it would break Console apps (managed Console class 
        // methods such as WriteLine as well as native CRT functions like printf) which are making an
        // assumption that the console standard handles (obtained via GetStdHandle()) are opened
        // for synchronous I/O and hence they can work fine with ReadFile/WriteFile synchrnously!
        private void CreatePipe(out SafeFileHandle parentHandle, out SafeFileHandle childHandle, bool parentInputs)
        {
            NativeMethods.SECURITY_ATTRIBUTES securityAttributesParent = new NativeMethods.SECURITY_ATTRIBUTES();
            securityAttributesParent.bInheritHandle = true;

            SafeFileHandle hTmp = null;
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
    }
}
#endif
