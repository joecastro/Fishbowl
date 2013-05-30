// Things to consider:
// * Identify the exception and only ask the user to report it once.
// * Support populating an email rather than displaying a file.
// * Add application extensible properties
// * Use HTML for formatting.
namespace BlackBox
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using Standard;

    /// <summary>
    /// A localized version of the Microsoft Watson service.
    /// </summary>
    public static class DoctorWilson
    {
        private static Dictionary<string, string> _entries;

        public static bool IsInstalled { get; private set; }

        //public static bool IncludePersonallyIdentifiableInformation { get; set; }

        public static string Preamble { get; set; }

        public static bool InitializeReporting()
        {
            // This should only be done once for the AppDomain.
            Verify.IsFalse(IsInstalled, "IsInstalled");

            // Don't bother with this if someone's debugging,
            // if for no other reason then that the deployment stuff throws first change exceptions
            // just to query basic properties.
            if (Debugger.IsAttached)
            {
                return false;
            }

            AppDomain.CurrentDomain.UnhandledException += _OnUnhandledException;

            // Consider also supporting personally identifiable information as well, e.g.:
            // UserName, UserEmailAddress, MachineName, Domain, etc...
            _entries = new Dictionary<string, string>
            {
                { "Processor Count", Environment.ProcessorCount.ToString() },
                { "OS Version", Environment.OSVersion.ToString() },
                { "Processor Architecture", Environment.GetEnvironmentVariable("Processor_Architecture") },
                { "Runtime Version", Environment.Version.ToString() },
                { "Process Name", Process.GetCurrentProcess().ProcessName },
                { "Application Version", Assembly.GetEntryAssembly().GetName().Version.ToString() },
                { "Working Set", (Environment.WorkingSet / (1024 * 1024)).ToString() + "MB" },
                { "Command Line", Environment.CommandLine },
                { "Deployment Version", System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed ? System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() : "Not Network Deployed" },
            };

            IsInstalled = true;
            return true;
        }

        private static void _OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Add data about the current state of the world.
            _entries["Has Shutdown Started"] = Environment.HasShutdownStarted.ToString();

            string errorFile = Path.GetTempFileName();
            using (var tw = new StreamWriter(errorFile))
            {
                if (!string.IsNullOrEmpty(Preamble))
                {
                    tw.WriteLine(Preamble);
                }

                tw.WriteLine("Environment Info:");

                foreach (var dataPair in _entries)
                {
                    tw.WriteLine("\t{0}: {1}", dataPair.Key, dataPair.Value);
                }

                tw.WriteLine(Environment.NewLine + "Exception Information:" + Environment.NewLine);
                tw.WriteLine(e.ExceptionObject.ToString());
            }

            string newErrorFile = Path.ChangeExtension(errorFile, ".txt");
            File.Move(errorFile, newErrorFile);

            // Display this file for the user
            Process.Start(new ProcessStartInfo { FileName = newErrorFile });
        }
    }
}
