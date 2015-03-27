using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Data;

namespace CallReportingWebService
{
    class Program
    {
        static void Main(string[] args)
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            DataSet dataSet = new DataSet("ReportData");

            // Read in all .out files, adding them to the dataset.
            foreach (string outFileName in Directory.GetFiles(folder, "*.out"))
                dataSet.Tables.Add(Utility.ApsimTextFile.ToTable(outFileName));

            // Dummy report name
            string reportName = "2015-03-16 (12-35-18 PM) FLOHR392 Crop Report (Complete)";

            // Call StoreReport
            YP.com.au.Reporting reportingService = new YP.com.au.Reporting();
            //reportingService.StoreReport(reportName, dataSet);
        }
    }
}
