// -----------------------------------------------------------------------
// <copyright file="CreateWeatherFile.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Shared
{
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;


    /// <summary>
    /// Creates a custom built weather file
    /// </summary>
    public class Weather
    {
        /// <summary>
        /// A list of fields to ignore when overlaying data.
        /// </summary>
        private static string[] fieldsToOverlay = new string[] { "radn", "maxt", "mint", "rain" };

        /// <summary>Creates a long term weather file.</summary>
        /// <param name="fileName">Name of the file to create.</param>
        /// <param name="stationNumber">The SILO station number to use.</param>
        /// <param name="startDate">The start date of the weather file.</param>
        /// <param name="nowDate">The end date for using observed data.</param>
        /// <param name="observedData">The observed data to use. Can be null.</param>
        /// <param name="decileDate">The date to start the decile file.</param>
        /// <param name="numYears">Number of years for create weather file for.</param>
        /// <returns>The list of file names that were created.</returns>
        public static string[] CreateLongTerm(string fileName, int stationNumber,
                                              DateTime startDate,
                                              DateTime nowDate,
                                              DataTable observedData,
                                              DateTime decileDate,
                                              int numYears)
        {
            // Get the longterm (numYears) SILO weather data.
            Data  weatherFileData = ExtractDataFromSILO(stationNumber, startDate.AddYears(-numYears), DateTime.Now);

            if (decileDate != DateTime.MinValue)
            {
                DataTable decileData = CreateDecileWeather(weatherFileData.Table, startDate);
                WriteDecileFile(decileData, decileDate, Path.Combine(Path.GetDirectoryName(fileName), "Decile.out"));
            }

            // Duplicate the maxt and mint columns to origmaxt and origmint columns. 
            // This is so that we have both the patched and unpatched data.
            CloneColumn(weatherFileData.Table, "maxt", "origmaxt");
            CloneColumn(weatherFileData.Table, "mint", "origmint");

            // Need to create a patch data table from the observed data and the SILO data 
            // between the 'startDate' and the 'now' date.
            DataTable patchData = CopyDataToNewTableWithDateRange(weatherFileData.Table, startDate, nowDate);
            OverlayData(observedData, patchData);

            // Overlay our patch data over all years in the weather data.
            OverlayDataAllYears(patchData, weatherFileData.Table);

            List<string> filesCreated = new List<string>();
            for (DateTime date = weatherFileData.FirstDate; date.Year < startDate.Year; date = date.AddYears(1))
            {
                // Get a view of the data for this year up until the NaN data starts.
                DataTable yearlyData = CreateView(weatherFileData.Table, date, date.AddYears(1).AddDays(-1)).ToTable();

                // Change the years to always be the first year of poama data.
                RedateData(yearlyData, startDate);

                // Write the file.
                string extension = Path.GetExtension(fileName);
                string yearlyFileName = fileName.Replace(extension, date.Year + extension);
                WriteWeatherFile(yearlyData, yearlyFileName, weatherFileData.Latitude, weatherFileData.Longitude, weatherFileData.TAV, weatherFileData.AMP);
                filesCreated.Add(yearlyFileName);
            }
            return filesCreated.ToArray();
        }

        /// <summary>Creates a long term weather file.</summary>
        /// <param name="fileName">Name of the file to create.</param>
        /// <param name="stationNumber">The SILO station number to use.</param>
        /// <param name="startDate">The start date of the weather file.</param>
        /// <param name="nowDate">The end date for using observed data.</param>
        /// <param name="observedData">The observed data to use. Can be null.</param>
        /// <returns>The list of file names that were created.</returns>
        public static string[] CreatePOAMA(string fileName, int stationNumber,
                                           DateTime startDate,
                                           DateTime nowDate,
                                           DataTable observedData)
        {
            // Get the SILO weather data from the start date
            Data weatherFileData = ExtractDataFromSILO(stationNumber, startDate, nowDate);

            // Overlay the observed data over the SILO data.
            OverlayData(observedData, weatherFileData.Table);

            // Get the POAMA data beginning from the last date of SILO data.
            Data poamaData = ExtractDataFromPOAMA(stationNumber, weatherFileData.LastDate);

            // Looks like a bug in POAMA where if we ask for date 2016-05-11, poama returns data for that
            // date in the first year only. Subsequent years the poama data starts on the 2016-05-12.
            // To get around this we ask for the 2016-05-10 and remove the first row. All years should
            // then be fine.
            poamaData.Table.Rows.RemoveAt(0);

            // Redate the POAMA data. For some reason the year is 100 years in the future e.g. 2116.
            RedateData(poamaData.Table, poamaData.FirstDate.AddYears(-100));
            if (weatherFileData.LastDate != poamaData.FirstDate.AddDays(-1))
                throw new Exception("The end of the SILO data doesn't match the beginning of the POAMA data");

            // copy the silo data to the front of the poama data.
            DataTableUtilities.InsertRowsAt(weatherFileData.Table, poamaData.Table, 0);

            // overlay the silo data on top of the poama data for all years.
            OverlayDataAllYears(weatherFileData.Table, poamaData.Table);

            List<string> filesCreated = new List<string>();
            for (DateTime date = poamaData.FirstDate; date < poamaData.LastDate; date = date.AddYears(1))
            {
                // Get a view of the data for this year up until the NaN data starts.
                DataView yearlyView = CreateView(poamaData.Table, date, date.AddYears(1).AddDays(-1));
                DateTime startOfNaNDate = FindStartOfNaNData(yearlyView);
                DataTable yearlyGoodData = CreateView(poamaData.Table, date, startOfNaNDate.AddDays(-1)).ToTable();

                // Change the years to always be the first year of poama data.
                RedateData(yearlyGoodData, poamaData.FirstDate);

                // Write the file.
                string extension = Path.GetExtension(fileName);
                string yearlyFileName = fileName.Replace(extension, date.Year + extension);
                WriteWeatherFile(yearlyGoodData, yearlyFileName, weatherFileData.Latitude, weatherFileData.Longitude, weatherFileData.TAV, weatherFileData.AMP);
                filesCreated.Add(yearlyFileName);
            }

            return filesCreated.ToArray();
        }

        /// <summary>Create a data view for a date range.</summary>
        /// <param name="data">The raw data table.</param>
        /// <param name="firstDate">The first date.</param>
        /// <param name="lastDate">The last date.</param>
        /// <returns></returns>
        private static DataView CreateView(DataTable data, DateTime firstDate, DateTime lastDate)
        {
            DataView view = new DataView(data);
            view.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}# and Date <= #{1:yyyy-MM-dd}#",
                                            firstDate, lastDate);
            return view;
        }

        /// <summary>Find the date where the NaN values begin. Will return the last date + 1 if none found.</summary>
        /// <param name="view">The view to look through.</param>
        /// <returns>The date</returns>
        private static DateTime FindStartOfNaNData(DataView view)
        {
            foreach (DataRowView row in view)
            {
                if (float.IsNaN((float)row[1]))
                    return (DateTime)row[0];
            }

            DateTime lastDate = (DateTime)view[view.Count - 1][0];
            return lastDate.AddDays(1);
        }

        /// <summary>Redate the data starting from the specified date.</summary>
        /// <param name="data">The data table to redate.</param>
        /// <param name="startDate">The first date</param>
        private static void RedateData(DataTable data, DateTime startDate)
        {
            DateTime rowDate = startDate;
            foreach (DataRow row in data.Rows)
            {
                row["Date"] = rowDate;
                rowDate = rowDate.AddDays(1);
            }
        }

        /// <summary>Clone a column in the specified table. Adds the new column at the end of the column list.</summary>
        /// <param name="data">The data table with the column.</param>
        /// <param name="columnName">The name of the column to clone.</param>
        public static void CloneColumn(DataTable data, string columnName, string newColumnName)
        {
            // Duplicate the maxt and mint columns to origmaxt and origmint columns. 
            // This is so that we have both the patched and unpatched data.
            data.Columns.Add(newColumnName, typeof(double));
            foreach (DataRow row in data.Rows)
            {
                row[newColumnName] = Convert.ToDouble(row[columnName]);
                row["codes"] = row["codes"].ToString() + "-";
            }

            // Move the codes column to the end.
            data.Columns["codes"].SetOrdinal(data.Columns.Count - 1);

        }

        /// <summary>Sets the year in date column.</summary>
        /// <remarks>
        ///     The patch data can go over a year i.e. starts in 2014 and goes into 2015.
        ///     This method doesn't want to set all years to the one specified, rather
        ///     it needs to work out what value needs to be subtracted from all years 
        ///     such that the first year of patch data is the same as the year specified.
        /// </remarks>
        /// <param name="patchData">The patch data.</param>
        /// <param name="year">The year to set the date to.</param>
        private static void SetYearInDateColumn(DataTable patchData, int year)
        {
            int firstYear = DataTableUtilities.GetDateFromRow(patchData.Rows[0]).Year;
            int offset = year - firstYear;

            DateTime[] dates = DataTableUtilities.GetColumnAsDates(patchData, "Date");
            for (int i = 0; i < dates.Length; i++)
            {
                if (DateTime.IsLeapYear(dates[i].Year) && !DateTime.IsLeapYear(dates[i].Year + offset) &&
                    dates[i].Month == 2 && dates[i].Day == 29)
                    dates[i] = new DateTime(dates[i].Year + offset, dates[i].Month, 28);
                else
                    dates[i] = new DateTime(dates[i].Year + offset, dates[i].Month, dates[i].Day);
            }
            DataTableUtilities.AddColumnOfObjects(patchData, "Date", dates);
        }

        /// <summary>
        /// Extracts weather data from silo for the specified station number, between the 
        /// specified dates. The private variables: latitude, longitude, tav, amp and 
        /// LastSILODateFound will be set to the values from the SILO file.
        /// </summary>
        /// <param name="stationNumber">The station number.</param>
        /// <param name="observedData">The observed data.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <returns>The data returned will have year, month, day, date and code columns.</returns>
        public static Data ExtractDataFromSILO(int stationNumber, DateTime startDate, DateTime endDate)
        {
            if (startDate.Date == endDate.Date)
                throw new Exception("The start date and end date for extracting data from SILO are both: " + startDate.Date.ToString());
            ApsimTextFile weatherFile = ExtractMetFromSILO(stationNumber, startDate, endDate);

            // Add a codes and date column to weatherdata
            DataTable weatherData = weatherFile.ToTable();
            if (weatherData.Rows.Count == 0)
                throw new Exception("No weather data found for station " + stationNumber);
            AddCodesColumn(weatherData, 'S');
            AddDateToTable(weatherData);

            // Return the info object.
            return new Data(weatherData,
                latitude: Convert.ToDouble(weatherFile.Constant("Latitude").Value),
                longitude: Convert.ToDouble(weatherFile.Constant("Longitude").Value),
                tav: Convert.ToDouble(weatherFile.Constant("tav").Value),
                amp: Convert.ToDouble(weatherFile.Constant("amp").Value));
        }

        /// <summary>
        /// Extracts climate forecast weather data from POAMA for the specified station number, for the 
        /// specified date.
        /// </summary>
        /// <param name="stationNumber">The station number.</param>
        /// <param name="startDate">The date to extract the forecast for.</param>
        public static Data ExtractDataFromPOAMA(int stationNumber, DateTime fromDate)
        {
            ApsimTextFile weatherFile = ExtractMetFromPOAMA(stationNumber, fromDate);

            // Add a codes and date column to weatherdata
            DataTable weatherData = weatherFile.ToTable();
            AddCodesColumn(weatherData, 'P');
            AddDateToTable(weatherData);

            // Return the info object.
            return new Data(weatherData,
                latitude: Convert.ToDouble(weatherFile.Constant("Latitude").Value),
                longitude: Convert.ToDouble(weatherFile.Constant("Longitude").Value),
                tav: Convert.ToDouble(weatherFile.Constant("tav").Value),
                amp: Convert.ToDouble(weatherFile.Constant("amp").Value));
        }

        /// <summary>
        /// Extracts radiation data from BOM for the specified station number, between the 
        /// specified dates.
        /// </summary>
        /// <param name="stationNumber">The station number.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        public static Data ExtractRadiationDataFromBOM(int stationNumber, DateTime startDate, DateTime endDate)
        {
            string url = "http://www.bom.gov.au/jsp/ncc/cdio/weatherData/av?p_display_type=dailyZippedDataFile&p_stn_num=" +
                        stationNumber.ToString() +
                        "&ddStart=" + startDate.Day.ToString() +
                        "&mmStart=" + startDate.Month.ToString() +
                        "&yyyyStart=" + startDate.Year.ToString() +
                        "&ddFinish=" + endDate.Day.ToString() +
                        "&mmFinish=" + endDate.Month.ToString() +
                        "&yyyyFinish=" + endDate.Year.ToString();
            ApsimTextFile weatherFile = ExtractDataFromURL(url);

            // Add a codes and date column to weatherdata
            DataTable weatherData = weatherFile.ToTable();
            AddCodesColumn(weatherData, 'S');
            AddDateToTable(weatherData);

            // Return the info object.
            return new Data(weatherData,
                latitude: Convert.ToDouble(weatherFile.Constant("Latitude").Value),
                longitude: Convert.ToDouble(weatherFile.Constant("Longitude").Value),
                tav: Convert.ToDouble(weatherFile.Constant("tav").Value),
                amp: Convert.ToDouble(weatherFile.Constant("amp").Value));
        }

        /// <summary>Creates a monthly decile weather file</summary>
        /// <param name="decileRain">The decile data to write.</param>
        /// <param name="startDate">First date for decile table.</param>
        /// <param name="fileName">The file name to write.</param>
        /// <results>Montly decile data.</results>
        private static void WriteDecileFile(DataTable decileRain, DateTime startDate, string fileName)
        {
            StreamWriter decileWriter = new StreamWriter(fileName);

            decileWriter.WriteLine("      Date RainDecile1 RainDecile5 RainDecile9");
            decileWriter.WriteLine("(dd/mm/yyyy)      (mm)        (mm)        (mm)");
            foreach (DataRow row in decileRain.Rows)
                decileWriter.WriteLine("{0:dd/MM/yyyy}{1,12:F1}{2,12:F1}{3,12:F1}",
                                       new object[] {row["Date"], row["RainDecile1"],
                                                     row["RainDecile5"], row["RainDecile9"]});

            decileWriter.Close();
        }

        /// <summary>
        /// Calculate the rainfall deciles for each decile for each month
        /// </summary>
        /// <param name="stationNumber"></param>
        /// <param name="startDate">Start date for the calculations</param>
        /// <param name="endDate"></param>
        /// <returns>The deciles array</returns>
        public static double[,] CalculateRainDeciles(int stationNumber, DateTime startDate, DateTime endDate)
        {
            Data weatherFileData = ExtractDataFromSILO(stationNumber, startDate, endDate);
            DataTable weatherData = weatherFileData.Table;
                        
            if (weatherData != null)
            {
                return CreatePercentileWeather(weatherData, startDate);
            }
            return null;
        }

        /// <summary>
        /// Create the array of monthly deciles for each month from the startDate.
        /// </summary>
        /// <param name="weatherData">The raw daily weather data</param>
        /// <param name="startDate">The starting date. The month is the start of the season.</param>
        /// <returns>Array of monthly deciles (from 10 - 100)</returns>
        private static double[,] CreatePercentileWeather(DataTable weatherData, DateTime startDate)
        {
            DateTime firstDate = DataTableUtilities.GetDateFromRow(weatherData.Rows[0]);
            DataView weatherView = new DataView(weatherData);
            weatherView.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}#", new DateTime(firstDate.Year, startDate.Month, startDate.Day));

            // Create an array of lists, 1 for each month.
            List<double>[] sumsForEachMonth = new List<double>[12];
            for (int i = 0; i < 12; i++)
                sumsForEachMonth[i] = new List<double>();

            int currentMonth = startDate.Month;
            double sum = 0.0;
            double value;
            foreach (DataRowView row in weatherView)
            {
                // Get the date and rain for the row.
                DateTime rowDate = DataTableUtilities.GetDateFromRow(row.Row);
                value = Convert.ToDouble(row["rain"]);              // get rain value
                if (currentMonth != rowDate.Month)                  // if new month then
                {
                    sumsForEachMonth[currentMonth - 1].Add(sum);    // store the month sum
                    if (rowDate.Month == startDate.Month)           // if back at the start of yearly period
                        sum = value;
                    currentMonth = rowDate.Month;
                }
                else
                {
                    sum += value;                                   //accumulate
                }
            }

            double[,] monthlyDeciles = new double[12,10];

            DateTime decileDate = new DateTime(startDate.Year, startDate.Month, 1); ;
            for (int i = 0; i < 12; i++)
            {
                double[] sums = new double[sumsForEachMonth[i].Count];
                Array.Copy(sumsForEachMonth[i].ToArray(), sums, sumsForEachMonth[i].Count);
                Array.Sort(sums);

                for (int dec = 1; dec <= 10; dec++)
                {
                    monthlyDeciles[i, dec - 1] = MathUtilities.Percentile(sums, dec * 0.1);
                }
            }
            return monthlyDeciles;
        }

        /// <summary>Creates a monthly decile weather DataTable</summary>
        /// <param name="weatherData">The weather data.</param>
        /// <param name="startDate">First date for decile table.</param>
        /// <results>Montly decile data.</results>
        private static DataTable CreateDecileWeather(DataTable weatherData, DateTime startDate)
        {
            DateTime firstDate = DataTableUtilities.GetDateFromRow(weatherData.Rows[0]);
            DataView weatherView = new DataView(weatherData);
            weatherView.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}#", new DateTime(firstDate.Year, startDate.Month, startDate.Day));

            // Create an array of lists, 1 for each month.
            List<double>[] sumsForEachMonth = new List<double>[12];
            for (int i = 0; i < 12; i++)
                sumsForEachMonth[i] = new List<double>();

            double sum = 0.0;
            foreach (DataRowView row in weatherView)
            {
                // Get the date and rain for the row.
                DateTime rowDate = DataTableUtilities.GetDateFromRow(row.Row);
                double value = Convert.ToDouble(row["rain"]);

                // Accumulate the value every day.
                sum += value;

                // At the end of each month, store the accumulated value into the right array element.
                // if (rowDate.AddDays(1).Day == 1)  // end of month?  - GOOD
                if (rowDate.Day == 1)  // end of month?   - REPRODUCE BUG IN YP
                    sumsForEachMonth[rowDate.Month-1].Add(sum);

                if (rowDate.Day == 1 && rowDate.Month == startDate.Month)
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
            double[] probValues = MathUtilities.ProbabilityDistribution(values.Length, false);
            Array.Sort(values);
            for (int i = 0; i < probValues.Length; i++)
            {
                if (probValues[i] >= probability)
                    return values[i];
            }

            return probValues[probValues.Length - 1];  // last element.
        }

        /// <summary>Creates a new data table for a given date range.</summary>
        /// <param name="sourceData">The source data that will be copied to the new table.</param>
        /// <param name="startDate">The first date</param>
        /// <param name="endDate">The last date</param>
        /// <returns>The new data table</returns>
        private static DataTable CopyDataToNewTableWithDateRange(DataTable sourceData, DateTime startDate, DateTime endDate)
        {
            DataView yearlyDataView = new DataView(sourceData);
            yearlyDataView.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}# and Date <= #{1:yyyy-MM-dd}#",
                                                     startDate, endDate);
            return yearlyDataView.ToTable();
        }

        /// <summary>Adds the codes column to the specified weatherData.</summary>
        /// <param name="weatherData">The weather data.</param>
        public static void AddCodesColumn(DataTable weatherData, char code)
        {
            if (!weatherData.Columns.Contains("codes"))
                weatherData.Columns.Add("codes", typeof(string));

            // Count the number of code characters we need.
            int count = 0;
            foreach (string fieldToInclude in fieldsToOverlay)
                if (weatherData.Columns.Contains(fieldToInclude))
                    count++;

            string codeString = new string(code, count);
            foreach (DataRow row in weatherData.Rows)
                row["codes"] = codeString;
        }

        /// <summary>
        /// Write the specified data table to a text file.
        /// </summary>
        /// <param name="weatherData">The data to write.</param>
        /// <param name="fileName">The name of the file to create.</param>
        public static void WriteWeatherFile(DataTable weatherData, string fileName,
                                            double latitude, double longitude,
                                            double tav, double amp)
        {
            WriteWeatherFile(new DataView(weatherData), fileName, latitude, longitude, tav, amp);
        }

        /// <summary>
        /// Write the specified data table to a text file.
        /// </summary>
        /// <param name="weatherData">The data to write.</param>
        /// <param name="fileName">The name of the file to create.</param>
        public static void WriteWeatherFile(DataView weatherData, string fileName,
                                            double latitude, double longitude,
                                            double tav, double amp)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine("[weather.met.weather]");
                writer.WriteLine("Latitude = " + latitude.ToString());
                writer.WriteLine("Longitude = " + longitude.ToString());
                writer.WriteLine("TAV = " + tav.ToString());
                writer.WriteLine("AMP = " + amp.ToString());
                writer.WriteLine("! Codes:");
                writer.WriteLine("!  S    SILO");
                writer.WriteLine("!  O    Observed");
                writer.WriteLine("!  P    POAMA");
                writer.WriteLine("!  s    SILO (patched)");
                writer.WriteLine("!  o    Observed (patched)");
                writer.WriteLine("!  p    POAMA (patched)");

                // Work out column formats and widths.
                string formatString = string.Empty;
                string headings = string.Empty;
                string units = string.Empty;
                int i = 0;
                foreach (DataColumn column in weatherData.Table.Columns)
                {
                    int columnWidth = 0;
                    string columnFormat = string.Empty;
                    string columnUnits = string.Empty;
                    if (column.DataType == typeof(DateTime))
                    {
                        columnFormat += "yyyy-MM-dd";
                        columnWidth = 12;
                        columnUnits = "(yyyy-mm-dd)";
                    }
                    else if (column.ColumnName.Contains("radn"))
                    {
                        columnFormat += "F1";
                        columnWidth = 9;
                        columnUnits = "(MJ/m^2)";
                    }
                    else if (column.ColumnName.Contains("maxt"))
                    {
                        columnFormat += "F1";
                        columnWidth = 9;
                        columnUnits = "(oC)";
                    }
                    else if (column.ColumnName.Contains("mint"))
                    {
                        columnFormat += "F1";
                        columnWidth = 9;
                        columnUnits = "(oC)";
                    }
                    else if (column.ColumnName.Contains("rain"))
                    {
                        columnFormat += "F1";
                        columnWidth = 7;
                        columnUnits = "(mm)";
                    }
                    else
                    {
                        columnFormat += string.Empty;
                        columnWidth = 9;
                        columnUnits = "()";
                    }

                    if (columnWidth > 0)
                    {
                        headings += string.Format("{0," + columnWidth.ToString() + "}", column.ColumnName);
                        units += string.Format("{0," + columnWidth.ToString() + "}", columnUnits);
                        formatString += "{" + i + "," + columnWidth;
                        if (columnFormat != string.Empty)
                            formatString += ":" + columnFormat;
                        formatString += "}";
                        i++;
                    }
                }

                // Write headings and units
                writer.WriteLine(headings);
                writer.WriteLine(units);

                // Write data.
                object[] values = new object[weatherData.Table.Columns.Count];
                foreach (DataRowView row in weatherData)
                {
                    // Create an object array to pass to writeline.
                    for (int c = 0; c < weatherData.Table.Columns.Count; c++)
                        values[c] = row[c];

                    writer.WriteLine(formatString, values);
                }
            }
        }

        /// <summary>
        /// Overlay data in fromData on top of toData for all years found in toData.
        /// </summary>
        /// <param name="fromData">Source data</param>
        /// <param name="toData">Destination data</param>
        public static void OverlayDataAllYears(DataTable fromData, DataTable toData)
        {
            DataTable clonedData = fromData.Copy();
            
            // Loop through all years in the long term weather data and overlay the from data onto
            // each year of the to data
            if (clonedData.Rows.Count > 0)
            {
                int firstYear = DataTableUtilities.GetDateFromRow(toData.Rows[0]).Year;
                int lastYear = DataTableUtilities.GetDateFromRow(toData.Rows[toData.Rows.Count - 1]).Year;
                for (int year = firstYear; year <= lastYear; year++)
                {
                    // Before overlaying the from data we need to change the year because the
                    // OverlayData method uses date matching.
                    SetYearInDateColumn(clonedData, year);

                    // Now overlay the patch data.
                    OverlayData(clonedData, toData, true);
                }
            }
        }

        /// <summary>
        /// Overlay data from table1 on top of table2 using the date in each row. Date
        /// dates in both tables have to exactly match before the data is overlaid.
        /// </summary>
        /// <param name="fromData">First data table</param>
        /// <param name="toData">The data table that will change</param>
        /// <param name="lowercaseCode">Lowercase the code?</param>
        public static void OverlayData(DataTable fromData, DataTable toData, bool lowercaseCode = false)
        {
            if (fromData != null && toData.Rows.Count > 0)
            {
                // This algorithm assumes that toData does not have missing days.
                DateTime firstDate = DataTableUtilities.GetDateFromRow(toData.Rows[0]);
                DateTime lastDate = DataTableUtilities.GetDateFromRow(toData.Rows[toData.Rows.Count-1]);

                // Filter fromData so that it is in the same range as table2.
                DataView table1View = new DataView(fromData);
                table1View.RowFilter = string.Format("Date >= #{0:yyyy-MM-dd}# and Date <= #{1:yyyy-MM-dd}#",
                                                     firstDate, lastDate);

                foreach (DataRowView table1Row in table1View)
                {
                    DateTime table1Date = DataTableUtilities.GetDateFromRow(table1Row.Row);

                    int table2RowIndex = (table1Date - firstDate).Days;
                    if (table2RowIndex >= 0 && table2RowIndex < toData.Rows.Count)
                    {
                        DataRow table2Row = toData.Rows[table2RowIndex];
                        if (DataTableUtilities.GetDateFromRow(table2Row) == table1Date)
                        {
                            // Found the matching row
                            OverlayRowData(table1Row.Row, table2Row, lowercaseCode);
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
        /// <param name="lowercaseCode">Lowercase the code?</param>
        private static void OverlayRowData(DataRow fromRow, DataRow toRow, bool lowercaseCode)
        {
            char[] fromRowCodes = fromRow["codes"].ToString().ToCharArray();
            char[] toRowCodes = toRow["codes"].ToString().ToCharArray();
            
            foreach (DataColumn fromColumn in fromRow.Table.Columns)
            {
                if (StringUtilities.Contains(fieldsToOverlay, fromColumn.ColumnName))
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
                                int fromCodeIndex = fromColumn.Ordinal - 1; // First column in fromRow will be Date but Date doesn't have a code.
                                int toCodeIndex = StringUtilities.IndexOfCaseInsensitive(fieldsToOverlay, toColumn.ColumnName);
                                toRowCodes[toCodeIndex] = fromRowCodes[fromCodeIndex];
                                if (lowercaseCode)
                                    toRowCodes[toCodeIndex] = Char.ToLower(toRowCodes[toCodeIndex]);
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
        public static ApsimTextFile ExtractMetFromSILO(int stationNumber, DateTime startDate, DateTime endDate)
        {
            if (startDate < DateTime.Now)
            {
                string url = "http://apsrunet.apsim.info/cgi-bin/getData.met?format=apsim&station=" +
                                        stationNumber.ToString() +
                                        "&ddStart=" + startDate.Day.ToString() +
                                        "&mmStart=" + startDate.Month.ToString() +
                                        "&yyyyStart=" + startDate.Year.ToString() +
                                        "&ddFinish=" + endDate.Day.ToString() +
                                        "&mmFinish=" + endDate.Month.ToString() +
                                        "&yyyyFinish=" + endDate.Year.ToString();
                return ExtractDataFromURL(url);
            }
            return null;
        }

        /// <summary>Extracts weather data from silo.</summary>
        /// <param name="stationNumber">The station number.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <exception cref="System.Exception">Cannot find SILO!</exception>
        /// <returns>The APSIM text file from SILO</returns>
        public static MemoryStream ExtractMetStreamFromSILO(int stationNumber, DateTime startDate, DateTime endDate)
        {
            if (startDate < DateTime.Now)
            {
                string url = "http://apsrunet.apsim.info/cgi-bin/getData.tcl?format=apsim&station=" +
                                        stationNumber.ToString() +
                                        "&ddStart=" + startDate.Day.ToString() +
                                        "&mmStart=" + startDate.Month.ToString() +
                                        "&yyyyStart=" + startDate.Year.ToString() +
                                        "&ddFinish=" + endDate.Day.ToString() +
                                        "&mmFinish=" + endDate.Month.ToString() +
                                        "&yyyyFinish=" + endDate.Year.ToString();
                return WebUtilities.ExtractDataFromURL(url);
            }
            return null;
        }

        /// <summary>Extracts weather data from POAMA.</summary>
        /// <param name="stationNumber">The station number.</param>
        /// <param name="fromDate">The date from which to extract data</param>
        private static ApsimTextFile ExtractMetFromPOAMA(int stationNumber, DateTime fromDate)
        {
            string url = string.Format("http://www.agforecast.com.au/Forecast/Get?stationNumber={0}&date={1}&rainOnly=False",
                                       stationNumber, fromDate.ToString("yyyy/MM/dd"));
            return ExtractDataFromURL(url);
        }

        /// <summary>Extracts weather data from calling a url and returns an ApsimTextFile.</summary>
        /// <param name="url">The url to call</param>
        private static ApsimTextFile ExtractDataFromURL(string url)
        {
            MemoryStream stream = WebUtilities.ExtractDataFromURL(url);
            if (stream != null)
            {
                // Convert the memory stream to a data table.
                stream.Seek(0, SeekOrigin.Begin);
                ApsimTextFile inputFile = new ApsimTextFile();
                inputFile.Open(stream);
                return inputFile;
            }
            return null;
        }

        /// <summary>
        /// Add year, month, day and date columns to the specified Table.
        /// </summary>
        public static void AddDateToTable(DataTable table)
        {
            if (!table.Columns.Contains("Date"))
            {
                List<DateTime> dates = new List<DateTime>();
                foreach (DataRow Row in table.Rows)
                    dates.Add(DataTableUtilities.GetDateFromRow(Row));
                DataTableUtilities.AddColumnOfObjects(table, "Date", dates.ToArray());
                table.Columns["Date"].SetOrdinal(0);

                // remove year, day, pan, vp, code columns.
                int yearColumn = table.Columns.IndexOf("Year");
                if (yearColumn != -1)
                    table.Columns.RemoveAt(yearColumn);
                int dayColumn = table.Columns.IndexOf("Day");
                if (dayColumn != -1)
                    table.Columns.RemoveAt(dayColumn);
                int panColumn = table.Columns.IndexOf("pan");
                if (panColumn != -1)
                    table.Columns.RemoveAt(panColumn);
                int vpColumn = table.Columns.IndexOf("vp");
                if (vpColumn != -1)
                    table.Columns.RemoveAt(vpColumn);
                int codeColumn = table.Columns.IndexOf("code");
                if (codeColumn != -1)
                    table.Columns.RemoveAt(codeColumn);
            }
        }

        /// <summary>
        /// A simple class for holding data from a weather file.
        /// </summary>
        public class Data
        {
            /// <summary>The latitude</summary>
            public double Latitude { get; private set; }

            /// <summary>The longitude</summary>
            public double Longitude { get; private set; }

            /// <summary>The tav</summary>
            public double TAV { get; private set; }

            /// <summary>The amp</summary>
            public double AMP { get; private set; }

            /// <summary>The daily data.</summary>
            public DataTable Table { get; private set; }

            /// <summary>Returns the first date in the weather data.</summary>
            public DateTime FirstDate
            {
                get
                {
                    if (Table.Rows.Count == 0)
                        return DateTime.MinValue;
                    else
                        return DataTableUtilities.GetDateFromRow(Table.Rows[0]);
                }
            }

            /// <summary>Returns the last date in the weather data.</summary>
            public DateTime LastDate
            {
                get
                {
                    if (Table.Rows.Count == 0)
                        return DateTime.MinValue;
                    else
                        return DataTableUtilities.GetDateFromRow(Table.Rows[Table.Rows.Count - 1]);
                }
            }

            /// <summary>
            /// Constructor for a weather data instance.
            /// </summary>
            /// <param name="data">Daily data.</param>
            /// <param name="latitude">The latitude.</param>
            /// <param name="longitude">The longitude.</param>
            /// <param name="tav">The average temp.</param>
            /// <param name="amp">The temp. amplitude.</param>
            public Data(DataTable data, double latitude, double longitude, 
                        double tav, double amp)
            {
                this.Table = data;
                this.Latitude = latitude;
                this.Longitude = longitude;
                this.TAV = tav;
                this.AMP = amp;
            }
        }
    }
    
}
