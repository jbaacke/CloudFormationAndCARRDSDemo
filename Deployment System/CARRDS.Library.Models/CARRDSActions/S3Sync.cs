using System;
using System.Collections.Generic;
using System.Text;

namespace CARRDS.Library.Models.CARRDSActions
{
    public class S3Sync : CARRDSAction
    {

        // The origin information 
        public string OriginBucket { get; set; }
        public string OriginPath { get; set; }

        // The destination information 
        public string DestinationBucket { get; set; }
        public string DestinationPath { get; set; }

        public S3Sync()
        {
            this.Type = CARRDSActionType.S3BucketSync;
        }

    }
}
