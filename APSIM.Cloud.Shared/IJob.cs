namespace APSIM.Cloud.Shared
{
    using System.Data;
    using System.IO;

    public interface IJob
    {
        DataSet Outputs { get; }
        Stream AllFilesZipped { get; }
    }
}