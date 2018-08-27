// From reference source v4.6.1/System/services/monitoring/system/diagnosticts/ProcessManager.cs

using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System;
using System.Collections;
using System.IO;
using Microsoft.Win32;
using wbMicrosoft.Win32.SafeHandles;
using System.Collections.Specialized;
using System.Globalization;
using System.Security;
using System.Security.Permissions;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;
using wbMicrosoft.Win32;

namespace wbSystem.Diagnostics
{

    /// <devdoc>
    ///     This static class is a platform independent Api for querying information
    ///     about processes, threads and modules.  It delegates to the platform
    ///     specific classes WinProcessManager for Win9x and NtProcessManager
    ///     for WinNt.
    /// </devdoc>
    /// <internalonly/>
    internal static class ProcessManager
    {

        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        static ProcessManager()
        {
            // In order to query information (OpenProcess) on some protected processes
            // like csrss, we need SeDebugPrivilege privilege.
            // After removing the depenecy on Performance Counter, we don't have a chance
            // to run the code in CLR performance counter to ask for this privilege.
            // So we will try to get the privilege here.
            // We could fail if the user account doesn't have right to do this, but that's fair.

            NativeMethods.LUID luid = new NativeMethods.LUID();
            if (!NativeMethods.LookupPrivilegeValue(null, "SeDebugPrivilege", out luid))
            {
                return;
            }

            IntPtr tokenHandle = IntPtr.Zero;
            try
            {
                if (!NativeMethods.OpenProcessToken(
                        new HandleRef(null, NativeMethods.GetCurrentProcess()),
                        (int)TokenAccessLevels.AdjustPrivileges,
                        out tokenHandle))
                {
                    return;
                }

                NativeMethods.TokenPrivileges tp = new NativeMethods.TokenPrivileges();
                tp.PrivilegeCount = 1;
                tp.Luid = luid;
                tp.Attributes = NativeMethods.SE_PRIVILEGE_ENABLED;

                // AdjustTokenPrivileges can return true even if it didn't succeed (when ERROR_NOT_ALL_ASSIGNED is returned).
                NativeMethods.AdjustTokenPrivileges(new HandleRef(null, tokenHandle), false, tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    SafeNativeMethods.CloseHandle(tokenHandle);
                }
            }
        }



        public static bool IsNt
        {
            get
            {
                return Environment.OSVersion.Platform == PlatformID.Win32NT;
            }
        }

