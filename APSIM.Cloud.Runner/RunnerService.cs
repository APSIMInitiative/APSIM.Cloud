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
        private IJobRunner jobRunner = null;
        private RunJobsInDB jobManager = null;

        /// <summary>The maximum number of CPU cores to use.</summary>
        private int maximumNumberOfProcessors;

        /// <summary>Constructor</summary>
        /// <param name="maximumNumberOfProcessors">The maximum number of CPU cores to use.</param>
        public RunnerService(int maximumNumberOfProcessors = -1)
        {
            InitializeComponent();
            jobManager = null;
            this.maximumNumberOfProcessors = maximumNumberOfProcessors;
        }

        

        protected override void OnStart(string[] args)
        {
            if (jobManager == null)
            {
                jobRunner = new JobRunnerAsync();
                IJobManager jobManager = new RunJobsInDB(jobRunner);
                jobRunner.Run(jobManager, wait: false, numberOfProcessors: maximumNumberOfProcessors);
            }
        }

        protected override void OnStop()
        {
            jobManager.Stop();
        }
    }
}
