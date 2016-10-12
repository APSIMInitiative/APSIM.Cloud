using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using APSIM.Cloud.Service;
using System.Data.SqlClient;
using System.IO;
using System.Data;
using APSIM.Cloud.Shared;
using System.Reflection;
using System.Xml;
using APSIM.Shared.Utilities;
using System.Net.Mail;

namespace APSIM.Cloud.Service
{
    public class Jobs : IJobs
    {
        /// <summary>
        /// A lock object to control serial access when getting jobs to run.
        /// </summary>
        private static object lockObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="Jobs"/> class.
        /// </summary>
        public Jobs()
        {
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
            string newJobName = DateTime.Now.ToString("yyyy-MM-dd (HH-mm-ss) ") + yieldProphet.ReportName;

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
            string newJobName = DateTime.Now.ToString("yyyy-MM-dd (HH-mm-ss) ") + f4p.TaskName + "_F4P";

            string xml = Farm4ProphetUtility.Farm4ProphetToXML(f4p);

            AddAsXML(newJobName, xml);
            return newJobName;
        }

        /// <summary>
        /// Adds an older yield prophet job to the APSIM cloud.
        /// </summary>
        /// <param name="yieldProphet">The job specification.</param>
        public void AddYP(string yieldProphetXML, DataTable weatherData, DataTable soilProbeData)
        {
            // Parse the PaddockXML.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(yieldProphetXML);

            // Set the report type to indicate to the run machine to do a current growth stage.
            //XmlHelper.SetValue(Doc.DocumentElement, "ReportType", "Current growth stage");

            // Get the paddock node.
            XmlNode PaddockNode = XmlUtilities.FindByType(doc.DocumentElement, "Paddock");
            if (PaddockNode == null)
                throw new Exception("Cannot find a <paddock> node in the PaddockXML");

            // Get a temporary working folder.
            string workingDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(workingDirectory);

            // Save the xml to file so that we can attach it to the email.
            string ypFileName = Path.Combine(workingDirectory, "YieldProphet.xml");
            doc.Save(ypFileName);

            // Write the weather file.
            string weatherFileName = Path.Combine(workingDirectory, "observed.csv");
            StreamWriter writer = new StreamWriter(weatherFileName);
            DataTableUtilities.DataTableToText(weatherData, 0, ",", true, writer);
            writer.Close();

            // Write the soil probe file.
            string soilProbeFileName = Path.Combine(workingDirectory, "soilprobe.csv");
            writer = new StreamWriter(soilProbeFileName);
            DataTableUtilities.DataTableToText(soilProbeData, 0, ",", true, writer);
            writer.Close();

            // Send email to run machine.
            MailMessage mail = new MailMessage();
            mail.Subject = "YieldProphet run";
            mail.To.Add(new MailAddress("apsimmailer@gmail.com"));
            mail.From = new System.Net.Mail.MailAddress("apsimmailer@gmail.com");
            mail.IsBodyHtml = false;
            mail.Attachments.Add(new Attachment(ypFileName));
            mail.Attachments.Add(new Attachment(weatherFileName));
            mail.Attachments.Add(new Attachment(soilProbeFileName));
            SmtpClient smtp = new SmtpClient("smtp-relay.csiro.au");
            smtp.Send(mail);
            mail.Dispose();

            // Clean up our directory.
            Directory.Delete(workingDirectory, true);

        }

