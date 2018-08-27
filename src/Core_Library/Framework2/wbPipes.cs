using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Diagnostics;
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

namespace wb
{
    public class ReadPipe : IDisposable
    {
        SafeHandle Handle;

        public ReadPipe(SafeHandle Handle)
        {
            this.Handle = Handle;
        }

        public void Dispose()
        {
            if (Handle != null)
            {
                Handle.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            if (Handle != null) Handle.Close();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool PeekNamedPipe(SafeHandle hNamedPipe, IntPtr lpBuffer, uint nBufferSize, IntPtr lpBytesRead, ref UInt32 lpTotalBytesAvail, IntPtr lpBytesLeftThisMessage);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(SafeHandle hPipe, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out UInt32 lpNumberOfBytesRead, IntPtr lpOverlapped);

        public int NonBlockingRead(byte[] Buffer, int MaxCount)
        {
            if (Handle.IsClosed || Handle.IsInvalid) throw new Exception("Pipe handle closed or invalid at non-blocking read attempt.");

            UInt32 TotalBytesAvailable = 0;
            if (!PeekNamedPipe(Handle, IntPtr.Zero, 0, IntPtr.Zero, ref TotalBytesAvailable, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (TotalBytesAvailable == 0) return 0;

            UInt32 NumberOfBytesRead = 0;
            if (!ReadFile(Handle, Buffer, (uint)MaxCount, out NumberOfBytesRead, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return (int)NumberOfBytesRead;
    	}
    }

    public class WritePipe : IDisposable
    {
        SafeHandle Handle;

        public WritePipe(SafeHandle Handle)
        {
            this.Handle = Handle;
        }

        public void Dispose()
        {
            if (Handle != null)
            {
                Handle.Dispose();
            }
            GC.SuppressFinalize(this);
        }

        public void Close()
        {
            if (Handle != null) Handle.Close();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(SafeHandle hPipe, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        public void Write(byte[] Buffer, int Count)
        {
            if (Handle.IsClosed || Handle.IsInvalid) throw new Exception("Pipe handle closed or invalid at write attempt.");            

            UInt32 NumberOfBytesWritten = 0;
            if (!WriteFile(Handle, Buffer, (uint)Count, out NumberOfBytesWritten, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (NumberOfBytesWritten < Count)
                throw new Exception("Failed to write complete message to pipe.");
        }
    }
}
