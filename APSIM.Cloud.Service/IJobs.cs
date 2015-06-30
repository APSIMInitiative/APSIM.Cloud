using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using APSIM.Cloud.Shared;
using APSIM.Cloud.Service;
using System.Data;

namespace APSIM.Cloud.Service
{
    /// <summary>
    /// An enumeration containing the valid values for the status field
    /// in the DB
    /// </summary>
    public enum StatusEnum { Queued, Running, Completed, Error };

    [ServiceContract]
    public interface IJobs
    {
        [OperationContract]
        string GetData(int value);

        /// <summary>
        /// Adds the job to the APSIM cloud.
        /// </summary>
        /// <param name="yieldProphet">The job specification.</param>
        /// <returns>The unique job name.</returns>
        [OperationContract]
        string Add(YieldProphet yieldProphet);

        [OperationContract]
        string AddFarm4Prophet(Farm4Prophet f4p);

        /// <summary>Add a new entry to the database.</summary>
        /// <param name="name">The name of the job.</param>
        /// <param name="jobXML">The job XML.</param>
        /// <param name="zipFileContents">The zip file contents.</param>
        [OperationContract]
        void AddAsXML(string name, string jobXML);

        /// <summary>Delete the specified job from the database.</summary>
        /// <param name="Name">The name of the job.</param>
        [OperationContract]
        void Delete(string name);

        /// <summary>Gets all jobs with the specified status.</summary>
        /// <param name="status">The status to match</param>
        /// <returns>The array of matching jobs</returns>
        [OperationContract]
        Job Get(string name);

        /// <summary>
        /// Gets the next job that needs to run.
        /// </summary>
        /// <returns>The job needing to be run or null if none.</returns>
        [OperationContract]
        Job GetNextToRun();

        /// <summary>Gets all jobs</summary>
        /// <param name="maxNum">The maximum number of jobs to return.</param>
        /// <returns>The array of matching jobs</returns>
        [OperationContract]
        Job[] GetMany(int maxNum);

        /// <summary>Gets a jobs XML.</summary>
        /// <param name="name">The name of the job.</param>
        /// <returns>The XML of the job or null if not found.</returns>
        [OperationContract]
        string GetJobXML(string name);

        /// <summary>Adds a log message.</summary>
        /// <param name="message">The message to add.</param>
        /// <param name="isError">Indicates if message is an error message</param>
        [OperationContract]
        void AddLogMessage(string message, bool isError);

        /// <summary>
        /// Gets the log messages.
        /// </summary>
        /// <returns>The log message: Date, Status, Message</returns>
        [OperationContract]
        DataSet GetLogMessages();

        /// <summary>Specifies that the job is completed./summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="errorMessage">Any error message. Can be null.</param>
        [OperationContract]
        void SetCompleted(string jobName, string errorMessage);

        /// <summary>Rerun the specified job/summary>
        /// <param name="jobName">Name of the job.</param>
        [OperationContract]
        void ReRun(string jobName);
    }
}

