using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Principal;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Core_Library;

namespace Host_Portal
{
    /// <summary>
    /// HostPortalConsole is very similar to HostPortalService, but the Console can be launched from the command-line and displays event logs on the console instead of the
    /// event logger.  It is intended for debugging, allowing us to run what would be the service directly, outside of a service, so that we can watch outputs.  It displays
    /// debug log messages that the service would discard.
    /// </summary>
    public class HostPortalConsole : Core_Library.ILogger, IDisposable
    {        
        SlaveCore Core;

        void ILogger.WriteLine(string Message, Severity Severity)
        {
            lock (this)
            {
                Console.WriteLine(DateTime.Now.ToString() + ": " + Message);
            }
        }
        
        public HostPortalConsole()
        {
        }

        public void Start()
        {
            Core = new SlaveCore(this, false);
        }

        public void Stop()
        {
            if (Core != null) { Core.Stop(); Core = null; }
        }

        bool Disposed = false;
        public void Dispose()
        {
            if (!Disposed)
            {
                Stop();
                Disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
