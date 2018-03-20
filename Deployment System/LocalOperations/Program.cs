using System;

using CARRDS.Library.Models;
using CARRDS.Library.Models.CARRDSActions;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

namespace LocalOperations
{
    class Program
    {
        static void Main(string[] args)
        {

            CARRDSConfiguration myconfig = new CARRDSConfiguration();

            #region Create a stack status response with one action
            // Create the stack status response
            StackStatusResponse hostingBucketDone = new StackStatusResponse()
            {
                Status = new ResourceStatus("CREATE_COMPLETE"),
                ResourceName = "HostingBucket",
            };
            
            // Create an example S3 sync
            hostingBucketDone.CARRDSActions.Add(new S3Sync()
            {
                Description = "Transfers all of the files from the deployment bucket to the hosting bucket.",

                OriginBucket = "${CARRDS.SourceBucket}",
                OriginPath = "${CARRDS.SourcePath}/Web Resources/",
                DestinationBucket = "${Stack.Outputs.HostingBucketName}",
                DestinationPath = ""
            });
            myconfig.StackStatusResponses.Add(hostingBucketDone);
            #endregion

            #region Create a stack status response with two actions
            // The stack status response
            StackStatusResponse stackDone = new StackStatusResponse()
            {
                Status = new ResourceStatus("CREATE_COMPLETE"),
                ResourceName = "SampleStack",
            };

            // Create an example of another runlambda
            stackDone.CARRDSActions.Add(new RunLambda()
            {
                Description = "Runs a lambda",
                LambdaName = "${Stack.Resources.SeedLambda.Name}",
                Payload = "Hello beautiful world!"
            });

            // Create an example of another runlambda
            stackDone.CARRDSActions.Add(new RunLambda()
            {
                Description = "Runs a lambda",
                LambdaName = "${Stack.Resources.SeedLambda.Name}",
                Payload = "Hello beautiful world!"
            });


            myconfig.StackStatusResponses.Add(stackDone);
            #endregion


            myconfig.Parameters.Add(new Parameter() { ParameterKey = "EnvironmentName", ParameterValue = "" });
            myconfig.Parameters.Add(new Parameter() { ParameterKey = "StackSourceBucket", ParameterValue = "" });
            myconfig.Parameters.Add(new Parameter() { ParameterKey = "StackSourcePath", ParameterValue = "" });

            

            myconfig.save("C:\\temp\\CARRDS.json");

            Console.WriteLine("Done");

            Console.Read();
        }
    }
}