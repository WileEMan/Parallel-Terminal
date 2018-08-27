using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wb
{
    using System.Text;
    using System.Threading;
    using System.Runtime.InteropServices;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution;
    using System.Diagnostics;
    using System;
    using System.Collections;
    using System.IO;
    using Microsoft.Win32;
    using Microsoft.Win32.SafeHandles;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Security;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Runtime.Versioning;
    using System.IO.Pipes;
    using System.Net;

    public static class CredentialValidation
    {
        /// <summary>
        /// Checks the user's credentials using the same priviledges that would be granted by ProcessLauncher if logging in with user profile.  The LogonUser() call is
        /// used, but the logon is immediately closed after successful validation.  If the credentials are invalid or an error occurs, an exception is thrown.
        /// </summary>
        public static void ValidateUser(string Domain, string UserName, string PasswordInClearText)
        {
            IntPtr hLoginToken;
            if (!TokenManagement.LogonUser(UserName, Domain, PasswordInClearText,
                            (int)TokenManagement.LogonType.LOGON32_LOGON_INTERACTIVE, (int)TokenManagement.LogonProvider.LOGON32_PROVIDER_DEFAULT, out hLoginToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }            

            NativeMethods.CloseHandle(hLoginToken);
        }

        public static void ValidateUser(NetworkCredential Credentials)
        {
            ValidateUser(Credentials.Domain, Credentials.UserName, Credentials.Password);
        }
    }

    public class ProcessLauncher : IDisposable
    {
        SafeProcessHandle ProcessHandle = null;
        //bool HaveProcessId = false;
        public int ProcessId;

        IntPtr hLoginToken;
        IntPtr hDupeLoginToken;        

        public WritePipe StandardInput;
        public ReadPipe StandardOutput;
        public ReadPipe StandardError;

        public StringBuilder DebugLog = new StringBuilder();

        public ProcessLauncher()
        {            
        }

        #region "Disposal/Destruction"

        bool disposed = false;
        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    //Dispose managed and unmanaged resources
                    Close();
                }
                this.disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);            
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            if (ProcessHandle != null) { ProcessHandle.Dispose(); ProcessHandle = null; }
            if (hLoginToken != IntPtr.Zero) { NativeMethods.CloseHandle(hLoginToken); hLoginToken = IntPtr.Zero; }
            if (hDupeLoginToken != IntPtr.Zero) { NativeMethods.CloseHandle(hDupeLoginToken); hDupeLoginToken = IntPtr.Zero; }
        }
        #endregion

        public ProcessStartInfo StartInfo = new ProcessStartInfo();

        enum Mechanism
        {
            CreateProcessAsUser,
            CreateProcess
        }

        public bool Start()
        {
            Close();
            ProcessStartInfo startInfo = StartInfo;
            if (startInfo.FileName.Length == 0)
                throw new InvalidOperationException("FileName missing");

            if (startInfo.UseShellExecute) throw new NotSupportedException("UseShellExecute is not supported here.");
            else
            {
                /** 
                 *  As per the CreateProcessAsUser() documentation:
                        Typically, the process that calls the CreateProcessAsUser function must have the SE_INCREASE_QUOTA_NAME privilege and may require the SE_ASSIGNPRIMARYTOKEN_NAME privilege if the token is not assignable.
                        If this function fails with ERROR_PRIVILEGE_NOT_HELD (1314), use the CreateProcessWithLogonW function instead.CreateProcessWithLogonW requires no special privileges, but the specified user account must 
                        be allowed to log on interactively.Generally, it is best to use CreateProcessWithLogonW to create a process with alternate credentials.
                        ...
                        If hToken is a restricted version of the caller's primary token, the SE_ASSIGNPRIMARYTOKEN_NAME privilege is not required. If the necessary privileges are not already enabled, CreateProcessAsUser 
                        enables them for the duration of the call.
                 *
                 *  As per the CreateProcessWithLogonW() documentation:
                 *      You cannot call CreateProcessWithLogonW from a process that is running under the LocalSystem account, because the function uses the logon SID in the caller token, and the token for the LocalSystem 
                 *      account does not contain this SID.
                 *
                 *  Both of these situations come up in this application.  As a simple fix, I wrap the ERROR_PRIVILEGE_NOT_HELD failure and try the alternative.
                */
                if (startInfo.UserName != null && startInfo.UserName.Length > 0 && startInfo.LoadUserProfile)
                {
                    try
                    {
                        return StartAux(startInfo, Mechanism.CreateProcessAsUser);
                    }
                    catch (Win32Exception wex)
                    {
                        const int ERROR_PRIVILEGE_NOT_HELD = 1314;
                        if (wex.NativeErrorCode == ERROR_PRIVILEGE_NOT_HELD)
                        {
                            return StartAux(startInfo, Mechanism.CreateProcess);
                        }
                        else
                        {
                            DebugLog.AppendLine("Re-throwing: " + wex.ToString());
                            throw wex;
                        }
                    }
                }
                else
                    return StartAux(startInfo, Mechanism.CreateProcess);
            }
        }

        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        //private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
        //private static void CreatePipeWithSecurityAttributes(out SafePipeHandle hReadPipe, out SafePipeHandle hWritePipe, NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
        private static void CreatePipeWithSecurityAttributes(out IntPtr hReadPipe, out IntPtr hWritePipe, NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
        {
            bool ret = NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);
            if (!ret) throw new Win32Exception();
        }

        // Using synchronous Anonymous pipes for process input/output redirection means we would end up 
        // wasting a worker threadpool thread per pipe instance. Overlapped pipe IO is desirable, since 
        // it will take advantage of the NT IO completion port infrastructure. But we can't really use 
        // Overlapped I/O for process input/output as it would break Console apps (managed Console class 
        // methods such as WriteLine as well as native CRT functions like printf) which are making an
        // assumption that the console standard handles (obtained via GetStdHandle()) are opened
        // for synchronous I/O and hence they can work fine with ReadFile/WriteFile synchrnously!
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        private void CreatePipe(out IntPtr parentHandle, out IntPtr childHandle, bool parentInputs)
        {
            NativeMethods.SECURITY_ATTRIBUTES securityAttributesParent = new NativeMethods.SECURITY_ATTRIBUTES();
            securityAttributesParent.bInheritHandle = true;

            IntPtr hTmp = IntPtr.Zero;
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
                if (hTmp != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(hTmp);
                    hTmp = IntPtr.Zero;
                }
            }
        }

        private static StringBuilder BuildCommandLine(string executableFileName, string arguments)
        {
            // Construct a StringBuilder with the appropriate command line
            // to pass to CreateProcess.  If the filename isn't already 
            // in quotes, we quote it here.  This prevents some security
            // problems (it specifies exactly which part of the string
            // is the file to execute).
            StringBuilder commandLine = new StringBuilder();
            string fileName = executableFileName.Trim();
            bool fileNameIsQuoted = (fileName.StartsWith("\"", StringComparison.Ordinal) && fileName.EndsWith("\"", StringComparison.Ordinal));
            if (!fileNameIsQuoted)
            {
                commandLine.Append("\"");
            }

            commandLine.Append(fileName);

            if (!fileNameIsQuoted)
            {
                commandLine.Append("\"");
            }

            if (!String.IsNullOrEmpty(arguments))
            {
                commandLine.Append(" ");
                commandLine.Append(arguments);
            }

            return commandLine;
        }

        static object s_CreateProcessLock = new object();                

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        private bool StartAux(ProcessStartInfo startInfo, Mechanism StartMechanism)
        {
            if (startInfo.StandardOutputEncoding != null && !startInfo.RedirectStandardOutput)
            {
                throw new InvalidOperationException("Standard output encoding is only supported with RedirectStandardOutput.");
            }

            if (startInfo.StandardErrorEncoding != null && !startInfo.RedirectStandardError)
            {
                throw new InvalidOperationException("Standard error encoding is only supported with RedirectStandardError.");
            }

            // See knowledge base article Q190351 for an explanation of the following code.  Noteworthy tricky points:
            //    * The handles are duplicated as non-inheritable before they are passed to CreateProcess so
            //      that the child process can not close them
            //    * CreateProcess allows you to redirect all or none of the standard IO handles, so we use
            //      GetStdHandle for the handles that are not being redirected

            //Cannot start a new process and store its handle if the object has been disposed, since finalization has been suppressed.            
            if (this.disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            StringBuilder commandLine = BuildCommandLine(startInfo.FileName, startInfo.Arguments);

            NativeMethods.STARTUPINFO startupInfo = new NativeMethods.STARTUPINFO();
            NativeMethods.Init(startupInfo);

            NativeMethods.PROCESS_INFORMATION processInfo = new NativeMethods.PROCESS_INFORMATION();
            SafeProcessHandle procSH = new SafeProcessHandle();
            SafeThreadHandle threadSH = new SafeThreadHandle();
            bool retVal;
            int errorCode = 0;
            // handles used in parent process
            IntPtr standardInputWritePipeHandle = IntPtr.Zero;
            IntPtr standardOutputReadPipeHandle = IntPtr.Zero;
            IntPtr standardErrorReadPipeHandle = IntPtr.Zero;
            GCHandle environmentHandle = new GCHandle();
            lock (s_CreateProcessLock)
            {
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
                            throw new NotSupportedException("Only fully or unredirected I/O is supported.");
                            //startupInfo.hStdInput = new SafePipeHandle(NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE), false);                            
                            //startupInfo.hStdInput = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE), false);
                        }

                        if (startInfo.RedirectStandardOutput)
                        {
                            CreatePipe(out standardOutputReadPipeHandle, out startupInfo.hStdOutput, false);
                        }
                        else
                        {
                            throw new NotSupportedException("Only fully or unredirected I/O is supported.");
                            //startupInfo.hStdOutput = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE), false);
                        }

                        if (startInfo.RedirectStandardError)
                        {
                            CreatePipe(out standardErrorReadPipeHandle, out startupInfo.hStdError, false);
                        }
                        else
                        {
                            throw new NotSupportedException("Only fully or unredirected I/O is supported.");
                            //startupInfo.hStdError = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_ERROR_HANDLE), false);
                        }

                        startupInfo.dwFlags = NativeMethods.STARTF_USESTDHANDLES;
                    }

                    // set up the creation flags paramater
                    int creationFlags = 0;
                    if (startInfo.CreateNoWindow) creationFlags |= NativeMethods.CREATE_NO_WINDOW;

                    // set up the environment block parameter
                    IntPtr environmentPtr = (IntPtr)0;
#if false
                    if (startInfo.environmentVariables != null)
                    {
                        bool unicode = false;

                        if (ProcessManager.IsNt)
                        {
                            creationFlags |= NativeMethods.CREATE_UNICODE_ENVIRONMENT;
                            unicode = true;
                        }

                        byte[] environmentBytes = EnvironmentBlock.ToByteArray(startInfo.environmentVariables, unicode);
                        environmentHandle = GCHandle.Alloc(environmentBytes, GCHandleType.Pinned);
                        environmentPtr = environmentHandle.AddrOfPinnedObject();
                    }
#endif

                    string workingDirectory = startInfo.WorkingDirectory;
                    if (workingDirectory == string.Empty)
                        workingDirectory = Environment.CurrentDirectory;                    

                    if (StartMechanism == Mechanism.CreateProcessAsUser)
                    {
                        if (startInfo.UserName.Length == 0) throw new Exception("Cannot use CreateProcessAsUser() without credentials.");

                        //if (startInfo.Password != null && startInfo.PasswordInClearText != null)
                        //throw new ArgumentException("Can't start process with both password and clear-text password provided.");                                        
                        if (startInfo.Password != null) throw new ArgumentException("This method only currently supports PasswordInClearText.");

                        if (!TokenManagement.LogonUser(startInfo.UserName, startInfo.Domain, startInfo.PasswordInClearText,
                        (int)TokenManagement.LogonType.LOGON32_LOGON_INTERACTIVE, (int)TokenManagement.LogonProvider.LOGON32_PROVIDER_DEFAULT, out hLoginToken))
                        {
                            errorCode = Marshal.GetLastWin32Error();
                            throw new Win32Exception(errorCode);
                        }

                        DebugLog.AppendLine("Starting DuplicateTokenEx...");
                        TokenManagement.SECURITY_ATTRIBUTES sa = new TokenManagement.SECURITY_ATTRIBUTES();
                        sa.bInheritHandle = false;
                        sa.Length = Marshal.SizeOf(sa);
                        sa.lpSecurityDescriptor = IntPtr.Zero;
                        hDupeLoginToken = IntPtr.Zero;
                        //uint ReqAccess = 0x10000000;
                        uint ReqAccess = TokenManagement.MAXIMUM_ALLOWED;
                        if (!TokenManagement.DuplicateTokenEx(hLoginToken, ReqAccess /*TokenManagement.TOKEN_QUERY | TokenManagement.TOKEN_IMPERSONATE | TokenManagement.TOKEN_DUPLICATE*/, ref sa,
                            (int)TokenManagement.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, (int)TokenManagement.TOKEN_TYPE.TokenPrimary, out hDupeLoginToken))
                            throw new Win32Exception(Marshal.GetLastWin32Error());

                        // From here:  https://stackoverflow.com/questions/38634070/why-loaduserprofile-fails-with-error-5-denied-access-in-this-code-running-in
                        // Basically, we either impersonate and accesss the registry hive or we call LoadUserProfile(), but not both.
                        //DebugLog.AppendLine("Starting impersonation...");
                        //using (WindowsImpersonationContext impersonatedUser = WindowsIdentity.Impersonate(hLoginToken))
                        {
                            DebugLog.AppendLine("Starting LoadUserProfile...");
                            // Need to actually load the user's registry information...
                            TokenManagement.PROFILEINFO profileInfo = new TokenManagement.PROFILEINFO();
                            profileInfo.dwSize = Marshal.SizeOf(profileInfo);
                            profileInfo.lpUserName = startInfo.UserName;
                            profileInfo.dwFlags = 1;
                            if (!TokenManagement.LoadUserProfile(/*hLoginToken*/ hDupeLoginToken, ref profileInfo))
                                throw new Win32Exception(Marshal.GetLastWin32Error());

                            try
                            {
                                using (EnvironmentBlock eb = new EnvironmentBlock(hDupeLoginToken, false))
                                {
                                    byte[] environmentBytes = eb.AsRaw();
                                    creationFlags |= NativeMethods.CREATE_UNICODE_ENVIRONMENT;

                                    environmentHandle = GCHandle.Alloc(environmentBytes, GCHandleType.Pinned);
                                    environmentPtr = environmentHandle.AddrOfPinnedObject();

                                    // The above doesn't seem to be enough to update the environment variables to the new user.  I haven't actually verified the above is having an effect on jw8-1070-c65, so I need to print out some debug lines I think.

                                    RuntimeHelpers.PrepareConstrainedRegions();

                                    // I was getting corrupted memory at the CreateProcessAsUser() call below.  Someone on the internet suggested this code snippet to duplicate the
                                    // token.  But the duplicate also fails.  For now I'm just avoiding using StartProcessWithCreateUser() (by avoiding LoginWithProfile setting).
                                    // Relevant CreateProcessAsUser blurb:  To get a primary token that represents the specified user, call the LogonUser function. Alternatively, you can call the 
                                    //  DuplicateTokenEx function to convert an impersonation token into a primary token. This allows a server application that is impersonating a client to create 
                                    //  a process that has the security context of the client.
                                    // So I don't think we need to duplicate because of the way we are getting the token (LogonUser).
                                    //hDupeLoginToken = TokenManagement.DuplicateToken(hLoginToken);                            

                                    //NativeMethods.SECURITY_ATTRIBUTES ProcessAttributes = new NativeMethods.SECURITY_ATTRIBUTES();
                                    //NativeMethods.SECURITY_ATTRIBUTES ThreadAttributes = new NativeMethods.SECURITY_ATTRIBUTES();                                

                                    commandLine.EnsureCapacity(commandLine.Length * 4 + 256);

                                    retVal = NativeMethods.CreateProcessAsUser(
                                        hLoginToken,
                                        //hDupeLoginToken,
                                        //string.Empty,            // we don't need this since all the info is in commandLine
                                        null,
                                        commandLine.ToString(),
                                        //ref ProcessAttributes,
                                        //ref ThreadAttributes,
                                        IntPtr.Zero, IntPtr.Zero,
                                        true,           // inherit handles
                                        (Int32)creationFlags,
                                        environmentPtr,
                                        workingDirectory,
                                        ref startupInfo,
                                        out processInfo
                                        );
                                    if (!retVal)
                                        errorCode = Marshal.GetLastWin32Error();
                                    if (processInfo.hProcess != (IntPtr)0 && processInfo.hProcess != (IntPtr)NativeMethods.INVALID_HANDLE_VALUE)
                                        procSH.InitialSetHandle(processInfo.hProcess);
                                    if (processInfo.hThread != (IntPtr)0 && processInfo.hThread != (IntPtr)NativeMethods.INVALID_HANDLE_VALUE)
                                        threadSH.InitialSetHandle(processInfo.hThread);

                                    if (!retVal)
                                    {
                                        throw new Win32Exception(errorCode);
                                    }

                                    DebugLog.AppendLine("CreateProcessAsUser() successful.");
                                }
                            }
                            finally
                            {
                                if (!TokenManagement.UnloadUserProfile(hDupeLoginToken, profileInfo.hProfile))
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                            }
                        }
                    }                    
                    else if (startInfo.UserName.Length != 0)
                    {
                        if (startInfo.Password != null && startInfo.PasswordInClearText != null)
                            throw new ArgumentException("Can't start process with both password and clear-text password provided.");

                        NativeMethods.LogonFlags logonFlags = (NativeMethods.LogonFlags)0;
                        if (startInfo.LoadUserProfile)
                        {
                            logonFlags = NativeMethods.LogonFlags.LOGON_WITH_PROFILE;
                        }

                        IntPtr password = IntPtr.Zero;
                        try
                        {
                            if (startInfo.Password != null)
                            {
                                password = Marshal.SecureStringToCoTaskMemUnicode(startInfo.Password);
                            }
                            else if (startInfo.PasswordInClearText != null)
                            {
                                password = Marshal.StringToCoTaskMemUni(startInfo.PasswordInClearText);
                            }
                            else
                            {
                                password = Marshal.StringToCoTaskMemUni(String.Empty);
                            }

                            RuntimeHelpers.PrepareConstrainedRegions();
                            try { }
                            finally
                            {
                                retVal = NativeMethods.CreateProcessWithLogonW(
                                        startInfo.UserName,
                                        startInfo.Domain,
                                        password,
                                        logonFlags,
                                        null,            // we don't need this since all the info is in commandLine
                                        commandLine,
                                        creationFlags,
                                        environmentPtr,
                                        workingDirectory,
                                        ref startupInfo,        // pointer to STARTUPINFO
                                        out processInfo         // pointer to PROCESS_INFORMATION
                                    );
                                if (!retVal)
                                    errorCode = Marshal.GetLastWin32Error();
                                if (processInfo.hProcess != (IntPtr)0 && processInfo.hProcess != (IntPtr)NativeMethods.INVALID_HANDLE_VALUE)
                                    procSH.InitialSetHandle(processInfo.hProcess);
                                if (processInfo.hThread != (IntPtr)0 && processInfo.hThread != (IntPtr)NativeMethods.INVALID_HANDLE_VALUE)
                                    threadSH.InitialSetHandle(processInfo.hThread);
                            }
                            if (!retVal)
                            {
                                throw new Win32Exception(errorCode);
                            }
                        }
                        finally
                        {
                            if (password != IntPtr.Zero)
                            {
                                Marshal.ZeroFreeCoTaskMemUnicode(password);
                            }
                        }
                    }
                    else
                    {
                        RuntimeHelpers.PrepareConstrainedRegions();
                        try { }
                        finally
                        {
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
                        }
                        if (!retVal) throw new Win32Exception(errorCode);
                    }                
                }
                finally
                {
                    // free environment block
                    if (environmentHandle.IsAllocated)
                    {
                        environmentHandle.Free();
                    }

                    //startupInfo.Dispose();
                    NativeMethods.DisposeOf(startupInfo);
                }
            }

            if (startInfo.RedirectStandardInput)
            {
                //StandardInput = new FileStream(standardInputWritePipeHandle, FileAccess.Write, 4096, false);                                
                //StandardInput = new AnonymousPipeClientStream(PipeDirection.Out, standardInputWritePipeHandle);
                //StandardInput = new WritePipe(standardInputWritePipeHandle);
            }
            if (startInfo.RedirectStandardOutput)
            {
                //StandardOutput = new FileStream(standardOutputReadPipeHandle, FileAccess.Read, 4096, false);
                //StandardOutput = new AnonymousPipeClientStream(PipeDirection.In, standardOutputReadPipeHandle);
                //StandardOutput = new ReadPipe(standardOutputReadPipeHandle);
            }
            if (startInfo.RedirectStandardError)
            {
                //StandardError = new FileStream(standardErrorReadPipeHandle, FileAccess.Read, 4096, false);
                //StandardError = new AnonymousPipeClientStream(PipeDirection.In, standardErrorReadPipeHandle);
                //StandardError = new ReadPipe(standardErrorReadPipeHandle);
            }
            
            if (!procSH.IsInvalid)
            {
                this.ProcessHandle = procSH;
                this.ProcessId = processInfo.dwProcessId;
                //this.HaveProcessId = true;
                threadSH.Close();
                return true;
            }

            return false;
        }

        /*
        public void WaitForInputIdle(uint TimeoutMS = INFINITE)
        {
            uint ret = WaitForInputIdle(ProcessHandle, TimeoutMS);
            if (ret == WAIT_OBJECT_0) return;
            if (ret == WAIT_TIMEOUT) throw new TimeoutException();
            //throw new Win32Exception(Marshal.GetLastWin32Error());
            throw new InvalidOperation("WaitForInputIdle() failed.");
        }
        Seems to fail for an unknown reason.
        */

        [DllImport("user32.dll")]
        static extern uint WaitForInputIdle(SafeProcessHandle hProcess, uint dwMilliseconds);
        const uint INFINITE = 0xFFFFFFFF;                   // Infinite timeout
        const uint WAIT_FAILED = ((uint)0xFFFFFFFF);
        const uint WAIT_OBJECT_0 = (uint)((0x00000000L) + 0);
        const uint WAIT_TIMEOUT = 258;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UInt32 WaitForSingleObject(SafeProcessHandle hHandle, UInt32 dwMilliseconds);

        public bool IsStillRunning
        {
            get
            {
                if (ProcessHandle.IsInvalid || ProcessHandle.IsClosed) return false;
                uint ret = WaitForSingleObject(ProcessHandle, 0);
                if (ret == WAIT_TIMEOUT) return true;
                if (ret == WAIT_OBJECT_0) return false;
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>Stops the associated process immediately.</summary>        
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public void Kill()
        {                        
            if (!NativeMethods.TerminateProcess(ProcessHandle, -1))
                throw new Win32Exception();
        }
    }

#region "Safe Handles"

    [SuppressUnmanagedCodeSecurityAttribute]    
    public sealed class SafeThreadHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeThreadHandle() : base(true) { }

        // 0 is an Invalid Handle
        internal SafeThreadHandle(IntPtr handle) : base(true)
        {
            base.SetHandle(handle);
        }

        internal void InitialSetHandle(IntPtr h)
        {
            Debug.Assert(base.IsInvalid, "Safe handle should only be set once");
            base.SetHandle(h);
        }

        [System.Security.SecurityCritical]
        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        override protected bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }    
    

    [SuppressUnmanagedCodeSecurityAttribute]
    public sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal static SafeProcessHandle InvalidHandle = new SafeProcessHandle(IntPtr.Zero);

        // Note that OpenProcess returns 0 on failure

        internal SafeProcessHandle() : base(true) { }

        internal SafeProcessHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public SafeProcessHandle(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(existingHandle);
        }

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern SafeProcessHandle OpenProcess(int access, bool inherit, int processId);

        internal void InitialSetHandle(IntPtr h)
        {
            Debug.Assert(base.IsInvalid, "Safe handle should only be set once");
            base.handle = h;
        }

        override protected bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    [HostProtectionAttribute(MayLeakOnAbort = true)]
    [SuppressUnmanagedCodeSecurityAttribute]
    public class SafeLocalMemHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeLocalMemHandle() : base(true) { }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        internal SafeLocalMemHandle(IntPtr existingHandle, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(existingHandle);
        }

        /*
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern unsafe bool ConvertStringSecurityDescriptorToSecurityDescriptor(string StringSecurityDescriptor, int StringSDRevision, out SafeLocalMemHandle pSecurityDescriptor, IntPtr SecurityDescriptorSize);
        */

        [DllImport(ExternDll.Kernel32)]
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        override protected bool ReleaseHandle()
        {
            return LocalFree(handle) == IntPtr.Zero;
        }

    }

