using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.ServiceProcess;
using System.IO;
using APSIM.Shared.Utilities;
using System.Runtime.InteropServices;
using System.Xml;
using System.Reflection;

namespace APSIM.Cloud.Runner
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            EnsureBuildNumberIsPutIntoSummaryFile();

            Dictionary<string,string> arguments = StringUtilities.ParseCommandLine(args);
            // If there is a -Service switch then start as a service otherwise start as a regular application
            if (arguments.ContainsKey("-Service"))
            {
                int maximumNumberOfCores = -1;
                if (arguments.ContainsKey("-MaximumNumberOfCores"))
                    maximumNumberOfCores = Convert.ToInt32(arguments["-MaximumNumberOfCores"]);

                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new RunnerService(maximumNumberOfCores)
                };
                ServiceBase.Run(ServicesToRun);
            }
            else
            {
                if (!RunJobFromCommandLine(args))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm(args));
                }
            }
        }

        /// <summary>Modify the apsim settings file to ensure summary file contains the apsim revision number.</summary>
        private static void EnsureBuildNumberIsPutIntoSummaryFile()
        {
            string settingsFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                                   "APSIM",
                                                   "Apsim.xml");
            XmlDocument doc = new XmlDocument();
            doc.Load(settingsFileName);
            XmlUtilities.SetValue(doc.DocumentElement, "ApsimUI/IncludeBuildNumberInOutSumFile", "Yes");
            doc.Save(settingsFileName);

            // Delete the cached version of the file we modified above.
            string cacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                             "APSIM");
            if (Directory.Exists(cacheFolder))
                Directory.Delete(cacheFolder, true); 
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachConsole(Int32 processId);

        /// <summary>Runs the job (.xml file) specified on the command line.</summary>
        /// <returns>True if something was run.</returns>
        private static bool RunJobFromCommandLine(string[] commandLineArguments)
        {
            if (commandLineArguments.Length > 0 && File.Exists(commandLineArguments[0]))
            {
                bool runAPSIM = !(commandLineArguments.Length == 2 &&
                                  commandLineArguments[1] == "/DontRunAPSIM");
                RunnableJobs.ProcessYPJob job = new RunnableJobs.ProcessYPJob(runAPSIM);
                job.JobFileName = commandLineArguments[0];
                if (commandLineArguments.Length > 1)
                    job.ApsimExecutable = commandLineArguments[1];
                JobManager jobManager = new JobManager();
                jobManager.AddJob(job);
                jobManager.Start(waitUntilFinished: true);
                List<Exception> errors = jobManager.Errors(job);
                if (errors != null || errors.Count > 0)
                {
                    AttachConsole(-1);
                    foreach (Exception error in errors)
                        Console.Write(error.ToString());
                }
                return true;
            }
            return false;
        }
    }
}
