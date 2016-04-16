using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.ServiceProcess;
using System.IO;
using APSIM.Shared.Utilities;
using System.Runtime.InteropServices;

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
            else if (args.Length > 0)
                RunJobFromCommandLine(args);
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args));
            }
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachConsole(Int32 processId);

        /// <summary>Runs the job (.xml file) specified on the command line.</summary>
        private static void RunJobFromCommandLine(string[] commandLineArguments)
        {
            if (commandLineArguments.Length > 0 && File.Exists(commandLineArguments[0]))
            {
                bool runAPSIM = commandLineArguments.Length == 2 &&
                                commandLineArguments[1] == "/DontRunAPSIM";
                RunnableJobs.ProcessYPJob job = new RunnableJobs.ProcessYPJob(runAPSIM);
                job.JobFileName = commandLineArguments[0];
                if (commandLineArguments.Length > 1)
                    job.ApsimExecutable = commandLineArguments[1];
                JobManager jobManager = new JobManager();
                jobManager.AddJob(job);
                jobManager.Start(waitUntilFinished: true);
                if (job.ErrorMessage != null)
                {
                    AttachConsole(-1);
                    Console.Write(job.ErrorMessage);
                }
            }
        }
    }
}
