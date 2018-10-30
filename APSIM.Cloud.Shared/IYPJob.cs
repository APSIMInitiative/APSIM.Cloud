// -----------------------------------------------------------------------
// <copyright file="IYPJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// A job manager for a running a specific YieldProphet job.
    /// </summary>
    public interface IYPJob
    {
        /// <summary>Get all errors encountered</summary>
        List<string> Errors { get; }

        /// <summary>Output zip file.</summary>
        Stream AllFilesZipped { get; }

    }
}
