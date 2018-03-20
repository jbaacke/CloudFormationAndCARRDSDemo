using System;
using System.Collections.Generic;
using System.Text;

using Amazon.CloudFormation;

namespace CARRDS.Library.Models
{
    public class StackStatusResponse
    {

        // This will be a list of states the stack needs to meet in order to qualify for the actions to take place
        public string ResourceName { get; set; }

        // The status of the resource to react to. 
        public ResourceStatus Status { get; set; }

        // This is my list of actions to take in response to changes in the stack state
        public List<CARRDSAction> CARRDSActions = new List<CARRDSAction>();

        // Returns the proper format of the name of the s3 object to be searched for
        public string GetName()
        {
            return ResourceName + "-" + Status.ToString();
        }

    }
}
