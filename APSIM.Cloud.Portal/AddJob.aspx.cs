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
                YieldProphet yieldProphet = null;
                if (Path.GetExtension(FileUpload.FileName) == ".zip")
                    yieldProphet = GetYieldProphetFromZip(bytes);
                else
                    yieldProphet = YieldProphetUtility.YieldProphetFromXML(Encoding.ASCII.GetString(bytes));

                DateTime nowDate = DateTime.Now;
                if (NowEditBox.Text != "")
                    nowDate = DateTime.ParseExact(NowEditBox.Text, "d/M/yyyy", CultureInfo.InvariantCulture);

                foreach (Paddock paddock in yieldProphet.Paddock)
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
        private static YieldProphet GetYieldProphetFromZip(byte[] bytes)
        {
            YieldProphet yieldProphet;

            string tempFolder = Path.GetTempFileName();
            File.Delete(tempFolder);
            Directory.CreateDirectory(tempFolder);
            MemoryStream memStream = new MemoryStream(bytes);
            string[] fileNames = APSIM.Shared.Utilities.ZipUtilities.UnZipFiles(memStream, tempFolder, null);

            string fileName = Path.Combine(tempFolder, "YieldProphet.xml");
            if (!File.Exists(fileName))
            {
                // Look for first XML file.
                foreach (string file in fileNames)
                {
                    if (file.Contains(".xml"))
                    {
                        fileName = file;
                        break;
                    }
                }
            }

            yieldProphet = YieldProphetUtility.YieldProphetFromFile(fileName);
            yieldProphet.ReportName = Path.GetFileNameWithoutExtension(fileName);
            Directory.Delete(tempFolder, true);
            return yieldProphet;
        }

 
    }
}