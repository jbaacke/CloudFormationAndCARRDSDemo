using System;
using System.Collections.Generic;
using System.Text;

namespace CARRDS.Library.Models
{
    public class BuildInformation
    {
        public string SourceBranchName { get; set; }

        public string CommitID { get; set; }

        public string BuildNumber { get; set; }

        public string QueuedBy { get; set; }
    }
}
