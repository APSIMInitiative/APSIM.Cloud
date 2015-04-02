// -----------------------------------------------------------------------
// <copyright file="Job.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.Service
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// A class for holding details about a job.
    /// </summary>
    public class Job
    {
        /// <summary>
        /// Default constructor so can be serialised.
        /// </summary>
        public Job() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Name of job.</param>
        /// <param name="xml">XML of job.</param>
        /// <param name="status">Status of job.</param>
        /// <param name="url">URL of archive.</param>
        /// <param name="errorText">Error text or null if no error.</param>
        public Job(string name, StatusEnum status, string url, string errorText)
        {
            this.Name = name;
            this.Status = status;
            this.URL = url;
            this.ErrorText = errorText;
        }

        public string Name { get; set; }
        public StatusEnum Status { get; set; }
        public string URL { get; set; }
        public string ErrorText { get; set; }
    }
}
