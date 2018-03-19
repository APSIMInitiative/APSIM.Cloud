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
using APSIM.Cloud.Shared;

namespace APSIM.Cloud.Runner
{
    static class Program
    {
        private static List<Exception> errors = new List<Exception>();

        private static void OnJobCompleted(object sender, JobCompleteArgs e)
        {
            if (e.exceptionThrowByJob != null)
                errors.Add(e.exceptionThrowByJob);
        }

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
                if (!RunJobFromCommandLine(arguments))
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
        private static bool RunJobFromCommandLine(Dictionary<string, string> commandLineArguments)
        {
            if (commandLineArguments.ContainsKey("Filename"))
            {
                string jobFileName = commandLineArguments["Filename"];

                if (File.Exists(jobFileName))
                {
                    bool runAPSIMx = (commandLineArguments.ContainsKey("RunAPSIMX"));
                    commandLineArguments.TryGetValue("APSIMXExecutable", out string executable);

                    string jobXML = File.ReadAllText(jobFileName);
                    string jobName = Path.GetFileNameWithoutExtension(jobFileName);

                    RunYPJob job = new RunYPJob(jobXML, null)
                    {
                        ApsimXExecutable = executable
                    };

                    errors.Clear();
                    IJobRunner runner = new JobRunnerAsync();
                    runner.JobCompleted += OnJobCompleted;
                    runner.Run(job, wait: true);

                    string destZipFileName = Path.ChangeExtension(jobFileName, ".out.zip");
                    using (Stream s = File.Create(destZipFileName))
                    {
                        job.AllFilesZipped.Seek(0, SeekOrigin.Begin);
                        job.AllFilesZipped.CopyTo(s);
                    }

                    if (errors != null && errors.Count > 0)
                    {
                        AttachConsole(-1);
                        foreach (Exception error in errors)
                            Console.Write(error.ToString());
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
