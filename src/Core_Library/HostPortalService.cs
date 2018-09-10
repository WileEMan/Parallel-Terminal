using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
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
    public partial class HostPortalService : ServiceBase, Core_Library.ILogger
    {
        int EventId = 0;
        EventLog EventLogger;
        SlaveCore Core;

        void ILogger.WriteLine(string Message, Severity Severity)
        {
            lock (EventLogger)
            {
                switch (Severity)
                {
                    case Severity.Debug: break;
                    case Severity.Information: EventLogger.WriteEntry(Message, System.Diagnostics.EventLogEntryType.Information, EventId++); break;
                    case Severity.Warning: EventLogger.WriteEntry(Message, System.Diagnostics.EventLogEntryType.Warning, EventId++); break;
                    default:
                    case Severity.Error: EventLogger.WriteEntry(Message, System.Diagnostics.EventLogEntryType.Error, EventId++); break;
                }
            }
        }

        static string EventLogSource = "Parallel Terminal Host Portal";
        public HostPortalService()
        {
            InitializeComponent();

            EventLogger = new EventLog();
            if (!EventLog.SourceExists(EventLogSource))
            {
                EventLog.CreateEventSource(EventLogSource, "Application");
            }
            EventLogger.Source = EventLogSource;
            EventLogger.Log = "Application";
        }

        protected override void OnStart(string[] args)
        {
            Core = new SlaveCore(this, false);            
        }

        protected override void OnStop()
        {
            if (Core != null) { Core.Stop(); Core = null; }
        }

        /// <summary>
        /// Call TriggerFirewallWarnings() from the installation process in order to momentarily trigger the firewall warning messages that must be accepted on Windows 7 for
        /// the service to operate.
        /// </summary>
        public void TriggerFirewallWarnings()
        {
            TcpListener listenerV4 = new TcpListener(IPAddress.Any, CommonCore.PortNumber);
            TcpListener listenerV6 = new TcpListener(IPAddress.IPv6Any, CommonCore.PortNumber);
            listenerV4.Start(10);
            listenerV6.Start(10);

            // Start listening for connections, but close them immediately if received.

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 15000)
            {
                TcpClient ClientRequest;
                try
                {
                    if (listenerV4.Pending())
                        ClientRequest = listenerV4.AcceptTcpClient();
                    else if (listenerV6.Pending())
                        ClientRequest = listenerV6.AcceptTcpClient();
                    else { Thread.Sleep(250); continue; }

                    ClientRequest.Close();
                }
                catch (Exception)
                {
                }
            }
        }
    }    
}
