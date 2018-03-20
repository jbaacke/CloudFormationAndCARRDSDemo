using System;
using System.Collections.Generic;
using System.Text;

namespace CARRDS.Library.Models.CARRDSActions
{
    public class RunLambda : CARRDSAction
    {
        // The name of the lambda to be run
        public string LambdaName { get; set; }

        // The payload to send to the lambda to be run
        public string Payload { get; set; }

        public RunLambda()
        {
            this.Type = CARRDSActionType.RunLambda;
        }

    }
}
