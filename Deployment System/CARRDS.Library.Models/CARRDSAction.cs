using System;
using System.Collections.Generic;
using System.Text;

using CARRDS.Library.Models;

using Amazon.CloudFormation;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace CARRDS.Library.Models
{
    public class CARRDSAction
    {
        // Logical name for action
        public string Name { get; set; }

        // A description for the user to use
        public string Description { get; set; }

        // The type of action to take 
        public CARRDSActionType Type { get; set; }
        
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum CARRDSActionType
    {
        [EnumMember(Value = "S3Sync")]
        S3BucketSync,
        [EnumMember(Value = "RunLambda")]
        RunLambda
    }
}
