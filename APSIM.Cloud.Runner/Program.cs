namespace APSIM.Cloud.Runner
{
    using APSIM.Cloud.Shared;
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.ServiceProcess;
    using System.Windows.Forms;
    using System.Xml;

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                EnsureBuildNumberIsPutIntoSummaryFile();

                Dictionary<string, string> appSettings = StringUtilities.ParseCommandLine(args);

                // Add app settings into command line arguments.
                foreach (string setting in ConfigurationManager.AppSettings)
                {
                    if (!appSettings.ContainsKey(setting))
                        appSettings.Add(setting, ConfigurationManager.AppSettings[setting]);
                }

                // If there is a -Service switch then start as a service otherwise start as a regular application
                if (appSettings["RunAsService"] == "true")
                {
                    ServiceBase[] ServicesToRun;
                    ServicesToRun = new ServiceBase[]
                    {
                        new RunnerService(appSettings)
                    };
                    ServiceBase.Run(ServicesToRun);
                }
                else
                {
                    if (!RunJobFromCommandLine(appSettings))
                    {
                        Application.EnableVisualStyles();
                        Application.SetCompatibleTextRenderingDefault(false);
                        Application.Run(new MainForm(appSettings));
                    }
                }
            }
            catch (Exception err)
            {
                AttachConsole(-1);
                Console.WriteLine(err.ToString());
                return 1;
            }
            return 0;
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
        private static bool RunJobFromCommandLine(Dictionary<string, string> appSettings)
        {
            if (appSettings.ContainsKey("FileName"))
            {
                string jobFileName = appSettings["FileName"];

                if (File.Exists(jobFileName))
                {
                    appSettings.TryGetValue("APSIMXExecutable", out string executable);

                    string jobXML = File.ReadAllText(jobFileName);
                    string jobName = Path.GetFileNameWithoutExtension(jobFileName);

                    var environment = new APSIM.Cloud.Shared.RuntimeEnvironment
                    {
                        APSIMRevision = appSettings["APSIMRevision"],
                        RuntimePackage = appSettings["RuntimePackage"],
                    };

                    RunYPJob job = new RunYPJob(jobXML, environment)
                    {
                        ApsimXExecutable = executable
                    };
                    if (job.Errors.Count == 0)
                    {
                        IJobRunner runner = new JobRunnerAsync();
                        runner.Run(job, wait: true);
                    }

                    if (job.AllFilesZipped != null)
                    {
                        string destZipFileName = Path.ChangeExtension(jobFileName, ".out.zip");
                        using (Stream s = File.Create(destZipFileName))
                        {
                            job.AllFilesZipped.Seek(0, SeekOrigin.Begin);
                            job.AllFilesZipped.CopyTo(s);
                        }
                    }

                    if (job.Errors.Count > 0)
                    {
                        string msg = string.Empty;
                        foreach (string error in job.Errors)
                            msg += error + Environment.NewLine;
                        throw new Exception(msg);
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
