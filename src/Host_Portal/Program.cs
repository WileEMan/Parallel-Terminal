/** To install, launch the executable with 'install' command-line option.
 *  To uninstall, launch with 'uninstall' command-line option.
 */

using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Reflection;
using System.Configuration.Install;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

namespace Host_Portal
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return System.IO.Path.GetDirectoryName(path);
            }
        }

        static string EventLogSource = "Parallel Terminal Host Portal";

        static int EventId = 0;
        static EventLog EventLogger = null;

        /// <summary>
        /// We prefer event logging be done in the service code so that it can be updated.  But, if we have in issue loading it,
        /// a log here is better than nothing.
        /// </summary>
        static void EmergencyEventLog(string ErrorMessage)
        {
            if (EventLogger == null)
            {
                EventLogger = new EventLog();
                if (!EventLog.SourceExists(EventLogSource))
                {
                    EventLog.CreateEventSource(EventLogSource, "Application");
                }
                EventLogger.Source = EventLogSource;
                EventLogger.Log = "Application";
            }
            EventLogger.WriteEntry(ErrorMessage, System.Diagnostics.EventLogEntryType.Error, EventId++);
        }

        static void UpdateFile(string WorkingFile, string UpdateFile)
        {
            string WorkingPath = System.IO.Path.Combine(AssemblyDirectory, WorkingFile);
            string UpdatePath = System.IO.Path.Combine(AssemblyDirectory, UpdateFile);

            if (System.IO.File.Exists(UpdatePath))
            {
                if (System.IO.File.Exists(WorkingPath)) System.IO.File.Delete(WorkingPath);
                System.IO.File.Move(UpdatePath, WorkingPath);
            }
        }

        static void UpdateServiceCode()
        {
            UpdateFile("Core_Library.dll", "Core_Library_Update.dll");
            UpdateFile("Core_Library.pdb", "Core_Library_Update.pdb");
        }

        /// <summary>
        /// 1. First, we check for a Core_Library_Update.dll file and replace Core_Library.dll if it exists.
        /// 2. We load the Core_Library.dll into this application domain.
        /// 3. We return the service entry-point for HostPortalService.
        /// 
        /// This approach facilitates a self-update by providing a Core_Library_Update.dll file in the same
        /// directory and then rebooting so that this check/reload will take place.
        /// </summary>
        /// <returns></returns>
        static ServiceBase LoadServiceCode()
        {
            var asm = Assembly.LoadFile(System.IO.Path.Combine(AssemblyDirectory, "Core_Library.dll"));
            var type = asm.GetType("Host_Portal.HostPortalService");
            ServiceBase runnable = Activator.CreateInstance(type) as HostPortalService;
            if (runnable == null) throw new Exception("Unable to load Core_Library.dll or entry-point service class.");
            return runnable;
        }

        /// <summary>
        /// 1. First, we check for a Core_Library_Update.dll file and replace Core_Library.dll if it exists.
        /// 2. We load the Core_Library.dll into this application domain.
        /// 3. We return the console entry-point for HostPortalConsole.
        /// 
        /// This approach facilitates a self-update by providing a Core_Library_Update.dll file in the same
        /// directory and then rebooting so that this check/reload will take place.
        /// </summary>
        /// <returns></returns>
        static Host_Portal.HostPortalConsole LoadConsoleCode()
        {
            var asm = Assembly.LoadFile(System.IO.Path.Combine(AssemblyDirectory, "Core_Library.dll"));
            var type = asm.GetType("Host_Portal.HostPortalConsole");
            Host_Portal.HostPortalConsole runnable = Activator.CreateInstance(type) as HostPortalConsole;
            if (runnable == null) throw new Exception("Unable to load Core_Library.dll or entry-point console class.");
            return runnable;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            bool IsInstalled = false;
            string SERVICE_NAME = "HostPortalService";

            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                if (service.ServiceName.Equals(SERVICE_NAME))
                {
                    IsInstalled = true;
                    if (service.Status == ServiceControllerStatus.StartPending)
                    {
                        // Started from the SCM, so launch the service.
                        System.ServiceProcess.ServiceBase[] servicestorun;
                        try
                        {
                            UpdateServiceCode();
                            ServiceBase Core = LoadServiceCode();
                            servicestorun = new System.ServiceProcess.ServiceBase[] { Core };
                        }
                        catch (Exception ex)
                        {
                            // Attempt to log this...
                            EmergencyEventLog("Error launching service core: " + ex.ToString());
                            return;
                        }
                        try
                        {
                            ServiceBase.Run(servicestorun);
                            return;
                        }
                        catch (Exception ex)
                        {
                            // An unhandled exception?  Try to log this...
                            EmergencyEventLog("Unhandled error in service: " + ex.ToString());
                            return;
                        }
                    }
                    break;
                }
            }

            // We weren't called from the SCM with StartPending, so the user wants to install or uninstall us.  We'll need a command-line argument to know.

            // redirect console output to parent process;
            // must be before any calls to Console.WriteLine()
            AttachConsole(ATTACH_PARENT_PROCESS);

            try
            {
                Console.WriteLine("");
                Console.WriteLine("Not being launched from SCM.");
                
                try
                {
                    Console.WriteLine("Checking for administrative permissions...");
                    System.Diagnostics.EventLog.SourceExists(EventLogSource);
                }
                catch (System.Security.SecurityException se)
                {
                    Console.WriteLine("Exception: " + se.ToString());
                    throw new Exception("You must install/uninstall with administrative permission.");
                }
                Console.WriteLine("");

                string Command = "";
                if (args.Length > 0) Command = args[0];

                if (Command == "install")
                {
                    if (IsInstalled)
                    {
                        throw new Exception("Already installed.");
                    }
                    else
                    {
                        try
                        {
                            Console.WriteLine("");
                            Console.WriteLine("Starting installation...");

                            Console.WriteLine("");
                            Console.WriteLine("Connecting to core DLL...");
                            UpdateServiceCode();
                            Host_Portal.HostPortalService Core = LoadServiceCode() as Host_Portal.HostPortalService;

                            Console.WriteLine("");
                            Console.WriteLine("Testing network connectivity and triggering firewall allowances request...");
                            Core.TriggerFirewallWarnings();

                            Console.WriteLine("");
                            Console.WriteLine("Starting service installation...");
                            if (!SelfInstaller.InstallMe())
                                Console.WriteLine("Installation failure reported/logged.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("Installation failed: " + ex.Message);
                        }
                    }
                }
                else if (Command == "uninstall")
                {
                    if (!IsInstalled)
                    {
                        throw new Exception("Wasn't installed.");
                    }
                    else
                    {
                        if (!SelfInstaller.UninstallMe())
                            Console.WriteLine("Failure reported.");
                    }
                }
                else if (Command == "test")
                {
                    Console.WriteLine("Testing DLL update...");
                    UpdateServiceCode();
                    Console.WriteLine("Testing DLL load...");
                    LoadServiceCode();
                }
                else if (Command == "console")
                {
                    Console.WriteLine("WARNING: Console mode doesn't really work because the command-line run already has a console, and an app can be attached to at most one console.");
                    Console.WriteLine("Launching in console mode...");                    
                    try
                    {
                        UpdateServiceCode();
                        using (Host_Portal.HostPortalConsole Core = LoadConsoleCode())
                        {
                            Core.Start();
                            Console.WriteLine("Console launched.  Press X key to exit.");
                            for (;;)
                            {
                                if (Console.KeyAvailable)
                                {
                                    ConsoleKeyInfo ki = Console.ReadKey();
                                    if (ki.KeyChar == 'X' || ki.KeyChar == 'x') break;                                    
                                }
                                Thread.Sleep(50);
                            }
                            Console.WriteLine("Exitting...");
                            Core.Stop();
                            Console.WriteLine("Console stopped successfully.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unhandled exception coming from the host portal console: " + ex.ToString());
                        return;
                    }                    
                }
                else throw new Exception("Either 'install' or 'uninstall' argument required.");
            }            
            catch (Exception ex)
            {
                Console.WriteLine("");
                Console.WriteLine("Error: " + ex.ToString());
            }

            Console.WriteLine("You probably need to hit enter to see your command prompt again.");
            Console.WriteLine("You may need to reboot for full effect.");
            Console.WriteLine("");
        }
    }

    public static class SelfInstaller
    {
        private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;
        public static bool InstallMe()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(
                    new string[] { _exePath });
            }
            catch
            {
                return false;
            }
            return true;
        }

        public static bool UninstallMe()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(
                    new string[] { "/u", _exePath });
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
