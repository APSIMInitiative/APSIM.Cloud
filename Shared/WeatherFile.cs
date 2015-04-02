// -----------------------------------------------------------------------
// <copyright file="CreateWeatherFile.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using System;
    using System.Data;
    using System.IO;
    using System.Net;
    using System.Collections.Generic;

    /// <summary>
    /// Creates a custom built weather file
    /// </summary>
    public class WeatherFile
    {
        /// <summary>
        /// A list of fields to ignore when overlaying data.
        /// </summary>
        private static string[] fieldsToInclude = new string[] { "radn", "maxt", "mint", "rain" };

        /// <summary>The weatherfiles that have been written</summary>
        private List<string> weatherfilesWritten = new List<string>();

        /// <summary>Gets the names of all files created.</summary>
        public string[] FilesCreated { get; private set; }

        /// <summary>Gets the last SILO date found. Returns DateTime.MinValue if no data.</summary>
        public DateTime LastSILODateFound { get; private set; }

        /// <summary>Creates a one season weather file.</summary>
        /// <param name="fileName">Name of the file to create</param>
        /// <param name="stationNumber">The SILO station number to use.</param>
        /// <param name="startDate">The start date of the weather file.</param>
        /// <param name="observedData">The observed data to use. Can be null</param>
        public void CreateOneSeason(string fileName, int stationNumber,
                                    DateTime startDate,
                                    DateTime endDate,
                                    DataTable observedData)
        {
            Utility.ApsimTextFile weatherFile = ExtractMetFromSILO(stationNumber, startDate, endDate);
            if (weatherFile != null)
            {
                DataTable weatherData = weatherFile.ToTable();
                if (weatherData.Rows.Count == 0)
                    LastSILODateFound = DateTime.MinValue;
                else
                    LastSILODateFound = Utility.DataTable.GetDateFromRow(weatherData.Rows[weatherData.Rows.Count - 1]);

                // Add a codes column to weatherdata
                AddCodesColumn(weatherData, 'S');

                if (observedData != null)
                {
                    AddCodesColumn(observedData, 'O');
                    OverlayData(observedData, weatherData);
                }

                double latitude = Convert.ToDouble(weatherFile.Constant("Latitude").Value);
                double longitude = Convert.ToDouble(weatherFile.Constant("Longitude").Value);
                double tav = Convert.ToDouble(weatherFile.Constant("tav").Value);
                double amp = Convert.ToDouble(weatherFile.Constant("amp").Value);
                WriteWeatherFile(weatherData, fileName, latitude, longitude, tav, amp);
                weatherFile.Close();

                FilesCreated = new string[1] { fileName };
            }
            else
                FilesCreated = new string[0];
        }

        /// <summary>Creates a long term weather file.</summary>
        /// <param name="fileName">Name of the file to create.</param>
        /// <param name="stationNumber">The SILO station number to use.</param>
        /// <param name="startDate">The start date of the weather file.</param>
        /// <param name="nowDate">The end date for using observed data.</param>
        /// <param name="observedData">The observed data to use. Can be null.</param>
        /// <param name="numYears">Number of years for create weather file for.</param>
        public void CreateLongTerm(string fileName, int stationNumber,
                                   DateTime startDate,
                                   DateTime endDate,
                                   DateTime nowDate,
                                   DataTable observedData,
                                   int numYears)
        {

            if (!AlreadyWritten(fileName))
            {
                DateTime longTermStartDate = startDate.AddYears(-numYears);
                Utility.ApsimTextFile weatherFile = ExtractMetFromSILO(stationNumber, longTermStartDate, DateTime.Now);
                DataTable weatherData = weatherFile.ToTable();
                double latitude = Convert.ToDouble(weatherFile.Constant("Latitude").Value);
                double longitude = Convert.ToDouble(weatherFile.Constant("Longitude").Value);
                double tav = Convert.ToDouble(weatherFile.Constant("tav").Value);
                double amp = Convert.ToDouble(weatherFile.Constant("amp").Value);
                if (weatherData.Rows.Count == 0)
                    LastSILODateFound = DateTime.MinValue;
                else
                    LastSILODateFound = Utility.DataTable.GetDateFromRow(weatherData.Rows[weatherData.Rows.Count - 1]);

                string workingFolder = Path.GetDirectoryName(fileName);
                WriteDecileFile(weatherData, startDate, workingFolder);

                // Add a codes and date column to weatherdata
                AddCodesColumn(weatherData, 'H');
                if (observedData != null)
                    AddCodesColumn(observedData, 'O');
                AddDateToTable(weatherData);

                // Need to create a patch data table from the observed data and the SILO data 
                // between the 'startDate' and the 'now' date.
                DataTable patchData = CreatePatchFile(weatherData, observedData, startDate, nowDate);

                List<string> fileNamesCreated = new List<string>();

                int numberOfDays = (endDate - startDate).Days;
                for (int year = startDate.Year - numYears; year < startDate.Year; year++)
                {
                    DateTime startDateForYear = new DateTime(year, startDate.Month, startDate.Day);
                    DateTime endDateForYear = startDateForYear.AddDays(numberOfDays);
                    DataView yearlyDataView = new DataView(weatherData);
                    yearlyDataView.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}# and Date <= #{1:yyyy-MM-dd}#",
                                                                startDateForYear, endDateForYear);
                    DataTable yearlyData = yearlyDataView.ToTable();

                    // Change the dates in yearlyData to the start date.
                    DateTime rowDate = startDate;
                    foreach (DataRow row in yearlyData.Rows)
                    {
                        row["Date"] = rowDate;
                        rowDate = rowDate.AddDays(1);
                    }

                    OverlayData(patchData, yearlyData);
                    string weatherFileName = Path.Combine(workingFolder, Path.GetFileNameWithoutExtension(fileName) + year.ToString() + ".met");
                    WriteWeatherFile(yearlyData, weatherFileName,
                                        latitude, longitude, tav, amp);
                    fileNamesCreated.Add(Path.GetFileName(weatherFileName));
                }

                FilesCreated = fileNamesCreated.ToArray();
            }
        }

        /// <summary>Creates a monthly decile weather file</summary>
        /// <param name="weatherData">The weather data.</param>
        /// <param name="startDate">First date for decile table.</param>
        /// <results>Montly decile data.</results>
        private static void WriteDecileFile(DataTable weatherData, DateTime startDate, string workingDirectory)
        {
            DataTable decileRain = CreateDecileWeather(weatherData, startDate);
            StreamWriter decileWriter = new StreamWriter(Path.Combine(workingDirectory, "Decile.out"));

            decileWriter.WriteLine("      Date RainDecile1 RainDecile5 RainDecile9");
            decileWriter.WriteLine("(dd/mm/yyyy)      (mm)        (mm)        (mm)");
            foreach (DataRow row in decileRain.Rows)
                decileWriter.WriteLine("{0:dd/MM/yyyy}{1,12:F1}{2,12:F1}{3,12:F1}",
                                       new object[] {row["Date"], row["RainDecile1"],
                                                     row["RainDecile5"], row["RainDecile9"]});

            decileWriter.Close();
        }



        /// <summary>Creates a monthly decile weather DataTable</summary>
        /// <param name="weatherData">The weather data.</param>
        /// <param name="startDate">First date for decile table.</param>
        /// <results>Montly decile data.</results>
        private static DataTable CreateDecileWeather(DataTable weatherData, DateTime startDate)
        {
            DateTime firstDate = Utility.DataTable.GetDateFromRow(weatherData.Rows[0]);

            // Create an array of lists, 1 for each month.
            List<double>[] sumsForEachMonth = new List<double>[12];
            for (int i = 0; i < 12; i++)
                sumsForEachMonth[i] = new List<double>();

            double sum = 0.0;
            foreach (DataRow row in weatherData.Rows)
            {
                // Get the date and rain for the row.
                DateTime rowDate = Utility.DataTable.GetDateFromRow(row);
                double value = Convert.ToDouble(row["rain"]);

                // Accumulate the value every day.
                sum += value;

                // At the end of each month, store the accumulated value into the right array element.
                if (rowDate.AddDays(1).Day == 1)  // end of month?
                    sumsForEachMonth[rowDate.Month-1].Add(sum);

                if (rowDate.Day == 1 && rowDate.Month == firstDate.Month)
                    sum = value;
            }

            DataTable decile = new DataTable();
            decile.Columns.Add("Date", typeof(DateTime));
            decile.Columns.Add("RainDecile1", typeof(double));
            decile.Columns.Add("RainDecile5", typeof(double));
            decile.Columns.Add("RainDecile9", typeof(double));

            DateTime decileDate = new DateTime(startDate.Year, startDate.Month, 1); ;
            for (int i = 0; i < 12; i++)
            {
                DataRow row = decile.NewRow();
                row["Date"] = decileDate;
                if (i == 0)
                {
                    row["RainDecile1"] = 0;
                    row["RainDecile5"] = 0;
                    row["RainDecile9"] = 0;
                }
                else
                {
                    row["RainDecile1"] = GetValueForProbability(10, sumsForEachMonth[decileDate.Month - 1].ToArray());
                    row["RainDecile5"] = GetValueForProbability(50, sumsForEachMonth[decileDate.Month - 1].ToArray());
                    row["RainDecile9"] = GetValueForProbability(90, sumsForEachMonth[decileDate.Month - 1].ToArray());
                }

                decile.Rows.Add(row);
                decileDate = decileDate.AddMonths(1);
            }

            return decile;
        }

        /// <summary>Gets the value for probability.</summary>
        /// <param name="probability">The probability.</param>
        /// <param name="values">The values.</param>
        /// <returns></returns>
        private static double GetValueForProbability(double probability, double[] values)
        {
            double[] probValues = Utility.Math.ProbabilityDistribution(values.Length, false);
            Array.Sort(values);
            for (int i = 0; i < probValues.Length; i++)
            {
                if (probValues[i] >= probability)
                    return values[i];
            }

            return probValues[probValues.Length - 1];  // last element.
        }

        /// <summary>Returns true if the specified file has already been writen.</summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns>True if written. False otherwise.</returns>
        private bool AlreadyWritten(string fileName)
        {
            bool found = false;

            string baseFileName = Path.GetFileNameWithoutExtension(fileName);
            foreach (string fileNameCreated in weatherfilesWritten)
                if (baseFileName.StartsWith(fileNameCreated))
                    found = true;
            
            if (!found)
                weatherfilesWritten.Add(baseFileName);
            return found;
        }

        /// <summary>Creates a patch file from the SILO data and the observed data for
        /// the dates between 'startDate' and 'endDate'</summary>
        /// <param name="SILOData">The SILO data.</param>
        /// <param name="observedData">The observed data.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date</param>
        /// <returns>The patch data</returns>
        private static DataTable CreatePatchFile(DataTable SILOData, DataTable observedData, 
                                                DateTime startDate, DateTime endDate)
        {
            DataTable table;
            DataView yearlyDataView = new DataView(SILOData);
            yearlyDataView.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}# and Date <= #{1:yyyy-MM-dd}#",
                                                     startDate, endDate);

            table = yearlyDataView.ToTable();
            AddCodesColumn(table, 'S');
            if (observedData != null)
                OverlayData(observedData, table);
            return table;
        }

        /// <summary>Adds the codes column to the specified weatherData.</summary>
        /// <param name="weatherData">The weather data.</param>
        private static void AddCodesColumn(DataTable weatherData, char code)
        {
            if (!weatherData.Columns.Contains("codes"))
                weatherData.Columns.Add("codes", typeof(string));
            string codeString = new string(code, fieldsToInclude.Length);
            foreach (DataRow row in weatherData.Rows)
                row["codes"] = codeString;
        }

        /// <summary>
        /// Write the specified data table to a text file.
        /// </summary>
        /// <param name="weatherData">The data to write.</param>
        /// <param name="fileName">The name of the file to create.</param>
        private static void WriteWeatherFile(DataTable weatherData, string fileName,
                                             double latitude, double longitude,
                                             double tav, double amp)
        {
            StreamWriter writer = new StreamWriter(fileName);
            writer.WriteLine("Latitude = " + latitude.ToString());
            writer.WriteLine("Longitude = " + longitude.ToString());
            writer.WriteLine("TAV = " + tav.ToString());
            writer.WriteLine("AMP = " + amp.ToString());
            writer.WriteLine("! Codes: S=SILO, H=Historical SILO, O=Observed, P=POAMA");

            // Write headings and units
            writer.WriteLine("        Date     radn maxt mint rain codes");
            writer.WriteLine("(yyyy-mm-dd) (MJ/m^2) (oC) (oC) (mm)    ()");

            // Write data.
            foreach (DataRow row in weatherData.Rows)
            {
                writer.WriteLine("{0,12:yyyy-MM-dd}{1,9:F1}{2,5:F1}{3,5:F1}{4,5:F1}{5,6}",
                                 new object[] {Utility.DataTable.GetDateFromRow(row),
                                               row["radn"],
                                               row["maxt"],
                                               row["mint"],
                                               row["rain"],
                                               row["codes"]});
            }
            
            writer.Close();
        }

        /// <summary>
        /// Overlay data from table1 on top of table2 using the date in each row.
        /// </summary>
        /// <param name="table1">First data table</param>
        /// <param name="table2">The data table that will change</param>
        private static void OverlayData(DataTable table1, DataTable table2)
        {
            if (table2.Rows.Count > 0)
            {
                // This algorithm assumes that table2 does not have missing days.
                DateTime firstDate = Utility.DataTable.GetDateFromRow(table2.Rows[0]);

                foreach (DataRow table1Row in table1.Rows)
                {
                    DateTime table1Date = Utility.DataTable.GetDateFromRow(table1Row);

                    int table2RowIndex = (table1Date - firstDate).Days;
                    if (table2RowIndex >= 0 && table2RowIndex < table2.Rows.Count)
                    {
                        DataRow table2Row = table2.Rows[table2RowIndex];
                        if (Utility.DataTable.GetDateFromRow(table2Row) == table1Date)
                        {
                            // Found the matching row
                            OverlayRowData(table1Row, table2Row);
                        }
                        else
                            throw new Exception("Non consecutive dates found in SILO data");
                    }
                    else
                    {
                        // Table 1 data is outside the range of table 2.
                    }
                }
            }
        }

        /// <summary>
        /// Overlay data of fromRow into toRow where the columns match
        /// </summary>
        /// <param name="fromRow">From row</param>
        /// <param name="toRow">To row</param>
        private static void OverlayRowData(DataRow fromRow, DataRow toRow)
        {
            char[] fromRowCodes = fromRow["codes"].ToString().ToCharArray();
            char[] toRowCodes = toRow["codes"].ToString().ToCharArray();
            
            foreach (DataColumn fromColumn in fromRow.Table.Columns)
            {
                if (Utility.String.Contains(fieldsToInclude, fromColumn.ColumnName))
                {
                    // See if this column is in table2.
                    foreach (DataColumn toColumn in toRow.Table.Columns)
                    {
                        if (toColumn.ColumnName.Equals(fromColumn.ColumnName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Matching column - move data from table1Row to table2Row
                            if (!Convert.IsDBNull(fromRow[fromColumn]))
                            {
                                toRow[toColumn] = fromRow[fromColumn];

                                // Update codes
                                int codeIndex = Utility.String.IndexOfCaseInsensitive(fieldsToInclude, toColumn.ColumnName);
                                toRowCodes[codeIndex] = fromRowCodes[codeIndex];
                            }
                        }
                    }
                }
            }
            toRow["codes"] = new string(toRowCodes);
        }

        /// <summary>Extracts weather data from silo.</summary>
        /// <param name="stationNumber">The station number.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <exception cref="System.Exception">Cannot find SILO!</exception>
        /// <returns>The APSIM text file from SILO</returns>
        private static Utility.ApsimTextFile ExtractMetFromSILO(int stationNumber, DateTime startDate, DateTime endDate)
        {
            if (startDate < DateTime.Now)
            {
                string serverAddress = "http://apsrunet.apsim.info/cgi-bin";
                HttpWebRequest SILO = null;
                HttpWebResponse SILOResponse = null;
                MemoryStream siloStream = new MemoryStream();
                try
                {
                    string requestString = serverAddress + "/getData.tcl?format=apsim&station=" +
                                           stationNumber.ToString() +
                                           "&ddStart=" + startDate.Day.ToString() +
                                           "&mmStart=" + startDate.Month.ToString() +
                                           "&yyyyStart=" + startDate.Year.ToString() +
                                           "&ddFinish=" + endDate.Day.ToString() +
                                           "&mmFinish=" + endDate.Month.ToString() +
                                           "&yyyyFinish=" + endDate.Year.ToString();

                    SILO = (HttpWebRequest)WebRequest.Create(requestString);
                    SILOResponse = (HttpWebResponse)SILO.GetResponse();
                    Stream streamResponse = SILOResponse.GetResponseStream();


                    // Reads 1024 characters at a time.    
                    byte[] read = new byte[1024];
                    int count = streamResponse.Read(read, 0, 1024);
                    while (count > 0)
                    {
                        // Dumps the 1024 characters into our memory stream.
                        siloStream.Write(read, 0, count);
                        count = streamResponse.Read(read, 0, 1024);
                    }

                    // Convert the memory stream to a data table.
                    siloStream.Seek(0, SeekOrigin.Begin);
                    Utility.ApsimTextFile inputFile = new Utility.ApsimTextFile();
                    inputFile.Open(siloStream);
                    return inputFile;
                }
                catch (Exception)
                {
                    throw new Exception("Cannot find SILO!");
                }
                finally
                {
                    // Releases the resources of the response.
                    if (SILOResponse != null)
                        SILOResponse.Close();
                }
            }

            return null;
        }

        /// <summary>
        /// Add year, month, day and date columns to the specified Table.
        /// </summary>
        private static void AddDateToTable(DataTable table)
        {
            if (!table.Columns.Contains("Date"))
            {
                List<DateTime> dates = new List<DateTime>();
                foreach (DataRow Row in table.Rows)
                    dates.Add(Utility.DataTable.GetDateFromRow(Row));
                Utility.DataTable.AddColumnOfObjects(table, "Date", dates.ToArray());
            }
        }
    }
    
}
