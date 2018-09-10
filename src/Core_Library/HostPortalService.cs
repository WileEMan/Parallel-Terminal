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
    }    
}
