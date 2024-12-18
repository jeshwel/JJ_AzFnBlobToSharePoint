using JJ_AzFnBlobToSharePoint.Core.Contracts;

namespace JJ_AzFnBlobToSharePoint.Core.Models
{
    public class ActivityLog : IMonitorRecord
    {
        public int RunId { get; set; }
        public string ActivityStage { get; set; }
        public string Container { get; set; }
        public string FileName { get; set; }
    }
}