#endregion

    public class NativeMethods
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public const int GENERIC_READ = unchecked(((int)0x80000000));
        public const int GENERIC_WRITE = (0x40000000);

        public const int FILE_SHARE_READ = 0x00000001;
        public const int FILE_SHARE_WRITE = 0x00000002;
        public const int FILE_SHARE_DELETE = 0x00000004;

        public const int S_OK = 0x0;
        public const int E_ABORT = unchecked((int)0x80004004);
        public const int E_NOTIMPL = unchecked((int)0x80004001);

        public const int CREATE_ALWAYS = 2;

        public const int FILE_ATTRIBUTE_NORMAL = 0x00000080;

        public const int STARTF_USESTDHANDLES = 0x00000100;

        public const int STD_INPUT_HANDLE = -10;
        public const int STD_OUTPUT_HANDLE = -11;
        public const int STD_ERROR_HANDLE = -12;

        public const int STILL_ACTIVE = 0x00000103;
        public const int SW_HIDE = 0;

        public const int WAIT_OBJECT_0 = 0x00000000;
        public const int WAIT_FAILED = unchecked((int)0xFFFFFFFF);
        public const int WAIT_TIMEOUT = 0x00000102;
        public const int WAIT_ABANDONED = 0x00000080;
        public const int WAIT_ABANDONED_0 = WAIT_ABANDONED;

        public const int CREATE_NO_WINDOW = 0x08000000;
        public const int CREATE_SUSPENDED = 0x00000004;
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        public const int DUPLICATE_CLOSE_SOURCE = 1;
        public const int DUPLICATE_SAME_ACCESS = 2;

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Ansi, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern bool DuplicateHandle(
            HandleRef hSourceProcessHandle,
            IntPtr hSourceHandle,
            HandleRef hTargetProcess,
            out IntPtr targetHandle,
            int dwDesiredAccess,
            bool bInheritHandle,
            int dwOptions
        );

        //
        // DACL related stuff
        //
        [StructLayout(LayoutKind.Sequential)]
        public class SECURITY_ATTRIBUTES
        {
#if !SILVERLIGHT
            // We don't support ACL's on Silverlight nor on CoreSystem builds in our API's.  
            // But, we need P/Invokes to occasionally take these as parameters.  We can pass null.
            public int nLength = 12;
            public SafeLocalMemHandle lpSecurityDescriptor = new SafeLocalMemHandle(IntPtr.Zero, false);
            public bool bInheritHandle = false;
#endif // !SILVERLIGHT
        }

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        //public static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);
        public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);        

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Ansi, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern IntPtr GetCurrentProcess();

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            /*
            public String lpReserved;
            public String lpDesktop;
            public String lpTitle;
            */
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
            /*
            public SafePipeHandle hStdInput;
            public SafePipeHandle hStdOutput;
            public SafePipeHandle hStdError;
            */
            
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
            
            /*
            public SafeHandle hStdInput;
            public SafeHandle hStdOutput;
            public SafeHandle hStdError;
            */
        }

        public static void Init(STARTUPINFO si)
        {
            si.cb = Marshal.SizeOf(si);
            si.lpReserved = IntPtr.Zero;
            si.lpDesktop = IntPtr.Zero;
            si.lpTitle = IntPtr.Zero;
            si.dwX = 0;
            si.dwY = 0;
            si.dwXSize = 0;
            si.dwYSize = 0;
            si.dwXCountChars = 0;
            si.dwYCountChars = 0;
            si.dwFillAttribute = 0;
            si.dwFlags = 0;
            si.wShowWindow = 0;
            si.cbReserved2 = 0;
            si.lpReserved2 = IntPtr.Zero;
            si.hStdInput = IntPtr.Zero;
            si.hStdOutput = IntPtr.Zero;
            si.hStdError = IntPtr.Zero;
        }

        public static void DisposeOf(STARTUPINFO si)
        {
            // close the handles created for child process
            if (si.hStdInput != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(si.hStdInput);
                si.hStdInput = IntPtr.Zero;
            }

            if (si.hStdOutput != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(si.hStdOutput);
                si.hStdOutput = IntPtr.Zero;
            }

            if (si.hStdError != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(si.hStdError);
                si.hStdError = IntPtr.Zero;
            }
        }    

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            // The handles in PROCESS_INFORMATION are initialized in unmanaged functions.
            // We can't use SafeHandle here because Interop doesn't support [out] SafeHandles in structures/classes yet.            
            public IntPtr hProcess;
            public IntPtr hThread;
            public Int32 dwProcessId;
            public Int32 dwThreadId;

            // Note this class makes no attempt to free the handles
            // Use InitialSetHandle to copy to handles into SafeHandles
        }

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern bool CreateProcess(
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpApplicationName,                   // LPCTSTR
            StringBuilder lpCommandLine,                // LPTSTR - note: CreateProcess might insert a null somewhere in this string
            SECURITY_ATTRIBUTES lpProcessAttributes,    // LPSECURITY_ATTRIBUTES
            SECURITY_ATTRIBUTES lpThreadAttributes,     // LPSECURITY_ATTRIBUTES
            bool bInheritHandles,                        // BOOL
            int dwCreationFlags,                        // DWORD
            IntPtr lpEnvironment,                       // LPVOID
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpCurrentDirectory,                  // LPCTSTR
            ref STARTUPINFO lpStartupInfo,                  // LPSTARTUPINFO
            out PROCESS_INFORMATION lpProcessInformation    // LPPROCESS_INFORMATION
            );

        [DllImport(ExternDll.Advapi32, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern bool CreateProcessWithLogonW(
            string userName,
            string domain,
            IntPtr password,
            LogonFlags logonFlags,
            [MarshalAs(UnmanagedType.LPTStr)]
            string appName,
            StringBuilder cmdLine,
            int creationFlags,
            IntPtr environmentBlock,
            [MarshalAs(UnmanagedType.LPTStr)]
            string lpCurrentDirectory,                  // LPCTSTR            
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport(ExternDll.Advapi32, CharSet = CharSet.Auto, SetLastError = true)]    
        public static extern Boolean CreateProcessAsUser(
            IntPtr hToken,
            String lpApplicationName,
            String lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            Boolean bInheritHandles,
            Int32 dwCreationFlags,
            IntPtr lpEnvironment,
            String lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
            );

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern bool TerminateProcess(SafeProcessHandle processHandle, int exitCode);

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Ansi, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern IntPtr GetStdHandle(int whichHandle);

        [SecurityCritical]
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport(ExternDll.Kernel32, ExactSpelling = true, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public static extern bool CloseHandle(IntPtr handle);

        [Flags]
        internal enum LogonFlags
        {
            LOGON_WITH_PROFILE = 0x00000001,
            LOGON_NETCREDENTIALS_ONLY = 0x00000002
        }        
    }

    internal static class ExternDll
    {

#if FEATURE_PAL && !SILVERLIGHT
#if !PLATFORM_UNIX
        internal const String DLLPREFIX = "";
        internal const String DLLSUFFIX = ".dll";
#else // !PLATFORM_UNIX
#if __APPLE__
        internal const String DLLPREFIX = "lib";
        internal const String DLLSUFFIX = ".dylib";
#elif _AIX
        internal const String DLLPREFIX = "lib";
        internal const String DLLSUFFIX = ".a";
#elif __hppa__ || IA64
        internal const String DLLPREFIX = "lib";
        internal const String DLLSUFFIX = ".sl";
#else
        internal const String DLLPREFIX = "lib";
        internal const String DLLSUFFIX = ".so";
#endif
#endif // !PLATFORM_UNIX

        public const string Kernel32 = DLLPREFIX + "rotor_pal" + DLLSUFFIX;
        public const string User32 = DLLPREFIX + "rotor_pal" + DLLSUFFIX;
        public const string Mscoree  = DLLPREFIX + "sscoree" + DLLSUFFIX;

#elif FEATURE_PAL && SILVERLIGHT
        public const string Kernel32 = "coreclr";
        public const string User32 = "coreclr";
#else
        public const string Kernel32 = "kernel32.dll";
        public const string User32 = "user32.dll";
        public const string Advapi32 = "advapi32.dll";
#endif //!FEATURE_PAL
    }

    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
    public class WBSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private WBSafeHandle() : base(false) { }

        public static readonly WBSafeHandle Null = new WBSafeHandle();

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        override protected bool ReleaseHandle()
        {
            if (handle != IntPtr.Zero) throw new Exception("Expected null handle in NullSafeHandle.");
            return true;
        }
    }

    public static class TokenManagement
    {
        public enum LogonType
        {
            /// <summary>
            /// This logon type is intended for users who will be interactively using the computer, such as a user being logged on  
            /// by a terminal server, remote shell, or similar process.
            /// This logon type has the additional expense of caching logon information for disconnected operations; 
            /// therefore, it is inappropriate for some client/server applications,
            /// such as a mail server.
            /// </summary>
            LOGON32_LOGON_INTERACTIVE = 2,

            /// <summary>
            /// This logon type is intended for high performance servers to authenticate plaintext passwords.

            /// The LogonUser function does not cache credentials for this logon type.
            /// </summary>
            LOGON32_LOGON_NETWORK = 3,

            /// <summary>
            /// This logon type is intended for batch servers, where processes may be executing on behalf of a user without 
            /// their direct intervention. This type is also for higher performance servers that process many plaintext
            /// authentication attempts at a time, such as mail or Web servers. 
            /// The LogonUser function does not cache credentials for this logon type.
            /// </summary>
            LOGON32_LOGON_BATCH = 4,

            /// <summary>
            /// Indicates a service-type logon. The account provided must have the service privilege enabled. 
            /// </summary>
            LOGON32_LOGON_SERVICE = 5,

            /// <summary>
            /// This logon type is for GINA DLLs that log on users who will be interactively using the computer. 
            /// This logon type can generate a unique audit record that shows when the workstation was unlocked. 
            /// </summary>
            LOGON32_LOGON_UNLOCK = 7,

            /// <summary>
            /// This logon type preserves the name and password in the authentication package, which allows the server to make 
            /// connections to other network servers while impersonating the client. A server can accept plaintext credentials 
            /// from a client, call LogonUser, verify that the user can access the system across the network, and still 
            /// communicate with other servers.
            /// NOTE: Windows NT:  This value is not supported. 
            /// </summary>
            LOGON32_LOGON_NETWORK_CLEARTEXT = 8,

            /// <summary>
            /// This logon type allows the caller to clone its current token and specify new credentials for outbound connections.
            /// The new logon session has the same local identifier but uses different credentials for other network connections. 
            /// NOTE: This logon type is supported only by the LOGON32_PROVIDER_WINNT50 logon provider.
            /// NOTE: Windows NT:  This value is not supported. 
            /// </summary>
            LOGON32_LOGON_NEW_CREDENTIALS = 9,
        }

        public enum LogonProvider
        {
            /// <summary>
            /// Use the standard logon provider for the system. 
            /// The default security provider is negotiate, unless you pass NULL for the domain name and the user name 
            /// is not in UPN format. In this case, the default provider is NTLM. 
            /// NOTE: Windows 2000/NT:   The default security provider is NTLM.
            /// </summary>
            LOGON32_PROVIDER_DEFAULT = 0,
            LOGON32_PROVIDER_WINNT35 = 1,
            LOGON32_PROVIDER_WINNT40 = 2,
            LOGON32_PROVIDER_WINNT50 = 3
        }

        [DllImport(ExternDll.Advapi32, SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]        
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPStr)] string pszUserName,
            [MarshalAs(UnmanagedType.LPStr)] string pszDomain,
            [MarshalAs(UnmanagedType.LPStr)] string pszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        //
        // DACL related stuff
        //
        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int Length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        public enum SECURITY_IMPERSONATION_LEVEL : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        };

        public const UInt32 STANDARD_RIGHTS_REQUIRED = 0x000F0000;
        public const UInt32 STANDARD_RIGHTS_READ = 0x00020000;
        public const UInt32 TOKEN_ASSIGN_PRIMARY = 0x0001;
        public const UInt32 TOKEN_DUPLICATE = 0x0002;
        public const UInt32 TOKEN_IMPERSONATE = 0x0004;
        public const UInt32 TOKEN_QUERY = 0x0008;
        public const UInt32 TOKEN_QUERY_SOURCE = 0x0010;
        public const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const UInt32 TOKEN_ADJUST_GROUPS = 0x0040;
        public const UInt32 TOKEN_ADJUST_DEFAULT = 0x0080;
        public const UInt32 TOKEN_ADJUST_SESSIONID = 0x0100;
        public const UInt32 TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
        public const UInt32 TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
            TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
            TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
            TOKEN_ADJUST_SESSIONID);
        public const uint MAXIMUM_ALLOWED = 0x2000000;

        public enum TOKEN_TYPE : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2
        }

        [DllImport(ExternDll.Advapi32, CharSet = CharSet.Auto, SetLastError = true)]
        public extern static bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            ref SECURITY_ATTRIBUTES lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken);

