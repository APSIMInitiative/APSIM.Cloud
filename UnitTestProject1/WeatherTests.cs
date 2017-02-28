using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using APSIM.Cloud.Shared;
using System.IO;
using System.Data;
using APSIM.Shared.Utilities;

namespace UnitTestProject1
{
    [TestClass]
    public class WeatherTests
    {
        [TestMethod]
        public void WeatherTests_EnsureSILOFetchWorks()
        {
            DataTable observedData = new DataTable();
            observedData.Columns.Add("Date", typeof(DateTime));
            observedData.Columns.Add("Rain", typeof(double));
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 1), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 2), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 3), 20.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 4), 30.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 5), 40.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 6), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 7), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 8), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 9), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 10), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 20), 0.0 });
            observedData.Rows.Add(new object[] { new DateTime(2016, 4, 21), 0.0 });

            Weather.Data weatherFile = Weather.ExtractDataFromSILO(77008, new DateTime(2016, 4, 1), new DateTime(2016, 5, 1));

            Assert.AreEqual(weatherFile.FirstDate, new DateTime(2016, 4, 1));
            Assert.AreEqual(weatherFile.LastDate, new DateTime(2016, 5, 1));
            Assert.AreEqual(weatherFile.AMP, 2.77);
            Assert.AreEqual(weatherFile.Latitude, -35.92);
            Assert.AreEqual(weatherFile.Longitude, 142.85);
            Assert.AreEqual(weatherFile.TAV, 16.13);
            Assert.AreEqual(weatherFile.Table.Columns[0].ColumnName, "Date");
            Assert.AreEqual(weatherFile.Table.Columns[1].ColumnName, "radn");
            Assert.AreEqual(weatherFile.Table.Columns[2].ColumnName, "maxt"); ;
            Assert.AreEqual(weatherFile.Table.Columns[3].ColumnName, "mint");
            Assert.AreEqual(weatherFile.Table.Columns[4].ColumnName, "rain");
            Assert.AreEqual(weatherFile.Table.Columns[5].ColumnName, "codes");
            Assert.AreEqual(weatherFile.Table.Rows.Count, 31);
            Assert.AreEqual(weatherFile.Table.Rows[0]["codes"], "SSSS");
        }

        [TestMethod]
        public void WeatherTests_EnsureDataOverlayWorks()
        {
            DataTable observedData = new DataTable();
            observedData.Columns.Add("Date", typeof(DateTime));
            observedData.Columns.Add("Rain", typeof(double));
            observedData.Columns.Add("Codes", typeof(string));
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 3), 20.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 4), 30.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 5), 40.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 6), 0.0, "O" });

            Weather.Data weatherFile = Weather.ExtractDataFromSILO(77008, new DateTime(2016, 5, 1), new DateTime(2016, 6, 1));
            Weather.OverlayData(observedData, weatherFile.Table);

            Assert.AreEqual(weatherFile.Table.Rows[0]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[1]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[2]["codes"], "SSSO");
            Assert.AreEqual(weatherFile.Table.Rows[3]["codes"], "SSSO");
            Assert.AreEqual(weatherFile.Table.Rows[4]["codes"], "SSSO");
            Assert.AreEqual(weatherFile.Table.Rows[5]["codes"], "SSSO");

            Assert.AreEqual(weatherFile.Table.Rows[0]["rain"], 13.0f);  // original SILO rainfall
            Assert.AreEqual(weatherFile.Table.Rows[1]["rain"], 0.0f);
            Assert.AreEqual(weatherFile.Table.Rows[2]["rain"], 20.0f);
            Assert.AreEqual(weatherFile.Table.Rows[3]["rain"], 30.0f);  // original SILO rainfall was 1.3mm on this day.
            Assert.AreEqual(weatherFile.Table.Rows[4]["rain"], 40.0f);
            Assert.AreEqual(weatherFile.Table.Rows[5]["rain"], 0.0f);
        }

        [TestMethod]
        public void WeatherTests_EnsureCloneColumnWorks()
        {
            DataTable data = new DataTable();
            data.Columns.Add("Date", typeof(DateTime));
            data.Columns.Add("rain", typeof(double));
            data.Columns.Add("codes", typeof(string));
            data.Rows.Add(new object[] { new DateTime(2016, 5, 3), 20.0, "----" });
            data.Rows.Add(new object[] { new DateTime(2016, 5, 4), 30.0, "----" });
            data.Rows.Add(new object[] { new DateTime(2016, 5, 5), 40.0, "----" });
            data.Rows.Add(new object[] { new DateTime(2016, 5, 6), 0.0, "----" });

            Weather.CloneColumn(data, "rain", "newrain");

            // Make sure the new column is second from the end of the columns list.
            Assert.AreEqual(data.Columns[2].ColumnName, "newrain");
            Assert.AreEqual(data.Rows[0][2], 20.0);
            Assert.AreEqual(data.Rows[1][2], 30.0);
            Assert.AreEqual(data.Rows[2][2], 40.0);
            Assert.AreEqual(data.Rows[3][2], 0.0);

            // Make sure the codes column was moved to the end of the column list.
            Assert.AreEqual(data.Rows[0][3], "-----");
            Assert.AreEqual(data.Rows[1][3], "-----");
            Assert.AreEqual(data.Rows[2][3], "-----"); 
            Assert.AreEqual(data.Rows[3][3], "-----"); 
        }

        [TestMethod]
        public void WeatherTests_EnsureDataOverlayEveryYearWorks()
        {
            DataTable observedData = new DataTable();
            observedData.Columns.Add("Date", typeof(DateTime));
            observedData.Columns.Add("Rain", typeof(double));
            observedData.Columns.Add("Codes", typeof(string));
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 3), 20.0, "O"});
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 4), 30.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 5), 40.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 6), 0.0, "O" });

            Weather.Data weatherFile = Weather.ExtractDataFromSILO(77008, new DateTime(2014, 5, 1), new DateTime(2016, 6, 1));
            Weather.OverlayDataAllYears(observedData, weatherFile.Table);

            // Check 2014
            Assert.AreEqual(weatherFile.Table.Rows[0]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[1]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[2]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[3]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[4]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[5]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[0]["rain"], 0.4f);  // original SILO rainfall
            Assert.AreEqual(weatherFile.Table.Rows[1]["rain"], 0.0f);
            Assert.AreEqual(weatherFile.Table.Rows[2]["rain"], 20.0f);
            Assert.AreEqual(weatherFile.Table.Rows[3]["rain"], 30.0f);  
            Assert.AreEqual(weatherFile.Table.Rows[4]["rain"], 40.0f);
            Assert.AreEqual(weatherFile.Table.Rows[5]["rain"], 0.0f);

            // Check 2015
            Assert.AreEqual(weatherFile.Table.Rows[0+365]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[1+365]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[2+365]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[3+365]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[4+365]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[5+365]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[0+365]["rain"], 0.0f);   // original SILO rainfall
            Assert.AreEqual(weatherFile.Table.Rows[1+365]["rain"], 0.0f);
            Assert.AreEqual(weatherFile.Table.Rows[2+365]["rain"], 20.0f);
            Assert.AreEqual(weatherFile.Table.Rows[3+365]["rain"], 30.0f);  
            Assert.AreEqual(weatherFile.Table.Rows[4+365]["rain"], 40.0f);
            Assert.AreEqual(weatherFile.Table.Rows[5+365]["rain"], 0.0f);

            // Check 2016
            Assert.AreEqual(weatherFile.Table.Rows[0 + 365*2+1]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[1 + 365*2+1]["codes"], "SSSS");
            Assert.AreEqual(weatherFile.Table.Rows[2 + 365*2+1]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[3 + 365*2+1]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[4 + 365*2+1]["codes"], "SSSo");
            Assert.AreEqual(weatherFile.Table.Rows[5 + 365*2+1]["codes"], "SSSo");

            Assert.AreEqual(weatherFile.Table.Rows[0 + 365*2+1]["rain"], 13.0f);  // original SILO rainfall
            Assert.AreEqual(weatherFile.Table.Rows[1 + 365*2+1]["rain"], 0.0f);
            Assert.AreEqual(weatherFile.Table.Rows[2 + 365*2+1]["rain"], 20.0f);
            Assert.AreEqual(weatherFile.Table.Rows[3 + 365*2+1]["rain"], 30.0f);  // original SILO rainfall was 1.3mm on this day.
            Assert.AreEqual(weatherFile.Table.Rows[4 + 365*2+1]["rain"], 40.0f);
            Assert.AreEqual(weatherFile.Table.Rows[5 + 365*2+1]["rain"], 0.0f);
        }
        
        [TestMethod]
        public void WeatherTests_EnsureLongtermWorks()
        {
            DataTable observedData = new DataTable();
            observedData.Columns.Add("Date", typeof(DateTime));
            observedData.Columns.Add("Rain", typeof(double));
            observedData.Columns.Add("Codes", typeof(string));
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 3), 20.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 4), 30.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 5), 40.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 6), 0.0, "O" });

            string tempFolder = Path.Combine(Path.GetTempPath(), "Longterm");
            Weather.CreateLongTerm(Path.Combine(tempFolder, "longterm.met"), 77008, 
                                   new DateTime(2016, 4, 1), new DateTime(2016, 5, 10), 
                                   observedData, DateTime.MinValue, 30);

            // **** Check 2016 file.
            ApsimTextFile metFile1 = new ApsimTextFile();
            metFile1.Open(Path.Combine(tempFolder, "longterm1986.met"));
            DataTable data1 = metFile1.ToTable();

            // Make sure observed data was patched and code is correct.
            Assert.AreEqual(data1.Rows[32][0], new DateTime(2016, 5, 3));
            Assert.AreEqual(data1.Rows[32][4], 20.0f);
            Assert.AreEqual(data1.Rows[32][7], "ssso--");
            Assert.AreEqual(data1.Rows[40][0], new DateTime(2016, 5, 11));
            Assert.AreEqual(data1.Rows[40][4], 0.0f);
            Assert.AreEqual(data1.Rows[40][7], "SSSS--");
            metFile1.Close();

            // **** Check 2017 file.
            ApsimTextFile metFile2 = new ApsimTextFile();
            metFile2.Open(Path.Combine(tempFolder, "longterm1987.met"));
            DataTable data2 = metFile2.ToTable();

            // Make sure observed data was patched and code is correct.
            Assert.AreEqual(data2.Rows[32][0], new DateTime(2016, 5, 3));
            Assert.AreEqual(data2.Rows[32][4], 20.0f);
            Assert.AreEqual(data2.Rows[32][7], "ssso--");
            Assert.AreEqual(data2.Rows[40][0], new DateTime(2016, 5, 11));
            Assert.AreEqual(data2.Rows[40][4], 0.0f);
            Assert.AreEqual(data2.Rows[40][7], "SSSS--");
            metFile2.Close();

            Directory.Delete(tempFolder, true);
        }

        [TestMethod]
        public void WeatherTests_EnsureFullPOAMAWorks()
        {
            DataTable observedData = new DataTable();
            observedData.Columns.Add("Date", typeof(DateTime));
            observedData.Columns.Add("Rain", typeof(double));
            observedData.Columns.Add("Codes", typeof(string));
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 3), 20.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 4), 30.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 5), 40.0, "O" });
            observedData.Rows.Add(new object[] { new DateTime(2016, 5, 6), 0.0, "O" });

            string tempPOAMAFolder = Path.Combine(Path.GetTempPath(), "POAMA");
            Weather.CreatePOAMA(Path.Combine(tempPOAMAFolder, "poama.met"), 77008, new DateTime(2016,4,1), new DateTime(2016, 5, 10), observedData);

            // **** Check 2016 file.
            ApsimTextFile metFile1 = new ApsimTextFile();
            metFile1.Open(Path.Combine(tempPOAMAFolder, "poama2016.met"));
            DataTable data1 = metFile1.ToTable();

            // Make sure observed data was patched and code is correct.
            Assert.AreEqual(data1.Rows[32][0], new DateTime(2016, 5, 3));
            Assert.AreEqual(data1.Rows[32][4], 20.0f);
            Assert.AreEqual(data1.Rows[32][5], "ssso");

            // Make sure POAMA data was added.
            Assert.AreEqual(data1.Rows[40][0], new DateTime(2016, 5, 11));
            Assert.AreEqual(data1.Rows[40][4], 0.0f);
            Assert.AreEqual(data1.Rows[40][5], "PPPP");
            metFile1.Close();

            // **** Check 2017 file.
            ApsimTextFile metFile2 = new ApsimTextFile();
            metFile2.Open(Path.Combine(tempPOAMAFolder, "poama2017.met"));
            DataTable data2 = metFile2.ToTable();

            // Make sure observed data was patched and code is correct.
            Assert.AreEqual(data2.Rows[32][0], new DateTime(2016, 5, 3));
            Assert.AreEqual(data2.Rows[32][4], 20.0f);
            Assert.AreEqual(data2.Rows[32][5], "ssso");

            // Make sure POAMA data was added.
            Assert.AreEqual(data2.Rows[40][0], new DateTime(2016, 5, 11));
            Assert.AreEqual(data2.Rows[40][4], 0.0f);
            Assert.AreEqual(data2.Rows[40][5], "PPPP");
            metFile2.Close();

            Directory.Delete(tempPOAMAFolder, true);
        }
    }
}
