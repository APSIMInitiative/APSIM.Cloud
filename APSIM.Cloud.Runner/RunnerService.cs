using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using APSIM.Cloud.Shared;
using APSIM.Shared.Utilities;

namespace APSIM.Cloud.Runner
{
    partial class RunnerService : ServiceBase
    {
        /// <summary>The job manager to send our jobs to</summary>
        private JobManager jobManager;

        public RunnerService()
        {
            InitializeComponent();
            jobManager = null;
        }

        

        protected override void OnStart(string[] args)
        {
            if (jobManager == null)
            {
                jobManager = new JobManager();
                jobManager.AddJob(new RunJobsInDB());
            }
            jobManager.Start(waitUntilFinished: false);
        }

        protected override void OnStop()
        {
            jobManager.Stop();
        }
    }
}
