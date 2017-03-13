using System.Data;
using System.IO;

namespace APSIM.Cloud.Runner.RunnableJobs
{
    public interface IJob
    {
        DataSet Outputs { get; }
        Stream AllFilesZipped { get; }
    }
}