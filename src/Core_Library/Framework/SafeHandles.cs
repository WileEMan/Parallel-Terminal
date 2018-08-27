// Safe handles collected from multiple reference source v4.6.1 source files

/**** From reference source v4.6.1/mscorlib/microsoft/win32/safehandles/safefindhandle.cs *****/

// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  SafeFindHandle 
**
**
** A wrapper for find handles
**
** 
===========================================================*/

namespace wbMicrosoft.Win32.SafeHandles
{
    using System;
    using System.Security;
    using System.Security.Permissions;
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices;
    using System.Runtime.ConstrainedExecution;
    using Microsoft.Win32;

    [System.Security.SecurityCritical]  // auto-generated
    internal sealed class SafeFindHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        [System.Security.SecurityCritical]  // auto-generated_required
        internal SafeFindHandle() : base(true) { }

        [System.Security.SecurityCritical]
        override protected bool ReleaseHandle()
        {
            return Win32Native.FindClose(handle);
        }
    }
}

/***** From reference source v4.6.1/System/compmod/microsoft/win32/safehandles/SafeProcessHandle.cs *****/

// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class:  SafeProcessHandle 
**
** A wrapper for a process handle
**
** 
===========================================================*/

namespace wbMicrosoft.Win32.SafeHandles
{
    using ExternDll = wbSystem.ExternDll;
    using System;
    using System.Security;
    using System.Diagnostics;
    using System.Security.Permissions;
    using System.Runtime.InteropServices;
    using System.Runtime.CompilerServices;
    using Microsoft.Win32;
    using Microsoft.Win32.SafeHandles;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.Versioning;

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
            return SafeNativeMethods.CloseHandle(handle);
        }

    }
}




