// Contains multiple source files, search from // From

using Microsoft.Win32;
using wbMicrosoft.Win32.SafeHandles;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Diagnostics.Tracing;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
#if !SILVERLIGHT
using System.Threading;
using System.Collections;
using System.IO;
using System.Configuration;
#endif

using SafeFileHandle = global::Microsoft.Win32.SafeHandles.SafeFileHandle;
using SafeWaitHandle = global::Microsoft.Win32.SafeHandles.SafeWaitHandle;
using ExternDll = wbSystem.ExternDll;

// From reference source v4.6.1/mscorlib/microsoft/win32/unsafenativemethods.cs
//#define WB_OMIT

// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class: UnsafeNativeMethods
**
============================================================*/

namespace wbMicrosoft.Win32
{    
    [System.Security.SecurityCritical]  // auto-generated
    [SuppressUnmanagedCodeSecurityAttribute()]
    internal static partial class UnsafeNativeMethods
    {

        [DllImport(Win32Native.KERNEL32, EntryPoint = "GetTimeZoneInformation", SetLastError = true, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern int GetTimeZoneInformation(out Win32Native.TimeZoneInformation lpTimeZoneInformation);

        [DllImport(Win32Native.KERNEL32, EntryPoint = "GetDynamicTimeZoneInformation", SetLastError = true, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern int GetDynamicTimeZoneInformation(out Win32Native.DynamicTimeZoneInformation lpDynamicTimeZoneInformation);

        // 
        // BOOL GetFileMUIPath(
        //   DWORD  dwFlags,
        //   PCWSTR  pcwszFilePath,
        //   PWSTR  pwszLanguage,
        //   PULONG  pcchLanguage,
        //   PWSTR  pwszFileMUIPath,
        //   PULONG  pcchFileMUIPath,
        //   PULONGLONG  pululEnumerator
        // );
        // 
        [DllImport(Win32Native.KERNEL32, EntryPoint = "GetFileMUIPath", SetLastError = true, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.Machine)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileMUIPath(
                                     int flags,
                                     [MarshalAs(UnmanagedType.LPWStr)]
                                     String filePath,
                                     [MarshalAs(UnmanagedType.LPWStr)]
                                     StringBuilder language,
                                     ref int languageLength,
                                     [MarshalAs(UnmanagedType.LPWStr)]
                                     StringBuilder fileMuiPath,
                                     ref int fileMuiPathLength,
                                     ref Int64 enumerator);


        [DllImport(Win32Native.USER32, EntryPoint = "LoadStringW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        [ResourceExposure(ResourceScope.Process)]
        internal static extern int LoadString(SafeLibraryHandle handle, int id, StringBuilder buffer, int bufferLength);

        [DllImport(Win32Native.KERNEL32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern SafeLibraryHandle LoadLibraryEx(string libFilename, IntPtr reserved, int flags);

        [DllImport(Win32Native.KERNEL32, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [ResourceExposure(ResourceScope.None)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal static extern bool FreeLibrary(IntPtr hModule);


        [SecurityCritical]
        [SuppressUnmanagedCodeSecurityAttribute()]
        internal static unsafe class ManifestEtw
        {
            //
            // Constants error coded returned by ETW APIs
            //

            // The event size is larger than the allowed maximum (64k - header).
            internal const int ERROR_ARITHMETIC_OVERFLOW = 534;

            // Occurs when filled buffers are trying to flush to disk, 
            // but disk IOs are not happening fast enough. 
            // This happens when the disk is slow and event traffic is heavy. 
            // Eventually, there are no more free (empty) buffers and the event is dropped.
            internal const int ERROR_NOT_ENOUGH_MEMORY = 8;

            internal const int ERROR_MORE_DATA = 0xEA;
            internal const int ERROR_NOT_SUPPORTED = 50;
            internal const int ERROR_INVALID_PARAMETER = 0x57;

            //
            // ETW Methods
            //

            internal const int EVENT_CONTROL_CODE_DISABLE_PROVIDER = 0;
            internal const int EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
            internal const int EVENT_CONTROL_CODE_CAPTURE_STATE = 2;

            //
            // Callback
            //
            [SecurityCritical]
            internal unsafe delegate void EtwEnableCallback(
                [In] ref Guid sourceId,
                [In] int isEnabled,
                [In] byte level,
                [In] long matchAnyKeywords,
                [In] long matchAllKeywords,
                [In] EVENT_FILTER_DESCRIPTOR* filterData,
                [In] void* callbackContext
                );

            //
            // Registration APIs
            //
            [SecurityCritical]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventRegister", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe uint EventRegister(
                        [In] ref Guid providerId,
                        [In]EtwEnableCallback enableCallback,
                        [In]void* callbackContext,
                        [In][Out]ref long registrationHandle
                        );

            // 
            [SecurityCritical]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventUnregister", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern uint EventUnregister([In] long registrationHandle);

#if WB_OMIT
            //
            // Writing (Publishing/Logging) APIs
            //
            // 
            [SecurityCritical]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventWrite", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe int EventWrite(
                    [In] long registrationHandle,
                    [In] ref EventDescriptor eventDescriptor,
                    [In] int userDataCount,
                    [In] EventProvider.EventData* userData
                    );
#endif

            [SecurityCritical]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventWriteString", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe int EventWriteString(
                    [In] long registrationHandle,
                    [In] byte level,
                    [In] long keyword,
                    [In] string msg
                    );

            [StructLayout(LayoutKind.Sequential)]
            unsafe internal struct EVENT_FILTER_DESCRIPTOR
            {
                public long Ptr;
                public int Size;
                public int Type;
            };

#if WB_OMIT
            /// <summary>
            ///  Call the ETW native API EventWriteTransfer and checks for invalid argument error. 
            ///  The implementation of EventWriteTransfer on some older OSes (Windows 2008) does not accept null relatedActivityId.
            ///  So, for these cases we will retry the call with an empty Guid.
            /// </summary>
            internal static int EventWriteTransferWrapper(long registrationHandle,
                                                         ref EventDescriptor eventDescriptor,
                                                         Guid* activityId,
                                                         Guid* relatedActivityId,
                                                         int userDataCount,
                                                         EventProvider.EventData* userData)
            {
                int HResult = EventWriteTransfer(registrationHandle, ref eventDescriptor, activityId, relatedActivityId, userDataCount, userData);
                if (HResult == ERROR_INVALID_PARAMETER && relatedActivityId == null)
                {
                    Guid emptyGuid = Guid.Empty;
                    HResult = EventWriteTransfer(registrationHandle, ref eventDescriptor, activityId, &emptyGuid, userDataCount, userData);
                }

                return HResult;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventWriteTransfer", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
            private static extern int EventWriteTransfer(
                    [In] long registrationHandle,
                    [In] ref EventDescriptor eventDescriptor,
                    [In] Guid* activityId,
                    [In] Guid* relatedActivityId,
                    [In] int userDataCount,
                    [In] EventProvider.EventData* userData
                    );
#endif

            internal enum ActivityControl : uint
            {
                EVENT_ACTIVITY_CTRL_GET_ID = 1,
                EVENT_ACTIVITY_CTRL_SET_ID = 2,
                EVENT_ACTIVITY_CTRL_CREATE_ID = 3,
                EVENT_ACTIVITY_CTRL_GET_SET_ID = 4,
                EVENT_ACTIVITY_CTRL_CREATE_SET_ID = 5
            };

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventActivityIdControl", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
            internal static extern int EventActivityIdControl([In] ActivityControl ControlCode, [In][Out] ref Guid ActivityId);

            internal enum EVENT_INFO_CLASS
            {
                BinaryTrackInfo,
                SetEnableAllKeywords,
                SetTraits,
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventSetInformation", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
            internal static extern int EventSetInformation(
                [In] long registrationHandle,
                [In] EVENT_INFO_CLASS informationClass,
                [In] void* eventInformation,
                [In] int informationLength);

            // Support for EnumerateTraceGuidsEx
            internal enum TRACE_QUERY_INFO_CLASS
            {
                TraceGuidQueryList,
                TraceGuidQueryInfo,
                TraceGuidQueryProcess,
                TraceStackTracingInfo,
                MaxTraceSetInfoClass
            };

            internal struct TRACE_GUID_INFO
            {
                public int InstanceCount;
                public int Reserved;
            };

            internal struct TRACE_PROVIDER_INSTANCE_INFO
            {
                public int NextOffset;
                public int EnableCount;
                public int Pid;
                public int Flags;
            };

            internal struct TRACE_ENABLE_INFO
            {
                public int IsEnabled;
                public byte Level;
                public byte Reserved1;
                public ushort LoggerId;
                public int EnableProperty;
                public int Reserved2;
                public long MatchAnyKeyword;
                public long MatchAllKeyword;
            };

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EnumerateTraceGuidsEx", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
            internal static extern int EnumerateTraceGuidsEx(
                TRACE_QUERY_INFO_CLASS TraceQueryInfoClass,
                void* InBuffer,
                int InBufferSize,
                void* OutBuffer,
                int OutBufferSize,
                ref int ReturnLength);

        }
#if FEATURE_COMINTEROP
        [SecurityCritical]
        [DllImport("combase.dll", PreserveSig = true)]
        internal static extern int RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            [Out,MarshalAs(UnmanagedType.IInspectable)] out Object factory);
#endif

    }
}

// From reference source v4.6.1/System/compmod/microsoft/win32/UnsafeNativeMethods.cs

//------------------------------------------------------------------------------
// <copyright file="UnsafeNativeMethods.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace wbMicrosoft.Win32
{
#if !SILVERLIGHT
    [HostProtectionAttribute(MayLeakOnAbort = true)]
    [System.Security.SuppressUnmanagedCodeSecurity]
#endif // !SILVERLIGHT
    internal static partial class UnsafeNativeMethods
    {

        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern IntPtr GetStdHandle(int type);

#if !FEATURE_PAL && !FEATURE_CORESYSTEM
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage", Justification = "reviewed")]
        [DllImport(ExternDll.Gdi32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int GetSystemMetrics(int nIndex);
#endif // !FEATURE_PAL && !FEATURE_CORESYSTEM

#if !SILVERLIGHT       
        [DllImport(ExternDll.User32, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern IntPtr GetProcessWindowStation();
        [DllImport(ExternDll.User32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool GetUserObjectInformation(HandleRef hObj, int nIndex, [MarshalAs(UnmanagedType.LPStruct)] NativeMethods.USEROBJECTFLAGS pvBuffer, int nLength, ref int lpnLengthNeeded);
        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern IntPtr GetModuleHandle(string modName);
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool GetClassInfo(HandleRef hInst, string lpszClass, [In, Out] NativeMethods.WNDCLASS_I wc);

        [DllImport(ExternDll.User32, ExactSpelling = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool IsWindow(HandleRef hWnd);

        //SetClassLong won't work correctly for 64-bit: we should use SetClassLongPtr instead.  On
        //32-bit, SetClassLongPtr is just #defined as SetClassLong.  SetClassLong really should 
        //take/return int instead of IntPtr/HandleRef, but since we're running this only for 32-bit
        //it'll be OK.
        public static IntPtr SetClassLong(HandleRef hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 4)
            {
                return SetClassLongPtr32(hWnd, nIndex, dwNewLong);
            }
            return SetClassLongPtr64(hWnd, nIndex, dwNewLong);
        }
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto, EntryPoint = "SetClassLong")]
        [ResourceExposure(ResourceScope.None)]
        [SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable")]
        public static extern IntPtr SetClassLongPtr32(HandleRef hwnd, int nIndex, IntPtr dwNewLong);
        [SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")]
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto, EntryPoint = "SetClassLongPtr")]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr SetClassLongPtr64(HandleRef hwnd, int nIndex, IntPtr dwNewLong);

        //SetWindowLong won't work correctly for 64-bit: we should use SetWindowLongPtr instead.  On
        //32-bit, SetWindowLongPtr is just #defined as SetWindowLong.  SetWindowLong really should 
        //take/return int instead of IntPtr/HandleRef, but since we're running this only for 32-bit
        //it'll be OK.
        public static IntPtr SetWindowLong(HandleRef hWnd, int nIndex, HandleRef dwNewLong)
        {
            if (IntPtr.Size == 4)
            {
                return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
            }
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }
        [SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable")]
        [DllImport(ExternDll.User32, CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr SetWindowLongPtr32(HandleRef hWnd, int nIndex, HandleRef dwNewLong);
        [SuppressMessage("Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist")]
        [DllImport(ExternDll.User32, CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr SetWindowLongPtr64(HandleRef hWnd, int nIndex, HandleRef dwNewLong);


        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.None)]
        public static extern short RegisterClass(NativeMethods.WNDCLASS wc);
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.None)]
        public static extern short UnregisterClass(string lpClassName, HandleRef hInstance);
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true, BestFitMapping = true)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern IntPtr CreateWindowEx(int exStyle, string lpszClassName, string lpszWindowName, int style, int x, int y, int width,
                                              int height, HandleRef hWndParent, HandleRef hMenu, HandleRef hInst, [MarshalAs(UnmanagedType.AsAny)] object pvParam);
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr SendMessage(HandleRef hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern bool SetConsoleCtrlHandler(NativeMethods.ConHndlr handler, int add);
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern bool DestroyWindow(HandleRef hWnd);

        [DllImport(ExternDll.User32, ExactSpelling = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int MsgWaitForMultipleObjectsEx(int nCount, IntPtr pHandles, int dwMilliseconds, int dwWakeMask, int dwFlags);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int DispatchMessage([In] ref NativeMethods.MSG msg);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool PeekMessage([In, Out] ref NativeMethods.MSG msg, HandleRef hwnd, int msgMin, int msgMax, int remove);
        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern IntPtr SetTimer(HandleRef hWnd, HandleRef nIDEvent, int uElapse, HandleRef lpTimerProc);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool KillTimer(HandleRef hwnd, HandleRef idEvent);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool TranslateMessage([In, Out] ref NativeMethods.MSG msg);
        [DllImport(ExternDll.Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Ansi, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Process)]
        public static extern IntPtr GetProcAddress(HandleRef hModule, string lpProcName);

        [DllImport(ExternDll.User32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool PostMessage(HandleRef hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport(ExternDll.Wtsapi32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool WTSRegisterSessionNotification(HandleRef hWnd, int dwFlags);

        [DllImport(ExternDll.Wtsapi32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool WTSUnRegisterSessionNotification(HandleRef hWnd);

        private const int ERROR_INSUFFICIENT_BUFFER = 0x007A;
        private const int ERROR_NO_PACKAGE_IDENTITY = 0x3d54;

        // AppModel.h functions (Win8+)
        [DllImport(ExternDll.Kernel32, CharSet = CharSet.None, EntryPoint = "GetCurrentPackageId")]
        [System.Security.SecuritySafeCritical]
        [return: MarshalAs(UnmanagedType.I4)]
        private static extern Int32 _GetCurrentPackageId(ref Int32 pBufferLength, Byte[] pBuffer);

        // Copied from Win32Native.cs
        // Note - do NOT use this to call methods.  Use P/Invoke, which will
        // do much better things w.r.t. marshaling, pinning memory, security 
        // stuff, better interactions with thread aborts, etc.  This is used
        // solely by DoesWin32MethodExist for avoiding try/catch EntryPointNotFoundException
        // in scenarios where an OS Version check is insufficient
        [DllImport(ExternDll.Kernel32, CharSet = CharSet.Ansi, BestFitMapping = false, SetLastError = true, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, String methodName);

        [System.Security.SecurityCritical]  // auto-generated
        private static bool DoesWin32MethodExist(String moduleName, String methodName)
        {
            // GetModuleHandle does not increment the module's ref count, so we don't need to call FreeLibrary.
            IntPtr hModule = GetModuleHandle(moduleName);
            if (hModule == IntPtr.Zero)
            {
                Debug.Assert(hModule != IntPtr.Zero, "GetModuleHandle failed.  Dll isn't loaded?");
                return false;
            }
            IntPtr functionPointer = GetProcAddress(hModule, methodName);
            return (functionPointer != IntPtr.Zero);
        }

        [System.Security.SecuritySafeCritical]
        private static bool _IsPackagedProcess()
        {
            OperatingSystem os = Environment.OSVersion;
            if (os.Platform == PlatformID.Win32NT && os.Version >= new Version(6, 2, 0, 0) && DoesWin32MethodExist(ExternDll.Kernel32, "GetCurrentPackageId"))
            {
                Int32 bufLen = 0;
                // Will return ERROR_INSUFFICIENT_BUFFER when running within a packaged application,
                // and will return ERROR_NO_PACKAGE_IDENTITY otherwise.
                return _GetCurrentPackageId(ref bufLen, null) == ERROR_INSUFFICIENT_BUFFER;
            }
            else
            {   // We must be running on a downlevel OS.
                return false;
            }
        }

        [System.Security.SecuritySafeCritical]
        internal static Lazy<bool> IsPackagedProcess = new Lazy<bool>(() => _IsPackagedProcess());


        // File src\services\system\io\unsafenativemethods.cs

        public const int FILE_READ_DATA = (0x0001),
        FILE_LIST_DIRECTORY = (0x0001),
        FILE_WRITE_DATA = (0x0002),
        FILE_ADD_FILE = (0x0002),
        FILE_APPEND_DATA = (0x0004),
        FILE_ADD_SUBDIRECTORY = (0x0004),
        FILE_CREATE_PIPE_INSTANCE = (0x0004),
        FILE_READ_EA = (0x0008),
        FILE_WRITE_EA = (0x0010),
        FILE_EXECUTE = (0x0020),
        FILE_TRAVERSE = (0x0020),
        FILE_DELETE_CHILD = (0x0040),
        FILE_READ_ATTRIBUTES = (0x0080),
        FILE_WRITE_ATTRIBUTES = (0x0100),
        FILE_SHARE_READ = 0x00000001,
        FILE_SHARE_WRITE = 0x00000002,
        FILE_SHARE_DELETE = 0x00000004,
        FILE_ATTRIBUTE_READONLY = 0x00000001,
        FILE_ATTRIBUTE_HIDDEN = 0x00000002,
        FILE_ATTRIBUTE_SYSTEM = 0x00000004,
        FILE_ATTRIBUTE_DIRECTORY = 0x00000010,
        FILE_ATTRIBUTE_ARCHIVE = 0x00000020,
        FILE_ATTRIBUTE_NORMAL = 0x00000080,
        FILE_ATTRIBUTE_TEMPORARY = 0x00000100,
        FILE_ATTRIBUTE_COMPRESSED = 0x00000800,
        FILE_ATTRIBUTE_OFFLINE = 0x00001000,
        FILE_NOTIFY_CHANGE_FILE_NAME = 0x00000001,
        FILE_NOTIFY_CHANGE_DIR_NAME = 0x00000002,
        FILE_NOTIFY_CHANGE_ATTRIBUTES = 0x00000004,
        FILE_NOTIFY_CHANGE_SIZE = 0x00000008,
        FILE_NOTIFY_CHANGE_LAST_WRITE = 0x00000010,
        FILE_NOTIFY_CHANGE_LAST_ACCESS = 0x00000020,
        FILE_NOTIFY_CHANGE_CREATION = 0x00000040,
        FILE_NOTIFY_CHANGE_SECURITY = 0x00000100,
        FILE_ACTION_ADDED = 0x00000001,
        FILE_ACTION_REMOVED = 0x00000002,
        FILE_ACTION_MODIFIED = 0x00000003,
        FILE_ACTION_RENAMED_OLD_NAME = 0x00000004,
        FILE_ACTION_RENAMED_NEW_NAME = 0x00000005,
        FILE_CASE_SENSITIVE_SEARCH = 0x00000001,
        FILE_CASE_PRESERVED_NAMES = 0x00000002,
        FILE_UNICODE_ON_DISK = 0x00000004,
        FILE_PERSISTENT_ACLS = 0x00000008,
        FILE_FILE_COMPRESSION = 0x00000010,
        OPEN_EXISTING = 3,
        OPEN_ALWAYS = 4,
        FILE_FLAG_WRITE_THROUGH = unchecked((int)0x80000000),
        FILE_FLAG_OVERLAPPED = 0x40000000,
        FILE_FLAG_NO_BUFFERING = 0x20000000,
        FILE_FLAG_RANDOM_ACCESS = 0x10000000,
        FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000,
        FILE_FLAG_DELETE_ON_CLOSE = 0x04000000,
        FILE_FLAG_BACKUP_SEMANTICS = 0x02000000,
        FILE_FLAG_POSIX_SEMANTICS = 0x01000000,
        FILE_TYPE_UNKNOWN = 0x0000,
        FILE_TYPE_DISK = 0x0001,
        FILE_TYPE_CHAR = 0x0002,
        FILE_TYPE_PIPE = 0x0003,
        FILE_TYPE_REMOTE = unchecked((int)0x8000),
        FILE_VOLUME_IS_COMPRESSED = 0x00008000;

        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage", Justification = "reviewed")]
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        public extern static int LookupAccountSid(string systemName, byte[] pSid, StringBuilder szUserName, ref int userNameSize, StringBuilder szDomainName, ref int domainNameSize, ref int eUse);

        public const int GetFileExInfoStandard = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct WIN32_FILE_ATTRIBUTE_DATA
        {
            internal int fileAttributes;
            internal uint ftCreationTimeLow;
            internal uint ftCreationTimeHigh;
            internal uint ftLastAccessTimeLow;
            internal uint ftLastAccessTimeHigh;
            internal uint ftLastWriteTimeLow;
            internal uint ftLastWriteTimeHigh;
            internal uint fileSizeHigh;
            internal uint fileSizeLow;
        }

        // file Windows Forms
        [DllImport(ExternDll.Version, CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern int GetFileVersionInfoSize(string lptstrFilename, out int handle);
        [DllImport(ExternDll.Version, CharSet = CharSet.Auto, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern bool GetFileVersionInfo(string lptstrFilename, int dwHandle, int dwLen, HandleRef lpData);
        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
        [SuppressMessage("Microsoft.Security", "CA2101:SpecifyMarshalingForPInvokeStringArguments")]
        [DllImport(ExternDll.Kernel32, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern int GetModuleFileName(HandleRef hModule, StringBuilder buffer, int length);
        [DllImport(ExternDll.Version, CharSet = CharSet.Auto, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Process)]   // Review usages to see if versioning problems exist in distant callers
        public static extern bool VerQueryValue(HandleRef pBlock, string lpSubBlock, [In, Out] ref IntPtr lplpBuffer, out int len);
        [DllImport(ExternDll.Version, CharSet = CharSet.Auto, BestFitMapping = true)]
        [ResourceExposure(ResourceScope.None)]
        public static extern int VerLanguageName(int langID, StringBuilder lpBuffer, int nSize);

        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern bool ReportEvent(SafeHandle hEventLog, short type, ushort category,
                                                uint eventID, byte[] userSID, short numStrings, int dataLen, HandleRef strings,
                                                byte[] rawData);
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        public static extern bool ClearEventLog(SafeHandle hEventLog, HandleRef lpctstrBackupFileName);
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool GetNumberOfEventLogRecords(SafeHandle hEventLog, out int count);
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage", Justification = "[....]: EventLog is protected by EventLogPermission")]
        public static extern bool GetOldestEventLogRecord(SafeHandle hEventLog, out int number);
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage", Justification = "[....]: EventLog is protected by EventLogPermission")]
        public static extern bool ReadEventLog(SafeHandle hEventLog, int dwReadFlags,
                                                 int dwRecordOffset, byte[] buffer, int numberOfBytesToRead, out int bytesRead,
                                                 out int minNumOfBytesNeeded);
        [DllImport(ExternDll.Advapi32, CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        [return: MarshalAs(UnmanagedType.Bool)]
        [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage", Justification = "[....]: EventLog is protected by EventLogPermission")]
        public static extern bool NotifyChangeEventLog(SafeHandle hEventLog, SafeWaitHandle hEvent);

        [DllImport(ExternDll.Kernel32, EntryPoint = "ReadDirectoryChangesW", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        public unsafe static extern bool ReadDirectoryChangesW(SafeFileHandle hDirectory, HandleRef lpBuffer,
                                                                int nBufferLength, int bWatchSubtree, int dwNotifyFilter, out int lpBytesReturned,
                                                                NativeOverlapped* overlappedPointer, HandleRef lpCompletionRoutine);

        //////////////////// Serial Port structs ////////////////////
        // Declaration for C# representation of Win32 Device Control Block (DCB)
        // structure.  Note that all flag properties are encapsulated in the Flags field here,
        // and accessed/set through SerialStream's GetDcbFlag(...) and SetDcbFlag(...) methods.
        internal struct DCB
        {

            public uint DCBlength;
            public uint BaudRate;
            public uint Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public byte XonChar;
            public byte XoffChar;
            public byte ErrorChar;
            public byte EofChar;
            public byte EvtChar;
            public ushort wReserved1;
        }

        // Declaration for C# representation of Win32 COMSTAT structure associated with
        // a file handle to a serial communications resource.  SerialStream's
        // InBufferBytes and OutBufferBytes directly expose cbInQue and cbOutQue to reading, respectively.
        internal struct COMSTAT
        {
            public uint Flags;
            public uint cbInQue;
            public uint cbOutQue;
        }

        // Declaration for C# representation of Win32 COMMTIMEOUTS
        // structure associated with a file handle to a serial communications resource.
        ///Currently the only set fields are ReadTotalTimeoutConstant
        // and WriteTotalTimeoutConstant.
        internal struct COMMTIMEOUTS
        {
            public int ReadIntervalTimeout;
            public int ReadTotalTimeoutMultiplier;
            public int ReadTotalTimeoutConstant;
            public int WriteTotalTimeoutMultiplier;
            public int WriteTotalTimeoutConstant;
        }

        // Declaration for C# representation of Win32 COMMPROP
        // structure associated with a file handle to a serial communications resource.
        // Currently the only fields used are dwMaxTxQueue, dwMaxRxQueue, and dwMaxBaud
        // to ensure that users provide appropriate settings to the SerialStream constructor.
        internal struct COMMPROP
        {
            public ushort wPacketLength;
            public ushort wPacketVersion;
            public int dwServiceMask;
            public int dwReserved1;
            public int dwMaxTxQueue;
            public int dwMaxRxQueue;
            public int dwMaxBaud;
            public int dwProvSubType;
            public int dwProvCapabilities;
            public int dwSettableParams;
            public int dwSettableBaud;
            public ushort wSettableData;
            public ushort wSettableStopParity;
            public int dwCurrentTxQueue;
            public int dwCurrentRxQueue;
            public int dwProvSpec1;
            public int dwProvSpec2;
            public char wcProvChar;
        }
        //////////////////// end Serial Port structs ////////////////////

        //////////////////// Serial Port methods ////////////////////



        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern SafeFileHandle CreateFile(String lpFileName,
            int dwDesiredAccess, int dwShareMode,
            IntPtr securityAttrs, int dwCreationDisposition,
            int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool GetCommState(
            SafeFileHandle hFile,  // handle to communications device
            ref DCB lpDCB    // device-control block
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool SetCommState(
            SafeFileHandle hFile,  // handle to communications device
            ref DCB lpDCB    // device-control block
            );


        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool GetCommModemStatus(
            SafeFileHandle hFile,        // handle to communications device
            ref int lpModemStat  // control-register values
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool SetupComm(
            SafeFileHandle hFile,     // handle to communications device
            int dwInQueue,  // size of input buffer
            int dwOutQueue  // size of output buffer
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool SetCommTimeouts(
            SafeFileHandle hFile,                  // handle to comm device
            ref COMMTIMEOUTS lpCommTimeouts  // time-out values
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool SetCommBreak(
            SafeFileHandle hFile                 // handle to comm device
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool ClearCommBreak(
            SafeFileHandle hFile                 // handle to comm device
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool ClearCommError(
            SafeFileHandle hFile,                 // handle to comm device
            ref int lpErrors,
            ref COMSTAT lpStat
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool ClearCommError(
            SafeFileHandle hFile,                 // handle to comm device
            ref int lpErrors,
            IntPtr lpStat
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool PurgeComm(
            SafeFileHandle hFile,  // handle to communications resource
            uint dwFlags  // action to perform
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool GetCommProperties(
            SafeFileHandle hFile,           // handle to comm device
            ref COMMPROP lpCommProp   // communications properties
            );

        // All actual file Read/Write methods, which are declared to be unsafe.
        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, IntPtr numBytesRead, NativeOverlapped* overlapped);

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, out int numBytesRead, IntPtr overlapped);

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, IntPtr numBytesWritten, NativeOverlapped* lpOverlapped);

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern int WriteFile(SafeFileHandle handle, byte* bytes, int numBytesToWrite, out int numBytesWritten, IntPtr lpOverlapped);

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern int GetFileType(
            SafeFileHandle hFile   // handle to file
            );
        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool EscapeCommFunction(
            SafeFileHandle hFile, // handle to communications device
            int dwFunc      // extended function to perform
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern bool WaitCommEvent(
            SafeFileHandle hFile,                // handle to comm device
            int* lpEvtMask,                      // event type
            NativeOverlapped* lpOverlapped       // overlapped structure
            );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern bool SetCommMask(
            SafeFileHandle hFile,
            int dwEvtMask
        );

        [DllImport(ExternDll.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        unsafe internal static extern bool GetOverlappedResult(
            SafeFileHandle hFile,
            NativeOverlapped* lpOverlapped,
            ref int lpNumberOfBytesTransferred,
            bool bWait
        );


        //////////////////// end Serial Port methods ////////////////////

        [DllImport(ExternDll.Advapi32, CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern
        bool GetTokenInformation(
            [In]  IntPtr TokenHandle,
            [In]  uint TokenInformationClass,
            [In]  IntPtr TokenInformation,
            [In]  uint TokenInformationLength,
            [Out] out uint ReturnLength);

        internal const int TokenIsAppContainer = 29;


        [ComImport, Guid("00000003-0000-0000-C000-000000000046"),
        InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMarshal
        {
            [PreserveSig]
            int GetUnmarshalClass(
                ref Guid riid,
                IntPtr pv,
                int dwDestContext,
                IntPtr pvDestContext,
                int mshlflags,
                out Guid pCid);

            [PreserveSig]
            int GetMarshalSizeMax(
                ref Guid riid,
                IntPtr pv,
                int dwDestContext,
                IntPtr pvDestContext,
                int mshlflags,
                out int pSize);

            [PreserveSig]
            int MarshalInterface(
                IntPtr pStm,
                ref Guid riid,
                IntPtr pv,
                int dwDestContext,
                IntPtr pvDestContext,
                int mshlflags);

            [PreserveSig]
            int UnmarshalInterface(
                IntPtr pStm,
                ref Guid riid,
                out IntPtr ppv);

            [PreserveSig]
            int ReleaseMarshalData(IntPtr pStm);

            [PreserveSig]
            int DisconnectObject(int dwReserved);
        }


        [DllImport(ExternDll.Ole32)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern int CoGetStandardMarshal(
                ref Guid riid,
                IntPtr pv,
                int dwDestContext,
                IntPtr pvDestContext,
                int mshlflags,
                out IntPtr ppMarshal
        );

#endif // !SILVERLIGHT

    }
}