#if false
        public static IntPtr DuplicateToken(IntPtr OriginalToken)
        {
            SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
            sa.bInheritHandle = false;
            sa.Length = Marshal.SizeOf(sa);
            sa.lpSecurityDescriptor = IntPtr.Zero;
            const uint GENERIC_ALL = 0x10000000;
            const int TokenPrimary = 1;
            const int SecurityImpersonation = 2;
            IntPtr DupedToken = new IntPtr(0);
            bool bRetVal = DuplicateTokenEx(
                OriginalToken,
                GENERIC_ALL,
                ref sa,
                SecurityImpersonation,
                TokenPrimary,
                out DupedToken);

            if (bRetVal == false)
            {
                int ret = Marshal.GetLastWin32Error();
                throw new Win32Exception(ret);
            }
            return DupedToken;
        }
#endif

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool LoadUserProfile(IntPtr hToken, ref PROFILEINFO lpProfileInfo);

        [DllImport("userenv.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool UnloadUserProfile(IntPtr hToken, IntPtr hProfile);

        [StructLayout(LayoutKind.Sequential)]

        public struct PROFILEINFO
        {
            public int dwSize;
            public int dwFlags;
            [MarshalAs(UnmanagedType.LPTStr)]
            public String lpUserName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public String lpProfilePath;
            [MarshalAs(UnmanagedType.LPTStr)]
            public String lpDefaultPath;
            [MarshalAs(UnmanagedType.LPTStr)]
            public String lpServerName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public String lpPolicyPath;
            public IntPtr hProfile;
        }
    }

    public class EnvironmentBlock : IDisposable
    {
        bool disposed = false;
        protected void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (pEnvBlock != IntPtr.Zero)
                    {
                        DestroyEnvironmentBlock(pEnvBlock);
                        pEnvBlock = IntPtr.Zero;
                    }
                }
                this.disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [DllImport("userenv.dll", SetLastError = true)]
        internal static extern bool CreateEnvironmentBlock(ref IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError= true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        IntPtr pEnvBlock = IntPtr.Zero;

        /// <summary>
        /// Create an environment
        /// </summary>
        /// <param name="token">The security token</param>
        /// <param name="inherit">Inherit the environment from the calling process</param>
        /// <returns>a dictionary that represents the environ</returns>
        public EnvironmentBlock(IntPtr token, bool inherit)
        {
            if (!CreateEnvironmentBlock(ref pEnvBlock, token, inherit))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public Dictionary<string, string> AsDictionary()
        { 
            Dictionary<String, String> userEnvironment = new Dictionary<string, string>();            
            StringBuilder testData = new StringBuilder("");
            unsafe
            {
                short* start = (short*)pEnvBlock.ToPointer();
                bool done = false;
                short* current = start;
                while (!done)
                {
                    if ((testData.Length > 0) && (*current == 0) && (current != start))
                    {
                        String data = testData.ToString();
                        int index = data.IndexOf('=');
                        if (index == -1)
                        {
                            userEnvironment.Add(data, "");
                        }
                        else if (index == (data.Length - 1))
                        {
                            userEnvironment.Add(data.Substring(0, index), "");
                        }
                        else
                        {
                            userEnvironment.Add(data.Substring(0, index), data.Substring(index + 1));
                        }
                        testData.Length = 0;
                    }
                    if ((*current == 0) && (current != start) && (*(current - 1) == 0))
                    {
                        done = true;
                    }
                    if (*current != 0)
                    {
                        testData.Append((char)*current);
                    }
                    current++;
                }
            }
            return userEnvironment;
        }

        /// <summary>
        /// Create a byte array that represents the environment for 
        /// the different CreateProcess calls
        /// </summary>        
        /// <returns>A byte array</returns>
        public byte[] AsRaw()
        {
            var env = AsDictionary();

            MemoryStream ms = new MemoryStream();
            StreamWriter w = new StreamWriter(ms, Encoding.Unicode);
            w.Flush();
            ms.Position = 0; //Skip any byte order marks to identify the encoding
            Char nullChar = (char)0;
            foreach (string k in env.Keys)
            {
                w.Write("{0}={1}", k, env[k]);
                w.Write(nullChar);
            }
            w.Write(nullChar);
            w.Write(nullChar);
            w.Flush();
            ms.Flush();
            byte[] data = ms.ToArray();
            return data;
        }
    }
}

