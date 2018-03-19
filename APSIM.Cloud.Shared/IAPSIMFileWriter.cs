using System;
using System.Xml;
using APSIM.Shared.Soils;

namespace APSIM.Cloud.Shared
{
    /// <summary>
    /// Interface for a file writer.
    /// </summary>
    interface IAPSIMFileWriter
    {
        void AddFertilseOperation(Fertilise application);
        void AddIrrigateOperation(Irrigate application);
        void AddResetNitrogenOperation(ResetNitrogen reset);
        void AddResetWaterOperation(ResetWater reset);
        string AddSowingOperation(Sow sowing, bool useEC);
        void AddStubbleRemovedOperation(StubbleRemoved application);
        void AddSurfaceOrganicMatterOperation(ResetSurfaceOrganicMatter reset);
        void AddTillageOperation(Tillage application);
        void NameSimulation(string simulationName);
        void Next10DaysDry();
        void SetDailyOutput();
        void SetErosion(double slope, double slopeLength);
        void SetMonthlyOutput();
        void SetNUnlimited();
        void SetNUnlimitedFromToday();
        void SetReportDate(DateTime reportDate);
        void SetSoil(Soil soil);
        void SetStartEndDate(DateTime startDate, DateTime endDate);
        void SetStubble(string stubbleType, double stubbleMass, int cnratio);
        void SetWeatherFile(string weatherFileName);
        void SetYearlyOutput();
        XmlNode ToXML();
        void WriteDepthFile();
    }
}