using System;
using System.Globalization;
using System.Text;
using System.Web.UI.WebControls;
using APSIM.Cloud.Services;
using APSIM.Cloud.Services.Specification;
using System.IO;
using System.Data;

namespace APSIM.Cloud.WebPortal
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

            YieldProphetSpec yieldProphet = null;
            if (Path.GetExtension(FileUpload.FileName) == ".zip")
                yieldProphet = GetYieldProphetFromZip(bytes);
            else
                yieldProphet = YieldProphetServices.Create(Encoding.ASCII.GetString(bytes));

            DateTime nowDate = DateTime.Now;
            if (NowEditBox.Text != "")
                nowDate = DateTime.ParseExact(NowEditBox.Text, "d/M/yyyy", CultureInfo.InvariantCulture);

            foreach (Paddock paddock in yieldProphet.PaddockList)
                paddock.NowDate = nowDate;
            string newJobName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;

            string xml = YieldProphetServices.ToXML(yieldProphet);

            JobsDB jobsDB = new JobsDB();
            jobsDB.Open();
            jobsDB.Add(newJobName, xml);
            jobsDB.Close();
            Response.Redirect("Main.aspx");
        }

        /// <summary>
        /// Creates a instance of a yield prophet spec from zip.
        /// </summary>
        /// <param name="bytes">The bytes of the .zip file.</param>
        /// <returns></returns>
        private static YieldProphetSpec GetYieldProphetFromZip(byte[] bytes)
        {
            YieldProphetSpec yieldProphet;

            string tempFolder = Path.GetTempFileName();
            File.Delete(tempFolder);
            Directory.CreateDirectory(tempFolder);
            MemoryStream memStream = new MemoryStream(bytes);
            string[] fileNames = Utility.Zip.UnZipFiles(memStream, tempFolder, null);
            yieldProphet = YieldProphetServices.CreateFromFile(Path.Combine(tempFolder, "YieldProphet.xml"));
            string[] rainFiles = Directory.GetFiles(tempFolder, "*.rain");
            if (rainFiles.Length >= 1)
            {
                yieldProphet.PaddockList[0].ObservedData = Utility.ApsimTextFile.ToTable(rainFiles[0]);
                foreach (DataColumn column in yieldProphet.PaddockList[0].ObservedData.Columns)
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