        public static bool IsOSOlderThanXP
        {
            get
            {
                return Environment.OSVersion.Version.Major < 5 ||
                            (Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor == 0);
            }
        }

#if WB_OMIT
        public static ProcessInfo[] GetProcessInfos(string machineName)
        {
            bool isRemoteMachine = IsRemoteMachine(machineName);
            if (IsNt)
            {
                // Do not use performance counter for local machine with Win2000 and above
                if (!isRemoteMachine &&
                    (Environment.OSVersion.Version.Major >= 5))
                {
                    return NtProcessInfoHelper.GetProcessInfos();
                }
                return NtProcessManager.GetProcessInfos(machineName, isRemoteMachine);
            }

            else
            {
                if (isRemoteMachine)
                    throw new PlatformNotSupportedException("Windows NT required for remote");
                return WinProcessManager.GetProcessInfos();
            }
        }
#endif

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static int[] GetProcessIds()
        {
            if (IsNt)
                return NtProcessManager.GetProcessIds();
            else
            {
                return WinProcessManager.GetProcessIds();
            }
        }

#if WB_OMIT
        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static int[] GetProcessIds(string machineName)
        {
            if (IsRemoteMachine(machineName))
            {
                if (IsNt)
                {
                    return NtProcessManager.GetProcessIds(machineName, true);
                }
                else
                {
                    throw new PlatformNotSupportedException("Windows NT required for remote");
                }
            }
            else
            {
                return GetProcessIds();
            }
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        public static bool IsProcessRunning(int processId, string machineName)
        {
            return IsProcessRunning(processId, GetProcessIds(machineName));
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        public static bool IsProcessRunning(int processId)
        {
            return IsProcessRunning(processId, GetProcessIds());
        }

        static bool IsProcessRunning(int processId, int[] processIds)
        {
            for (int i = 0; i < processIds.Length; i++)
                if (processIds[i] == processId)
                    return true;
            return false;
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static int GetProcessIdFromHandle(SafeProcessHandle processHandle)
        {
            if (IsNt)
                return NtProcessManager.GetProcessIdFromHandle(processHandle);
            else
                throw new PlatformNotSupportedException("Windows NT or newer required");
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static IntPtr GetMainWindowHandle(int processId)
        {
            MainWindowFinder finder = new MainWindowFinder();
            return finder.FindMainWindow(processId);
        }

        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        public static ModuleInfo[] GetModuleInfos(int processId)
        {
            if (IsNt)
                return NtProcessManager.GetModuleInfos(processId);
            else
                return WinProcessManager.GetModuleInfos(processId);
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static SafeProcessHandle OpenProcess(int processId, int access, bool throwIfExited)
        {
            SafeProcessHandle processHandle = NativeMethods.OpenProcess(access, false, processId);
            int result = Marshal.GetLastWin32Error();
            if (!processHandle.IsInvalid)
            {
                return processHandle;
            }

            if (processId == 0)
            {
                throw new Win32Exception(5);
            }

            // If the handle is invalid because the process has exited, only throw an exception if throwIfExited is true.            
            if (!IsProcessRunning(processId))
            {
                if (throwIfExited)
                {
                    throw new InvalidOperationException("Process has already exited");
                }
                else
                {
                    return SafeProcessHandle.InvalidHandle;
                }
            }
            throw new Win32Exception(result);
        }

        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        public static SafeThreadHandle OpenThread(int threadId, int access)
        {
            try
            {
                SafeThreadHandle threadHandle = NativeMethods.OpenThread(access, false, threadId);
                int result = Marshal.GetLastWin32Error();
                if (threadHandle.IsInvalid)
                {
                    if (result == NativeMethods.ERROR_INVALID_PARAMETER)
                        throw new InvalidOperationException("Thread has exited");
                    throw new Win32Exception(result);
                }
                return threadHandle;
            }
            catch (EntryPointNotFoundException x)
            {
                throw new PlatformNotSupportedException("Windows 2000 or newer required");
            }
        }



        public static bool IsRemoteMachine(string machineName)
        {
            if (machineName == null)
                throw new ArgumentNullException("machineName");

            if (machineName.Length == 0)
                throw new ArgumentException("machineName invalid parameter");

            string baseName;

            if (machineName.StartsWith("\\", StringComparison.Ordinal))
                baseName = machineName.Substring(2);
            else
                baseName = machineName;
            if (baseName.Equals(".")) return false;

            StringBuilder sb = new StringBuilder(256);
            SafeNativeMethods.GetComputerName(sb, new int[] { sb.Capacity });
            string computerName = sb.ToString();
            if (String.Compare(computerName, baseName, StringComparison.OrdinalIgnoreCase) == 0) return false;
            return true;
        }
#endif
    }

    /// <devdoc>
    ///     This static class provides the process api for the Win9x platform.
    ///     We use the toolhelp32 api to query process, thread and module information.
    /// </devdoc>
    /// <internalonly/>
    internal static class WinProcessManager
    {

        // This is expensive.  We should specialize getprocessinfos and only get 
        // the ids instead of getting all the info and then copying the ids out.
        public static int[] GetProcessIds()
        {
            ProcessInfo[] infos = GetProcessInfos();
            int[] ids = new int[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                ids[i] = infos[i].processId;
            }
            return ids;
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Process, ResourceScope.Process)]
        public static ProcessInfo[] GetProcessInfos()
        {
            IntPtr handle = (IntPtr)(-1);
            GCHandle bufferHandle = new GCHandle();
            ArrayList threadInfos = new ArrayList();
            Hashtable processInfos = new Hashtable();

            try
            {
                handle = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS | NativeMethods.TH32CS_SNAPTHREAD, 0);
                if (handle == (IntPtr)(-1)) throw new Win32Exception();
                int entrySize = (int)Marshal.SizeOf(typeof(NativeMethods.WinProcessEntry));
                int bufferSize = entrySize + NativeMethods.WinProcessEntry.sizeofFileName;
                int[] buffer = new int[bufferSize / 4];
                bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject();
                Marshal.WriteInt32(bufferPtr, bufferSize);

                HandleRef handleRef = new HandleRef(null, handle);

                if (NativeMethods.Process32First(handleRef, bufferPtr))
                {
                    do
                    {
                        NativeMethods.WinProcessEntry process = new NativeMethods.WinProcessEntry();
                        Marshal.PtrToStructure(bufferPtr, process);
                        ProcessInfo processInfo = new ProcessInfo();
                        String name = Marshal.PtrToStringAnsi((IntPtr)((long)bufferPtr + entrySize));
                        processInfo.processName = Path.ChangeExtension(Path.GetFileName(name), null);
                        processInfo.handleCount = process.cntUsage;
                        processInfo.processId = process.th32ProcessID;
                        processInfo.basePriority = process.pcPriClassBase;
                        processInfo.mainModuleId = process.th32ModuleID;
                        processInfos.Add(processInfo.processId, processInfo);
                        Marshal.WriteInt32(bufferPtr, bufferSize);
                    }
                    while (NativeMethods.Process32Next(handleRef, bufferPtr));
                }

                NativeMethods.WinThreadEntry thread = new NativeMethods.WinThreadEntry();
                thread.dwSize = Marshal.SizeOf(thread);
                if (NativeMethods.Thread32First(handleRef, thread))
                {
                    do
                    {
                        ThreadInfo threadInfo = new ThreadInfo();
                        threadInfo.threadId = thread.th32ThreadID;
                        threadInfo.processId = thread.th32OwnerProcessID;
                        threadInfo.basePriority = thread.tpBasePri;
                        threadInfo.currentPriority = thread.tpBasePri + thread.tpDeltaPri;
                        threadInfos.Add(threadInfo);
                    }
                    while (NativeMethods.Thread32Next(handleRef, thread));
                }

                for (int i = 0; i < threadInfos.Count; i++)
                {
                    ThreadInfo threadInfo = (ThreadInfo)threadInfos[i];
                    ProcessInfo processInfo = (ProcessInfo)processInfos[threadInfo.processId];
                    if (processInfo != null)
                        processInfo.threadInfoList.Add(threadInfo);
                    //else 
                    //    throw new InvalidOperationException(SR.GetString(SR.ProcessNotFound, threadInfo.threadId.ToString(), threadInfo.processId.ToString()));                   
                }
            }
            finally
            {
                if (bufferHandle.IsAllocated) bufferHandle.Free();
                Debug.WriteLineIf(Process.processTracing.TraceVerbose, "Process - CloseHandle(toolhelp32 snapshot handle)");
                if (handle != (IntPtr)(-1)) SafeNativeMethods.CloseHandle(handle);
            }

            ProcessInfo[] temp = new ProcessInfo[processInfos.Values.Count];
            processInfos.Values.CopyTo(temp, 0);
            return temp;
        }

        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        public static ModuleInfo[] GetModuleInfos(int processId)
        {
            IntPtr handle = (IntPtr)(-1);
            GCHandle bufferHandle = new GCHandle();
            ArrayList moduleInfos = new ArrayList();

            try
            {
                handle = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPMODULE, processId);
                if (handle == (IntPtr)(-1)) throw new Win32Exception();
                int entrySize = Marshal.SizeOf(typeof(NativeMethods.WinModuleEntry));
                int bufferSize = entrySize + NativeMethods.WinModuleEntry.sizeofFileName + NativeMethods.WinModuleEntry.sizeofModuleName;
                int[] buffer = new int[bufferSize / 4];
                bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject();
                Marshal.WriteInt32(bufferPtr, bufferSize);

                HandleRef handleRef = new HandleRef(null, handle);

                if (NativeMethods.Module32First(handleRef, bufferPtr))
                {
                    do
                    {
                        NativeMethods.WinModuleEntry module = new NativeMethods.WinModuleEntry();
                        Marshal.PtrToStructure(bufferPtr, module);
                        ModuleInfo moduleInfo = new ModuleInfo();
                        moduleInfo.baseName = Marshal.PtrToStringAnsi((IntPtr)((long)bufferPtr + entrySize));
                        moduleInfo.fileName = Marshal.PtrToStringAnsi((IntPtr)((long)bufferPtr + entrySize + NativeMethods.WinModuleEntry.sizeofModuleName));
                        moduleInfo.baseOfDll = module.modBaseAddr;
                        moduleInfo.sizeOfImage = module.modBaseSize;
                        moduleInfo.Id = module.th32ModuleID;
                        moduleInfos.Add(moduleInfo);
                        Marshal.WriteInt32(bufferPtr, bufferSize);
                    }
                    while (NativeMethods.Module32Next(handleRef, bufferPtr));
                }
            }
            finally
            {
                if (bufferHandle.IsAllocated) bufferHandle.Free();
                Debug.WriteLineIf(Process.processTracing.TraceVerbose, "Process - CloseHandle(toolhelp32 snapshot handle)");
                if (handle != (IntPtr)(-1)) SafeNativeMethods.CloseHandle(handle);
            }

            ModuleInfo[] temp = new ModuleInfo[moduleInfos.Count];
            moduleInfos.CopyTo(temp, 0);
            return temp;
        }

    }

    /// <devdoc>
    ///     This static class provides the process api for the WinNt platform.
    ///     We use the performance counter api to query process and thread
    ///     information.  Module information is obtained using PSAPI.
    /// </devdoc>
    /// <internalonly/>
    internal static class NtProcessManager
    {
        private const int ProcessPerfCounterId = 230;
        private const int ThreadPerfCounterId = 232;
        private const string PerfCounterQueryString = "230 232";
        internal const int IdleProcessID = 0;

        static Hashtable valueIds;

        static NtProcessManager()
        {
            valueIds = new Hashtable();
            valueIds.Add("Handle Count", ValueId.HandleCount);
            valueIds.Add("Pool Paged Bytes", ValueId.PoolPagedBytes);
            valueIds.Add("Pool Nonpaged Bytes", ValueId.PoolNonpagedBytes);
            valueIds.Add("Elapsed Time", ValueId.ElapsedTime);
            valueIds.Add("Virtual Bytes Peak", ValueId.VirtualBytesPeak);
            valueIds.Add("Virtual Bytes", ValueId.VirtualBytes);
            valueIds.Add("Private Bytes", ValueId.PrivateBytes);
            valueIds.Add("Page File Bytes", ValueId.PageFileBytes);
            valueIds.Add("Page File Bytes Peak", ValueId.PageFileBytesPeak);
            valueIds.Add("Working Set Peak", ValueId.WorkingSetPeak);
            valueIds.Add("Working Set", ValueId.WorkingSet);
            valueIds.Add("ID Thread", ValueId.ThreadId);
            valueIds.Add("ID Process", ValueId.ProcessId);
            valueIds.Add("Priority Base", ValueId.BasePriority);
            valueIds.Add("Priority Current", ValueId.CurrentPriority);
            valueIds.Add("% User Time", ValueId.UserTime);
            valueIds.Add("% Privileged Time", ValueId.PrivilegedTime);
            valueIds.Add("Start Address", ValueId.StartAddress);
            valueIds.Add("Thread State", ValueId.ThreadState);
            valueIds.Add("Thread Wait Reason", ValueId.ThreadWaitReason);
        }

        internal static int SystemProcessID
        {
            get
            {
                const int systemProcessIDOnXP = 4;
                const int systemProcessIDOn2K = 8;

                if (ProcessManager.IsOSOlderThanXP)
                {
                    return systemProcessIDOn2K;
                }
                else
                {
                    return systemProcessIDOnXP;
                }
            }
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static int[] GetProcessIds(string machineName, bool isRemoteMachine)
        {
            ProcessInfo[] infos = GetProcessInfos(machineName, isRemoteMachine);
            int[] ids = new int[infos.Length];
            for (int i = 0; i < infos.Length; i++)
                ids[i] = infos[i].processId;
            return ids;
        }

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static int[] GetProcessIds()
        {
            int[] processIds = new int[256];
            int size;
            for (;;)
            {
                if (!NativeMethods.EnumProcesses(processIds, processIds.Length * 4, out size))
                    throw new Win32Exception();
                if (size == processIds.Length * 4)
                {
                    processIds = new int[processIds.Length * 2];
                    continue;
                }
                break;
            }
            int[] ids = new int[size / 4];
            Array.Copy(processIds, ids, ids.Length);
            return ids;
        }

#if WB_OMIT
        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        public static ModuleInfo[] GetModuleInfos(int processId)
        {
            return GetModuleInfos(processId, false);
        }

        [ResourceExposure(ResourceScope.Process)]
        [ResourceConsumption(ResourceScope.Process)]
        public static ModuleInfo GetFirstModuleInfo(int processId)
        {
            ModuleInfo[] moduleInfos = GetModuleInfos(processId, true);
            if (moduleInfos.Length == 0)
            {
                return null;
            }
            else
            {
                return moduleInfos[0];
            }
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        private static ModuleInfo[] GetModuleInfos(int processId, bool firstModuleOnly)
        {
            Contract.Ensures(Contract.Result<ModuleInfo[]>().Length >= 1);

            // preserving Everett behavior.    
            if (processId == SystemProcessID || processId == IdleProcessID)
            {
                // system process and idle process doesn't have any modules 
                throw new Win32Exception(HResults.EFail, SR.GetString(SR.EnumProcessModuleFailed));
            }

            SafeProcessHandle processHandle = SafeProcessHandle.InvalidHandle;
            try
            {
                processHandle = ProcessManager.OpenProcess(processId, NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ, true);

                IntPtr[] moduleHandles = new IntPtr[64];
                GCHandle moduleHandlesArrayHandle = new GCHandle();
                int moduleCount = 0;
                for (;;)
                {
                    bool enumResult = false;
                    try
                    {
                        moduleHandlesArrayHandle = GCHandle.Alloc(moduleHandles, GCHandleType.Pinned);
                        enumResult = NativeMethods.EnumProcessModules(processHandle, moduleHandlesArrayHandle.AddrOfPinnedObject(), moduleHandles.Length * IntPtr.Size, ref moduleCount);

                        // The API we need to use to enumerate process modules differs on two factors:
                        //   1) If our process is running in WOW64.
                        //   2) The bitness of the process we wish to introspect.
                        //
                        // If we are not running in WOW64 or we ARE in WOW64 but want to inspect a 32 bit process
                        // we can call psapi!EnumProcessModules.
                        //  
                        // If we are running in WOW64 and we want to inspect the modules of a 64 bit process then
                        // psapi!EnumProcessModules will return false with ERROR_PARTIAL_COPY (299).  In this case we can't 
                        // do the enumeration at all.  So we'll detect this case and bail out.
                        //
                        // Also, EnumProcessModules is not a reliable method to get the modules for a process. 
                        // If OS loader is touching module information, this method might fail and copy part of the data.
                        // This is no easy solution to this problem. The only reliable way to fix this is to 
                        // suspend all the threads in target process. Of course we don't want to do this in Process class.
                        // So we just to try avoid the ---- by calling the same method 50 (an arbitary number) times.
                        //
                        if (!enumResult)
                        {
                            bool sourceProcessIsWow64 = false;
                            bool targetProcessIsWow64 = false;
                            if (!ProcessManager.IsOSOlderThanXP)
                            {
                                SafeProcessHandle hCurProcess = SafeProcessHandle.InvalidHandle;
                                try
                                {
                                    hCurProcess = ProcessManager.OpenProcess(NativeMethods.GetCurrentProcessId(), NativeMethods.PROCESS_QUERY_INFORMATION, true);
                                    bool wow64Ret;

                                    wow64Ret = SafeNativeMethods.IsWow64Process(hCurProcess, ref sourceProcessIsWow64);
                                    if (!wow64Ret)
                                    {
                                        throw new Win32Exception();
                                    }

                                    wow64Ret = SafeNativeMethods.IsWow64Process(processHandle, ref targetProcessIsWow64);
                                    if (!wow64Ret)
                                    {
                                        throw new Win32Exception();
                                    }

                                    if (sourceProcessIsWow64 && !targetProcessIsWow64)
                                    {
                                        // Wow64 isn't going to allow this to happen, the best we can do is give a descriptive error to the user.
                                        throw new Win32Exception(NativeMethods.ERROR_PARTIAL_COPY, SR.GetString(SR.EnumProcessModuleFailedDueToWow));
                                    }

                                }
                                finally
                                {
                                    if (hCurProcess != SafeProcessHandle.InvalidHandle)
                                    {
                                        hCurProcess.Close();
                                    }
                                }
                            }

                            // If the failure wasn't due to Wow64, try again.
                            for (int i = 0; i < 50; i++)
                            {
                                enumResult = NativeMethods.EnumProcessModules(processHandle, moduleHandlesArrayHandle.AddrOfPinnedObject(), moduleHandles.Length * IntPtr.Size, ref moduleCount);
                                if (enumResult)
                                {
                                    break;
                                }
                                Thread.Sleep(1);
                            }
                        }
                    }
                    finally
                    {
                        moduleHandlesArrayHandle.Free();
                    }

                    if (!enumResult)
                    {
                        throw new Win32Exception();
                    }

                    moduleCount /= IntPtr.Size;
                    if (moduleCount <= moduleHandles.Length) break;
                    moduleHandles = new IntPtr[moduleHandles.Length * 2];
                }
                ArrayList moduleInfos = new ArrayList();

                int ret;
                for (int i = 0; i < moduleCount; i++)
                {
                    try
                    {
                        ModuleInfo moduleInfo = new ModuleInfo();
                        IntPtr moduleHandle = moduleHandles[i];
                        NativeMethods.NtModuleInfo ntModuleInfo = new NativeMethods.NtModuleInfo();
                        if (!NativeMethods.GetModuleInformation(processHandle, new HandleRef(null, moduleHandle), ntModuleInfo, Marshal.SizeOf(ntModuleInfo)))
                            throw new Win32Exception();
                        moduleInfo.sizeOfImage = ntModuleInfo.SizeOfImage;
                        moduleInfo.entryPoint = ntModuleInfo.EntryPoint;
                        moduleInfo.baseOfDll = ntModuleInfo.BaseOfDll;

                        StringBuilder baseName = new StringBuilder(1024);
                        ret = NativeMethods.GetModuleBaseName(processHandle, new HandleRef(null, moduleHandle), baseName, baseName.Capacity * 2);
                        if (ret == 0) throw new Win32Exception();
                        moduleInfo.baseName = baseName.ToString();

                        StringBuilder fileName = new StringBuilder(1024);
                        ret = NativeMethods.GetModuleFileNameEx(processHandle, new HandleRef(null, moduleHandle), fileName, fileName.Capacity * 2);
                        if (ret == 0) throw new Win32Exception();
                        moduleInfo.fileName = fileName.ToString();

                        // smss.exe is started before the win32 subsystem so it get this funny "\systemroot\.." path.
                        // We change this to the actual path by appending "smss.exe" to GetSystemDirectory()
                        if (string.Compare(moduleInfo.fileName, "\\SystemRoot\\System32\\smss.exe", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            moduleInfo.fileName = Path.Combine(Environment.SystemDirectory, "smss.exe");
                        }
                        // Avoid returning Unicode-style long string paths.  IO methods cannot handle them.
                        if (moduleInfo.fileName != null
                            && moduleInfo.fileName.Length >= 4
                            && moduleInfo.fileName.StartsWith(@"\\?\", StringComparison.Ordinal))
                        {

                            moduleInfo.fileName = moduleInfo.fileName.Substring(4);
                        }

                        moduleInfos.Add(moduleInfo);
                    }
                    catch (Win32Exception e)
                    {
                        if (e.NativeErrorCode == NativeMethods.ERROR_INVALID_HANDLE || e.NativeErrorCode == NativeMethods.ERROR_PARTIAL_COPY)
                        {
                            // It's possible that another thread casued this module to become
                            // unloaded (e.g FreeLibrary was called on the module).  Ignore it and
                            // move on.
                        }
                        else
                        {
                            throw;
                        }
                    }

                    //
                    // If the user is only interested in the main module, break now.
                    // This avoid some waste of time. In addition, if the application unloads a DLL                     
                    // we will not get an exception. 
                    //
                    if (firstModuleOnly) { break; }
                }
                ModuleInfo[] temp = new ModuleInfo[moduleInfos.Count];
                moduleInfos.CopyTo(temp, 0);
                return temp;
            }
            finally
            {
                Debug.WriteLineIf(Process.processTracing.TraceVerbose, "Process - CloseHandle(process)");
                if (!processHandle.IsInvalid)
                {
                    processHandle.Close();
                }
            }
        }
#endif

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        public static int GetProcessIdFromHandle(SafeProcessHandle processHandle)
        {
            NativeMethods.NtProcessBasicInfo info = new NativeMethods.NtProcessBasicInfo();
            int status = NativeMethods.NtQueryInformationProcess(processHandle, NativeMethods.NtQueryProcessBasicInfo, info, (int)Marshal.SizeOf(info), null);
            if (status != 0)
            {
                throw new InvalidOperationException("Unable to retrieve process id", new Win32Exception(status));
            }
            // We should change the signature of this function and ID property in process class.
            return info.UniqueProcessId.ToInt32();
        }
        public static ProcessInfo[] GetProcessInfos(string machineName, bool isRemoteMachine)
        {
            // We demand unmanaged code here because PerformanceCounterLib doesn't demand
            // anything.  This is the only place we do GetPerformanceCounterLib, and it isn't cached.
            new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Demand();
            PerformanceCounterLib library = null;
            try
            {
                library = PerformanceCounterLib.GetPerformanceCounterLib(machineName, new CultureInfo(0x009));
                return GetProcessInfos(library);
            }
            catch (Exception e)
            {
                if (isRemoteMachine)
                {
                    throw new InvalidOperationException("Couldn't connect to remote machine", e);
                }
                else
                {
                    throw e;
                }
            }
            // We don't want to call library.Close() here because that would cause us to unload all of the perflibs.
            // On the next call to GetProcessInfos, we'd have to load them all up again, which is SLOW!
        }

        static ProcessInfo[] GetProcessInfos(PerformanceCounterLib library)
        {
            ProcessInfo[] processInfos = new ProcessInfo[0];
            byte[] dataPtr = null;

            int retryCount = 5;
            while (processInfos.Length == 0 && retryCount != 0)
            {
                try
                {
                    dataPtr = library.GetPerformanceData(PerfCounterQueryString);
                    processInfos = GetProcessInfos(library, ProcessPerfCounterId, ThreadPerfCounterId, dataPtr);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException(SR.GetString(SR.CouldntGetProcessInfos), e);
                }

                --retryCount;
            }

            if (processInfos.Length == 0)
                throw new InvalidOperationException(SR.GetString(SR.ProcessDisabled));

            return processInfos;

        }

        static ProcessInfo[] GetProcessInfos(PerformanceCounterLib library, int processIndex, int threadIndex, byte[] data)
        {
            Debug.WriteLineIf(Process.processTracing.TraceVerbose, "GetProcessInfos()");
            Hashtable processInfos = new Hashtable();
            ArrayList threadInfos = new ArrayList();

            GCHandle dataHandle = new GCHandle();
            try
            {
                dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                IntPtr dataBlockPtr = dataHandle.AddrOfPinnedObject();
                NativeMethods.PERF_DATA_BLOCK dataBlock = new NativeMethods.PERF_DATA_BLOCK();
                Marshal.PtrToStructure(dataBlockPtr, dataBlock);
                IntPtr typePtr = (IntPtr)((long)dataBlockPtr + dataBlock.HeaderLength);
                NativeMethods.PERF_INSTANCE_DEFINITION instance = new NativeMethods.PERF_INSTANCE_DEFINITION();
                NativeMethods.PERF_COUNTER_BLOCK counterBlock = new NativeMethods.PERF_COUNTER_BLOCK();
                for (int i = 0; i < dataBlock.NumObjectTypes; i++)
                {
                    NativeMethods.PERF_OBJECT_TYPE type = new NativeMethods.PERF_OBJECT_TYPE();
                    Marshal.PtrToStructure(typePtr, type);
                    IntPtr instancePtr = (IntPtr)((long)typePtr + type.DefinitionLength);
                    IntPtr counterPtr = (IntPtr)((long)typePtr + type.HeaderLength);
                    ArrayList counterList = new ArrayList();

                    for (int j = 0; j < type.NumCounters; j++)
                    {
                        NativeMethods.PERF_COUNTER_DEFINITION counter = new NativeMethods.PERF_COUNTER_DEFINITION();
                        Marshal.PtrToStructure(counterPtr, counter);
                        string counterName = library.GetCounterName(counter.CounterNameTitleIndex);

                        if (type.ObjectNameTitleIndex == processIndex)
                            counter.CounterNameTitlePtr = (int)GetValueId(counterName);
                        else if (type.ObjectNameTitleIndex == threadIndex)
                            counter.CounterNameTitlePtr = (int)GetValueId(counterName);
                        counterList.Add(counter);
                        counterPtr = (IntPtr)((long)counterPtr + counter.ByteLength);
                    }
                    NativeMethods.PERF_COUNTER_DEFINITION[] counters = new NativeMethods.PERF_COUNTER_DEFINITION[counterList.Count];
                    counterList.CopyTo(counters, 0);
                    for (int j = 0; j < type.NumInstances; j++)
                    {
                        Marshal.PtrToStructure(instancePtr, instance);
                        IntPtr namePtr = (IntPtr)((long)instancePtr + instance.NameOffset);
                        string instanceName = Marshal.PtrToStringUni(namePtr);
                        if (instanceName.Equals("_Total")) continue;
                        IntPtr counterBlockPtr = (IntPtr)((long)instancePtr + instance.ByteLength);
                        Marshal.PtrToStructure(counterBlockPtr, counterBlock);
                        if (type.ObjectNameTitleIndex == processIndex)
                        {
                            ProcessInfo processInfo = GetProcessInfo(type, (IntPtr)((long)instancePtr + instance.ByteLength), counters);
                            if (processInfo.processId == 0 && string.Compare(instanceName, "Idle", StringComparison.OrdinalIgnoreCase) != 0)
                            {
                                // Sometimes we'll get a process structure that is not completely filled in.
                                // We can catch some of these by looking for non-"idle" processes that have id 0
                                // and ignoring those.
                                Debug.WriteLineIf(Process.processTracing.TraceVerbose, "GetProcessInfos() - found a non-idle process with id 0; ignoring.");
                            }
                            else
                            {
                                if (processInfos[processInfo.processId] != null)
                                {
                                    // We've found two entries in the perfcounters that claim to be the
                                    // same process.  We throw an exception.  Is this really going to be
                                    // helpfull to the user?  Should we just ignore?
                                    Debug.WriteLineIf(Process.processTracing.TraceVerbose, "GetProcessInfos() - found a duplicate process id");
                                }
                                else
                                {
                                    // the performance counters keep a 15 character prefix of the exe name, and then delete the ".exe",
                                    // if it's in the first 15.  The problem is that sometimes that will leave us with part of ".exe"
                                    // at the end.  If instanceName ends in ".", ".e", or ".ex" we remove it.
                                    string processName = instanceName;
                                    if (processName.Length == 15)
                                    {
                                        if (instanceName.EndsWith(".", StringComparison.Ordinal)) processName = instanceName.Substring(0, 14);
                                        else if (instanceName.EndsWith(".e", StringComparison.Ordinal)) processName = instanceName.Substring(0, 13);
                                        else if (instanceName.EndsWith(".ex", StringComparison.Ordinal)) processName = instanceName.Substring(0, 12);
                                    }
                                    processInfo.processName = processName;
                                    processInfos.Add(processInfo.processId, processInfo);
                                }
                            }
                        }
                        else if (type.ObjectNameTitleIndex == threadIndex)
                        {
                            ThreadInfo threadInfo = GetThreadInfo(type, (IntPtr)((long)instancePtr + instance.ByteLength), counters);
                            if (threadInfo.threadId != 0) threadInfos.Add(threadInfo);
                        }
                        instancePtr = (IntPtr)((long)instancePtr + instance.ByteLength + counterBlock.ByteLength);
                    }

                    typePtr = (IntPtr)((long)typePtr + type.TotalByteLength);
                }
            }
            finally
            {
                if (dataHandle.IsAllocated) dataHandle.Free();
            }

            for (int i = 0; i < threadInfos.Count; i++)
            {
                ThreadInfo threadInfo = (ThreadInfo)threadInfos[i];
                ProcessInfo processInfo = (ProcessInfo)processInfos[threadInfo.processId];
                if (processInfo != null)
                {
                    processInfo.threadInfoList.Add(threadInfo);
                }
            }

            ProcessInfo[] temp = new ProcessInfo[processInfos.Values.Count];
            processInfos.Values.CopyTo(temp, 0);
            return temp;
        }

        static ThreadInfo GetThreadInfo(NativeMethods.PERF_OBJECT_TYPE type, IntPtr instancePtr, NativeMethods.PERF_COUNTER_DEFINITION[] counters)
        {
            ThreadInfo threadInfo = new ThreadInfo();
            for (int i = 0; i < counters.Length; i++)
            {
                NativeMethods.PERF_COUNTER_DEFINITION counter = counters[i];
                long value = ReadCounterValue(counter.CounterType, (IntPtr)((long)instancePtr + counter.CounterOffset));
                switch ((ValueId)counter.CounterNameTitlePtr)
                {
                    case ValueId.ProcessId:
                        threadInfo.processId = (int)value;
                        break;
                    case ValueId.ThreadId:
                        threadInfo.threadId = (int)value;
                        break;
                    case ValueId.BasePriority:
                        threadInfo.basePriority = (int)value;
                        break;
                    case ValueId.CurrentPriority:
                        threadInfo.currentPriority = (int)value;
                        break;
                    case ValueId.StartAddress:
                        threadInfo.startAddress = (IntPtr)value;
                        break;
                    case ValueId.ThreadState:
                        threadInfo.threadState = (ThreadState)value;
                        break;
                    case ValueId.ThreadWaitReason:
                        threadInfo.threadWaitReason = GetThreadWaitReason((int)value);
                        break;
                }
            }

            return threadInfo;
        }

        internal static ThreadWaitReason GetThreadWaitReason(int value)
        {
            switch (value)
            {
                case 0:
                case 7: return ThreadWaitReason.Executive;
                case 1:
                case 8: return ThreadWaitReason.FreePage;
                case 2:
                case 9: return ThreadWaitReason.PageIn;
                case 3:
                case 10: return ThreadWaitReason.SystemAllocation;
                case 4:
                case 11: return ThreadWaitReason.ExecutionDelay;
                case 5:
                case 12: return ThreadWaitReason.Suspended;
                case 6:
                case 13: return ThreadWaitReason.UserRequest;
                case 14: return ThreadWaitReason.EventPairHigh; ;
                case 15: return ThreadWaitReason.EventPairLow;
                case 16: return ThreadWaitReason.LpcReceive;
                case 17: return ThreadWaitReason.LpcReply;
                case 18: return ThreadWaitReason.VirtualMemory;
                case 19: return ThreadWaitReason.PageOut;
                default: return ThreadWaitReason.Unknown;
            }
        }

        static ProcessInfo GetProcessInfo(NativeMethods.PERF_OBJECT_TYPE type, IntPtr instancePtr, NativeMethods.PERF_COUNTER_DEFINITION[] counters)
        {
            ProcessInfo processInfo = new ProcessInfo();
            for (int i = 0; i < counters.Length; i++)
            {
                NativeMethods.PERF_COUNTER_DEFINITION counter = counters[i];
                long value = ReadCounterValue(counter.CounterType, (IntPtr)((long)instancePtr + counter.CounterOffset));
                switch ((ValueId)counter.CounterNameTitlePtr)
                {
                    case ValueId.ProcessId:
                        processInfo.processId = (int)value;
                        break;
                    case ValueId.HandleCount:
                        processInfo.handleCount = (int)value;
                        break;
                    case ValueId.PoolPagedBytes:
                        processInfo.poolPagedBytes = value;
                        break;
                    case ValueId.PoolNonpagedBytes:
                        processInfo.poolNonpagedBytes = value;
                        break;
                    case ValueId.VirtualBytes:
                        processInfo.virtualBytes = value;
                        break;
                    case ValueId.VirtualBytesPeak:
                        processInfo.virtualBytesPeak = value;
                        break;
                    case ValueId.WorkingSetPeak:
                        processInfo.workingSetPeak = value;
                        break;
                    case ValueId.WorkingSet:
                        processInfo.workingSet = value;
                        break;
                    case ValueId.PageFileBytesPeak:
                        processInfo.pageFileBytesPeak = value;
                        break;
                    case ValueId.PageFileBytes:
                        processInfo.pageFileBytes = value;
                        break;
                    case ValueId.PrivateBytes:
                        processInfo.privateBytes = value;
                        break;
                    case ValueId.BasePriority:
                        processInfo.basePriority = (int)value;
                        break;
                }
            }
            return processInfo;
        }

        static ValueId GetValueId(string counterName)
        {
            if (counterName != null)
            {
                object id = valueIds[counterName];
                if (id != null)
                    return (ValueId)id;
            }
            return ValueId.Unknown;
        }

        static long ReadCounterValue(int counterType, IntPtr dataPtr)
        {
            if ((counterType & NativeMethods.NtPerfCounterSizeLarge) != 0)
                return Marshal.ReadInt64(dataPtr);
            else
                return (long)Marshal.ReadInt32(dataPtr);
        }

        enum ValueId
        {
            Unknown = -1,
            HandleCount,
            PoolPagedBytes,
            PoolNonpagedBytes,
            ElapsedTime,
            VirtualBytesPeak,
            VirtualBytes,
            PrivateBytes,
            PageFileBytes,
            PageFileBytesPeak,
            WorkingSetPeak,
            WorkingSet,
            ThreadId,
            ProcessId,
            BasePriority,
            CurrentPriority,
            UserTime,
            PrivilegedTime,
            StartAddress,
            ThreadState,
            ThreadWaitReason
        }
    }
}

