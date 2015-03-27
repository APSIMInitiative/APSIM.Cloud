using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.UI.WebControls;
using System.Xml;

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
            // Get the contents of the XML file.
            byte[] bytes = new byte[FileUpload.FileContent.Length];
            FileUpload.FileContent.Read(bytes, 0, bytes.Length);

            using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
            {
                JobsService.YieldProphet yieldProphet = null;
                if (Path.GetExtension(FileUpload.FileName) == ".zip")
                    yieldProphet = GetYieldProphetFromZip(bytes, jobsService);
                else
                    yieldProphet = jobsService.YieldProphetFromXML(Encoding.ASCII.GetString(bytes));

                DateTime nowDate = DateTime.Now;
                if (NowEditBox.Text != "")
                    nowDate = DateTime.ParseExact(NowEditBox.Text, "d/M/yyyy", CultureInfo.InvariantCulture);

                foreach (JobsService.Paddock paddock in yieldProphet.Paddock)
                    paddock.NowDate = nowDate;

                jobsService.Add(yieldProphet);
            }
            Response.Redirect("Main.aspx");
        }

        /// <summary>
        /// Creates a instance of a yield prophet spec from zip.
        /// </summary>
        /// <param name="bytes">The bytes of the .zip file.</param>
        /// <returns></returns>
        private static JobsService.YieldProphet GetYieldProphetFromZip(byte[] bytes, JobsService.JobsClient jobsClient)
        {
            JobsService.YieldProphet yieldProphet;

            string tempFolder = Path.GetTempFileName();
            File.Delete(tempFolder);
            Directory.CreateDirectory(tempFolder);
            MemoryStream memStream = new MemoryStream(bytes);
            string[] fileNames = Utility.Zip.UnZipFiles(memStream, tempFolder, null);

            XmlDocument doc = new XmlDocument();
            doc.Load(Path.Combine(tempFolder, "YieldProphet.xml"));

            yieldProphet = jobsClient.YieldProphetFromXML(doc.OuterXml);
            string[] rainFiles = Directory.GetFiles(tempFolder, "*.rain");
            if (rainFiles.Length >= 1)
            {
                yieldProphet.Paddock[0].ObservedData = Utility.ApsimTextFile.ToTable(rainFiles[0]);
                foreach (DataColumn column in yieldProphet.Paddock[0].ObservedData.Columns)
                {
                    if (column.ColumnName.Contains("patch_"))
                        column.ColumnName = column.ColumnName.Replace("patch_", "");
                }
            }
            Directory.Delete(tempFolder, true);
            return yieldProphet;
        }

 
    }
}