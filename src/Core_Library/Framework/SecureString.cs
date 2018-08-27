// From reference source v4.6.1/mscorlib/system/security/securestring.cs

using System.Security.Cryptography;
using System.Runtime.InteropServices;
#if FEATURE_CORRUPTING_EXCEPTIONS
    using System.Runtime.ExceptionServices;
#endif // FEATURE_CORRUPTING_EXCEPTIONS
using System.Text;
using wbMicrosoft.Win32;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics.Contracts;

// <OWNER>[....]</OWNER>
namespace System.Security
{
    using Buffer = wbSystem.Buffer;

    [System.Security.SecurityCritical]  // auto-generated
    [SuppressUnmanagedCodeSecurityAttribute()]
    internal sealed class SafeBSTRHandle : SafeBuffer
    {
        internal SafeBSTRHandle() : base(true) { }

        internal static SafeBSTRHandle Allocate(String src, uint len)
        {
            SafeBSTRHandle bstr = SysAllocStringLen(src, len);
            bstr.Initialize(len * sizeof(char));
            return bstr;
        }

        [ResourceExposure(ResourceScope.None)]
        [DllImport(Win32Native.OLEAUT32, CharSet = CharSet.Unicode)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private static extern SafeBSTRHandle SysAllocStringLen(String src, uint len);  // BSTR

        [System.Security.SecurityCritical]
        override protected bool ReleaseHandle()
        {
            Win32Native.ZeroMemory(handle, (UIntPtr)(Win32Native.SysStringLen(handle) * 2));
            Win32Native.SysFreeString(handle);
            return true;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        internal unsafe void ClearBuffer()
        {
            byte* bufferPtr = null;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                AcquirePointer(ref bufferPtr);
                Win32Native.ZeroMemory((IntPtr)bufferPtr, (UIntPtr)(Win32Native.SysStringLen((IntPtr)bufferPtr) * 2));
            }
            finally
            {
                if (bufferPtr != null)
                    ReleasePointer();
            }
        }


        internal unsafe int Length
        {
            get
            {
                return (int)Win32Native.SysStringLen(this);
            }
        }

        internal unsafe static void Copy(SafeBSTRHandle source, SafeBSTRHandle target)
        {
            byte* sourcePtr = null, targetPtr = null;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                source.AcquirePointer(ref sourcePtr);
                target.AcquirePointer(ref targetPtr);

                Contract.Assert(Win32Native.SysStringLen((IntPtr)targetPtr) >= Win32Native.SysStringLen((IntPtr)sourcePtr), "Target buffer is not large enough!");

                Buffer.Memcpy(targetPtr, sourcePtr, (int)Win32Native.SysStringLen((IntPtr)sourcePtr) * 2);
            }
            finally
            {
                if (sourcePtr != null)
                    source.ReleasePointer();
                if (targetPtr != null)
                    target.ReleasePointer();
            }
        }
    }
}