        /// <summary>Add a new entry to the database.</summary>
        /// <param name="name">The name of the job.</param>
        /// <param name="jobXML">The job XML.</param>
        /// <param name="zipFileContents">The zip file contents.</param>
        public void AddAsXML(string name, string jobXML)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                string SQL = "INSERT INTO Jobs (Name, XML, Status) " +
                                "VALUES (@Name, @XML, @Status)";

                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    command.Parameters.Add(new SqlParameter("@Name", name));
                    command.Parameters.Add(new SqlParameter("@XML", jobXML));
                    command.Parameters.Add(new SqlParameter("@Status", StatusEnum.Queued));
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Adds a YieldProphet job to the APSIM cloud. Job is zipped.
        /// </summary>
        /// <param name="job">The job bytes.</param>
        /// <returns>The unique job name.</returns>
        public string AddAsZIP(byte[] job)
        {
            string newJobName = DateTime.Now.ToString("yyyy-MM-dd (HH-mm-ss)");

            string tempZipFile = Path.GetTempFileName() + ".zip";
            File.WriteAllBytes(tempZipFile, job);
            YieldProphet yieldProphet = YieldProphetUtility.YieldProphetFromFile(tempZipFile);
            string xml = YieldProphetUtility.YieldProphetToXML(yieldProphet);

            AddAsXML(newJobName, xml);
            return newJobName;
        }

        /// <summary>Delete the specified job from the database.</summary>
        /// <param name="name">The name of the job.</param>
        public void Delete(string name)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                string SQL = "DELETE FROM Jobs WHERE Name = '" + name + "'";
                using (SqlCommand command = new SqlCommand(SQL, connection))
                    command.ExecuteNonQuery();
            }
        }

        /// <summary>Gets a specific job.</summary>
        /// <param name="name">The name of the job.</param>
        /// <returns>The job or null if not found</returns>
        public Job Get(string name)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                string SQL = "SELECT * FROM Jobs WHERE name = '" + name + "' ORDER BY name DESC";

                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                            return CreateJobFromReader(reader);
                        else
                            return null;
                    }
                }
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

                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(SQL, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Job newJob = CreateJobFromReader(reader);
                                reader.Close();
                                SetStatus(newJob.Name, StatusEnum.Running);
                                return newJob;
                            }
                            else
                                return null;
                        }
                    }
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

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            jobs.Add(CreateJobFromReader(reader));
                    }
                }
            }

            return jobs.ToArray();
        }

        /// <summary>Gets a jobs XML.</summary>
        /// <param name="name">The name of the job.</param>
        /// <returns>The XML of the job or null if not found.</returns>
        public string GetJobXML(string name)
        {
            string SQL = "SELECT * FROM Jobs WHERE name = '" + name + "' ORDER BY name DESC";

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                            return reader["XML"].ToString();
                        else
                            return null;
                    }
                }
            }
        }

        /// <summary>Adds a log message.</summary>
        /// <param name="message">The message to add.</param>
        /// <param name="isError">Indicates if message is an error message</param>
        public void AddLogMessage(string message, bool isError)
        {
            string SQL = "INSERT INTO Log (Date, Status, Message) " +
                    "VALUES (@Date, @Status, @Message)";

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    command.Parameters.Add(new SqlParameter("@Date", DateTime.Now));
                    command.Parameters.Add(new SqlParameter("@Status", isError));
                    command.Parameters.Add(new SqlParameter("@Message", message));
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Gets the log messages.
        /// </summary>
        /// <returns>The log message: Date, Status, Message</returns>
        public DataSet GetLogMessages()
        {
            string SQL = "SELECT TOP(100) * FROM LOG ORDER BY DATE DESC";

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(SQL, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
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
                }
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

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(sql, connection))
                    command.ExecuteNonQuery();
            }
        }

        /// <summary>Rerun the specified job/summary>
        /// <param name="jobName">Name of the job.</param>
        public void ReRun(string jobName)
        {
            string sql = "UPDATE Jobs SET Status = " + ((int)StatusEnum.Queued).ToString() +
                         " WHERE Name = '" + jobName + "'";

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(sql, connection))
                    command.ExecuteNonQuery();
            }
        }

        /// <summary>Open the DB ready for use.</summary>
        private string ConnectionString
        {
            get
            {
                return File.ReadAllText(@"D:\Websites\dbConnect.txt") + ";Database=\"APSIM.Cloud\"";
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
                url = "http://bob.apsim.info/APSIM.Cloud.Archive/" + Reader["Name"] + ".zip";

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

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(SQL, connection))
                    command.ExecuteNonQuery();
            }
        }


    }
}
