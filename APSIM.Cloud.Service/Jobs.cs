// -----------------------------------------------------------------------
// <copyright file="Jobs.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.Service
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Xml;
    using System.Xml.Serialization;
    using System.Data;
    using APSIM.Cloud.Shared;

    /// <summary>
    /// A class encapsulating a database of jobs that need running.
    /// </summary>
    public class Jobs : IJobs, IDisposable
    {
        /// <summary>The connection to the jobs database</summary>
        private SqlConnection Connection = null;

        /// <summary>
        /// A lock object to control serial access when getting jobs to run.
        /// </summary>
        private static object lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="Jobs"/> class.
        /// </summary>
        public Jobs()
        {
            Open();
        }

        /// <summary>
        /// Releases all resources used by the <see cref="T:System.ComponentModel.MarshalByValueComponent" />.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Adds the job to the APSIM cloud.
        /// </summary>
        /// <param name="yieldProphet">The job specification.</param>
        /// <returns>The unique job name.</returns>
        public string Add(YieldProphet yieldProphet)
        {
            DateTime nowDate = DateTime.Now;
            if (yieldProphet.Paddock.Count > 0 && yieldProphet.Paddock[0].NowDate != DateTime.MinValue)
                nowDate = yieldProphet.Paddock[0].NowDate;

            foreach (Paddock paddock in yieldProphet.Paddock)
                paddock.NowDate = nowDate;
            string newJobName = DateTime.Now.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;

            string xml = YieldProphetUtility.YieldProphetToXML(yieldProphet);

            AddAsXML(newJobName, xml);
            return newJobName;
        }

        /// <summary>
        /// Adds a Farm4Prophet job to the APSIM cloud.
        /// </summary>
        /// <param name="f4p">The job specification.</param>
        /// <returns>The unique job name.</returns>
        public string AddFarm4Prophet(Farm4Prophet f4p)
        {
            string newJobName = DateTime.Now.ToString("yyyy-MM-dd (h-mm-ss tt) ") + f4p.TaskName + "_F4P";

            string xml = Farm4ProphetUtility.Farm4ProphetToXML(f4p);

            AddAsXML(newJobName, xml);
            return newJobName;
        }

        /// <summary>Add a new entry to the database.</summary>
        /// <param name="name">The name of the job.</param>
        /// <param name="jobXML">The job XML.</param>
        /// <param name="zipFileContents">The zip file contents.</param>
        public void AddAsXML(string name, string jobXML)
        {
            string SQL = "INSERT INTO Jobs (Name, XML, Status) " +
                         "VALUES (@Name, @XML, @Status)";

            string nowString = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt");

            SqlCommand Cmd = new SqlCommand(SQL, Connection);

            Cmd.Parameters.Add(new SqlParameter("@Name", name));
            Cmd.Parameters.Add(new SqlParameter("@XML", jobXML));
            Cmd.Parameters.Add(new SqlParameter("@Status", StatusEnum.Queued));
            Cmd.ExecuteNonQuery();
        }

        /// <summary>Delete the specified job from the database.</summary>
        /// <param name="name">The name of the job.</param>
        public void Delete(string name)
        {
            string SQL = "DELETE FROM Jobs WHERE Name = '" + name + "'";
            SqlCommand Command = new SqlCommand(SQL, Connection);
            Command.ExecuteNonQuery();
        }

        /// <summary>Gets a specific job.</summary>
        /// <param name="name">The name of the job.</param>
        /// <returns>The job or null if not found</returns>
        public Job Get(string name)
        {
            string SQL = "SELECT * FROM Jobs WHERE name = '" + name + "' ORDER BY name DESC";

            SqlCommand Command = new SqlCommand(SQL, Connection);
            SqlDataReader Reader = Command.ExecuteReader();
            try
            {
                if (Reader.Read())
                    return CreateJobFromReader(Reader);
                else
                    return null;
            }
            finally
            {
                Reader.Close();
            }
        }

        /// <summary>
        /// Gets the next job that needs to run.
        /// </summary>
        /// <returns>The job needing to be run or null if none.</returns>
        public Job GetNextToRun()
        {
            lock (lockObject)
            {
                string SQL = "SELECT TOP(1) * FROM Jobs WHERE Status = " + ((int)StatusEnum.Queued).ToString();

                SqlCommand Command = new SqlCommand(SQL, Connection);
                SqlDataReader Reader = Command.ExecuteReader();
                try
                {
                    if (Reader.Read())
                    {
                        Job newJob = CreateJobFromReader(Reader);
                        Reader.Close();
                        SetStatus(newJob.Name, StatusEnum.Running);
                        return newJob;
                    }
                    else
                        return null;
                }
                finally
                {
                    Reader.Close();
                }
            }
        }

        /// <summary>Gets all jobs</summary>
        /// <param name="maxNum">The maximum number of jobs to return.</param>
        /// <returns>The array of matching jobs</returns>
        public Job[] GetMany(int maxNum)
        {
            List<Job> jobs = new List<Job>();

            string SQL = "SELECT TOP(" + maxNum.ToString() + ") * FROM Jobs ORDER BY name DESC";

            SqlCommand Command = new SqlCommand(SQL, Connection);
            SqlDataReader Reader = Command.ExecuteReader();
            try
            {
                while (Reader.Read())
                    jobs.Add(CreateJobFromReader(Reader));
            }
            finally
            {
                Reader.Close();
            }

            return jobs.ToArray();
        }

        /// <summary>Gets a jobs XML.</summary>
        /// <param name="name">The name of the job.</param>
        /// <returns>The XML of the job or null if not found.</returns>
        public string GetJobXML(string name)
        {
            string SQL = "SELECT * FROM Jobs WHERE name = '" + name + "' ORDER BY name DESC";

            SqlCommand Command = new SqlCommand(SQL, Connection);
            SqlDataReader Reader = Command.ExecuteReader();
            try
            {
                if (Reader.Read())
                    return Reader["XML"].ToString();
                else
                    return null;
            }
            finally
            {
                Reader.Close();
            }
        }

        /// <summary>Adds a log message.</summary>
        /// <param name="message">The message to add.</param>
        /// <param name="isError">Indicates if message is an error message</param>
        public void AddLogMessage(string message, bool isError)
        {
            string SQL = "INSERT INTO Log (Date, Status, Message) " +
                    "VALUES (@Date, @Status, @Message)";

            SqlCommand Cmd = new SqlCommand(SQL, Connection);

            Cmd.Parameters.Add(new SqlParameter("@Date", DateTime.Now));
            Cmd.Parameters.Add(new SqlParameter("@Status", isError));
            Cmd.Parameters.Add(new SqlParameter("@Message", message));
            Cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Gets the log messages.
        /// </summary>
        /// <returns>The log message: Date, Status, Message</returns>
        public DataSet GetLogMessages()
        {
            string SQL = "SELECT TOP(100) * FROM LOG ORDER BY DATE DESC";

            SqlCommand command = new SqlCommand(SQL, Connection);
            SqlDataReader reader = command.ExecuteReader();
            DataSet dataset = new DataSet();
            try
            {
                DataTable table = new DataTable();
                table.Load(reader);
                dataset.Tables.Add(table);
                return dataset;
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>Specifies that the job is completed./summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="errorMessage">Any error message. Can be null.</param>
        public void SetCompleted(string jobName, string errorMessage)
        {
            string sql;
            if (errorMessage == null)
                sql = "UPDATE Jobs SET Status = " + ((int)StatusEnum.Completed).ToString() +
                      " WHERE Name = '" + jobName + "'";
            else
                sql = "UPDATE Jobs SET Status = " + ((int)StatusEnum.Error).ToString() +
                         ", ErrorText = '" + errorMessage + "'" +
                         " WHERE Name = '" + jobName + "'";

            SqlCommand Cmd = new SqlCommand(sql, Connection);
            Cmd.ExecuteNonQuery();
        }

        /// <summary>Rerun the specified job/summary>
        /// <param name="jobName">Name of the job.</param>
        public void ReRun(string jobName)
        {
            string sql = "UPDATE Jobs SET Status = " + ((int)StatusEnum.Queued).ToString() +
                         " WHERE Name = '" + jobName + "'";

            SqlCommand Cmd = new SqlCommand(sql, Connection);
            Cmd.ExecuteNonQuery();
        }

        /// <summary>Open the DB ready for use.</summary>
        private void Open()
        {
            string ConnectionString = "Data Source=www.apsim.info;Database=\"Jobs\";Trusted_Connection=False;User ID=sv-login-external;password=P@ssword123";
            Connection = new SqlConnection(ConnectionString);
            Connection.Open();
        }

        /// <summary>Close the SoilsDB connection</summary>
        private void Close()
        {
            if (Connection != null)
            {
                Connection.Close();
                Connection = null;
            }
        }

        /// <summary>
        /// Create a job from the specified reader.
        /// </summary>
        /// <param name="Reader"></param>
        /// <returns>The newly created job.</returns>
        private static Job CreateJobFromReader(SqlDataReader Reader)
        {
            string url = null;
            if ((StatusEnum)Reader["Status"] == StatusEnum.Completed ||
                 (StatusEnum)Reader["Status"] == StatusEnum.Error)
                url = "http://www.apsim.info/YP/Archive/" + Reader["Name"] + ".zip";

            return new Job(Reader["Name"].ToString(),
                            (StatusEnum)Reader["Status"],
                            url,
                            Reader["ErrorText"].ToString());
        }

        /// <summary>Sets the job status for the specified job</summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="newStatus">The new status.</param>
        private void SetStatus(string jobName, StatusEnum newStatus)
        {
            string SQL = "UPDATE Jobs SET Status = " + ((int)newStatus).ToString() +
                         " WHERE Name = '" + jobName + "'";

            SqlCommand Cmd = new SqlCommand(SQL, Connection);
            Cmd.ExecuteNonQuery();
        }


    }
}
