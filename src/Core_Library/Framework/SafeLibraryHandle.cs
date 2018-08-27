// From reference source v4.6.1/mscorlib/microsoft/win32/safehandles/safelibraryhandle.cs

// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
/*============================================================
**
** Class: SafeLibraryHandle
**
============================================================*/

using wbMicrosoft.Win32;
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

namespace wbMicrosoft.Win32
{    
    [System.Security.SecurityCritical]  // auto-generated
    [HostProtectionAttribute(MayLeakOnAbort = true)]
    sealed internal class SafeLibraryHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeLibraryHandle() : base(true) { }

        [System.Security.SecurityCritical]
        override protected bool ReleaseHandle()
        {
            return UnsafeNativeMethods.FreeLibrary(handle);
        }
    }
}
