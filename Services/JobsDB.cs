// -----------------------------------------------------------------------
// <copyright file="JobsDB.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Data.SqlClient;

    /// <summary>This class is responsible for all access to the JOBS database.</summary>
    public class JobsDB
    {
        /// <summary>The connection to the jobs database</summary>
        private SqlConnection Connection;


        /// <summary>
        /// An enumeration containing the valid values for the status field
        /// in the DB
        /// </summary>
        public enum StatusEnum { Added, Running, Completed, Error, Deleting };

        /// <summary>Open the DB ready for use.</summary>
        public void Open()
        {
            string ConnectionString = "Data Source=www.apsim.info;Database=\"Jobs\";Trusted_Connection=False;User ID=sv-login-external;password=P@ssword123";
            Connection = new SqlConnection(ConnectionString);
            Connection.Open();
        }

        /// <summary>Close the SoilsDB connection</summary>
        public void Close()
        {
            if (Connection != null)
            {
                Connection.Close();
                Connection = null;
            }
        }

        /// <summary>Gets a value indicating whether the connection is open.</summary>
        /// <value><c>true</c> if this connection is open; otherwise, <c>false</c>.</value>
        public bool IsOpen
        {
            get
            {
                return Connection != null && Connection.State == System.Data.ConnectionState.Open;
            }
        }

        /// <summary>Add a new entry to the database.</summary>
        /// <param name="Name">The name of the job.</param>
        /// <param name="jobXML">The job XML.</param>
        /// <param name="zipFileContents">The zip file contents.</param>
        public void Add(string Name, string jobXML)
        {
            string SQL = "INSERT INTO Jobs (Name, XML, Status) " +
                         "VALUES (@Name, @XML, @Status)";

            string nowString = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt");

            SqlCommand Cmd = new SqlCommand(SQL, Connection);

            Cmd.Parameters.Add(new SqlParameter("@Name", Name));
            Cmd.Parameters.Add(new SqlParameter("@XML", jobXML));
            Cmd.Parameters.Add(new SqlParameter("@Status", StatusEnum.Added));
            Cmd.ExecuteNonQuery();
        }

        /// <summary>Delete the specified job from the database.</summary>
        /// <param name="Name">The name of the job.</param>
        public void Delete(string name)
        {
            string SQL = "DELETE FROM Jobs WHERE Name = '" + name + "'";
            SqlCommand Command = new SqlCommand(SQL, Connection);
            Command.ExecuteNonQuery();
        }

        /// <summary>Gets all jobs with the specified status.</summary>
        /// <param name="status">The status to match</param>
        /// <returns>The array of matching jobs</returns>
        public JobDB GetJob(string name)
        {
            string SQL = "SELECT * FROM Jobs WHERE name = '" + name + "' ORDER BY name DESC";

            SqlCommand Command = new SqlCommand(SQL, Connection);
            SqlDataReader Reader = Command.ExecuteReader();
            try
            {
                if (Reader.Read())
                    return new JobDB(Reader["Name"].ToString(),
                                     Reader["XML"].ToString(),
                                     (StatusEnum)Reader["Status"],
                                     Reader["URL"].ToString(),
                                     Reader["ErrorText"].ToString());
            }
            finally
            {
                Reader.Close();
            }

            return null;
        }

        /// <summary>Gets all jobs with the specified status.</summary>
        /// <param name="status">The status to match</param>
        /// <param name="maxNum">The maximum number of jobs to return.</param>
        /// <returns>The array of matching jobs</returns>
        public JobDB[] Get(StatusEnum status)
        {
            List<JobDB> jobs = new List<JobDB>();

            string SQL = "SELECT * FROM Jobs WHERE Status = " + ((int)status).ToString() + " ORDER BY name DESC";

            SqlCommand Command = new SqlCommand(SQL, Connection);
            SqlDataReader Reader = Command.ExecuteReader();
            try
            {
                while (Reader.Read())
                    jobs.Add(new JobDB(Reader["Name"].ToString(), 
                                       Reader["XML"].ToString(),
                                       (StatusEnum) Reader["Status"],
                                       Reader["URL"].ToString(),
                                       Reader["ErrorText"].ToString()));
            }
            finally
            {
                Reader.Close();
            }

            return jobs.ToArray();
        }

        /// <summary>Gets all jobs</summary>
        /// <param name="maxNum">The maximum number of jobs to return.</param>
        /// <returns>The array of matching jobs</returns>
        public JobDB[] Get(int maxNum)
        {
            List<JobDB> jobs = new List<JobDB>();

            string SQL = "SELECT TOP(" + maxNum.ToString() + ") * FROM Jobs ORDER BY name DESC";

            SqlCommand Command = new SqlCommand(SQL, Connection);
            SqlDataReader Reader = Command.ExecuteReader();
            try
            {
                while (Reader.Read())
                    jobs.Add(new JobDB(Reader["Name"].ToString(), 
                                       Reader["XML"].ToString(),
                                       (StatusEnum)Reader["Status"],
                                       Reader["URL"].ToString(),
                                       Reader["ErrorText"].ToString()));
            }
            finally
            {
                Reader.Close();
            }

            return jobs.ToArray();
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

        /// <summary>Sets the job status for the specified job</summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="newStatus">The new status.</param>
        public void SetJobStatus(string jobName, StatusEnum newStatus)
        {
            string SQL = "UPDATE Jobs SET Status = " + ((int)newStatus).ToString() +
                         " WHERE Name = '" + jobName + "'";

            SqlCommand Cmd = new SqlCommand(SQL, Connection);
            Cmd.ExecuteNonQuery();
        }

        /// <summary>Sets the job URL for the specified job</summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="newStatus">The URL</param>
        public void SetJobURL(string jobName, string url)
        {
            string SQL = "UPDATE Jobs SET URL = '" + url + "'" +
                         " WHERE Name = '" + jobName + "'";

            SqlCommand Cmd = new SqlCommand(SQL, Connection);
            Cmd.ExecuteNonQuery();
        }

        /// <summary>Sets the job error message for the specified job</summary>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="newStatus">The URL</param>
        public void SetJobErrorText(string jobName, string errorMessage)
        {
            string SQL = "UPDATE Jobs SET ErrorText = '" + errorMessage + "'" +
                         " WHERE Name = '" + jobName + "'";

            SqlCommand Cmd = new SqlCommand(SQL, Connection);
            Cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// A class for holding details about a job.
        /// </summary>
        public class JobDB
        {
            public JobDB(string name, string xml, StatusEnum status, string url, string errorText)
            {
                this.Name = name;
                this.XML = xml;
                this.Status = status;
                this.URL = url;
                this.ErrorText = errorText;
            }

            public string Name { get; private set; }
            public string XML { get; private set; }
            public StatusEnum Status { get; private set; }
            public string URL { get; private set; }
            public string ErrorText { get; private set; }
        }

    }
}
