using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.UI.WebControls;
using System.Xml;
using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using APSIM.Shared.Utilities;
using System.Xml.Serialization;

namespace APSIM.Cloud.Portal
{
    public partial class Upload : System.Web.UI.Page
    {
        /// <summary>
        /// The upload button was click.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void UploadButtonClick(object sender, EventArgs e)
        {
            // Get the contents of the file and write to a temp file.
            byte[] bytes = new byte[FileUpload.FileContent.Length];
            FileUpload.FileContent.Read(bytes, 0, bytes.Length);
            string tempFile = Path.GetTempFileName();
            FileStream writer = new FileStream(tempFile, FileMode.Create);
            writer.Write(bytes, 0, bytes.Length);
            writer.Close();

            if (Path.GetExtension(FileUpload.FileName) == ".zip")
            {
                File.Move(tempFile, Path.ChangeExtension(tempFile, ".zip"));
                tempFile = Path.ChangeExtension(tempFile, ".zip");
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(tempFile);
            if (doc.DocumentElement.Name == "Farm4Prophet")
            {
                UploadFarm4Prophet(tempFile);
            }
            else
            {
                UploadYieldProphet(tempFile);
            }

            File.Delete(tempFile);

            Response.Redirect("Main.aspx");
        }

        /// <summary>Uploads a job specified by the the yield prophet.</summary>
        /// <param name="fileName">The name of the file.</param>
        private void UploadYieldProphet(string fileName)
        {
            YieldProphet yieldProphet = YieldProphetUtility.YieldProphetFromFile(fileName);

            DateTime nowDate = DateTime.Now;
            if (NowEditBox.Text != "")
                nowDate = DateTime.ParseExact(NowEditBox.Text, "d/M/yyyy", CultureInfo.InvariantCulture);

            foreach (Paddock paddock in yieldProphet.Paddock)
                paddock.NowDate = nowDate;

            using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
            {
                jobsService.Add(yieldProphet);
            }
        }

        /// <summary>Uploads a job specified by the the yield prophet.</summary>
        /// <param name="fileName">The name of the file.</param>
        private void UploadFarm4Prophet(string fileName)
        {
            Farm4Prophet farm4Prophet = Farm4ProphetUtility.Farm4ProphetFromFile(fileName);

            using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
            {
                jobsService.AddFarm4Prophet(farm4Prophet);
            }
        }


    }
}