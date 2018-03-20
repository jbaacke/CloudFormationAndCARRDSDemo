using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

using Amazon.SimpleNotificationService;
using Amazon.Lambda.SNSEvents;

using Newtonsoft.Json;

using Amazon.CloudFormation;

using CARRDS.Library.Models;
using Amazon.S3;
using Amazon.S3.Util;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;
using System.IO;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CARRDS.Lambda.StackStreamResponse
{
    public class Function
    {
        // The name of the operations bucket
        string OperationsBucket = Environment.GetEnvironmentVariable("OperationsBucket");

        // My S3 Client
        IAmazonS3 S3Client = new AmazonS3Client();

        // CloudFormation client
        static AmazonCloudFormationClient CloudformationClient = new AmazonCloudFormationClient();

        private ILambdaSerializer serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();

        public Function()
        {
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(SNSEvent input, ILambdaContext context)
        {
            foreach (var record in input.Records)
            {
                context.Logger.LogLine(record.Sns.Message);

                JStackEvent evnt = new JStackEvent(record.Sns.Message);

                // Serializer settings are apparantly important. 
                var SerilaizerSettings = new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Objects
                };


                // Find the key I am looking for based on the event
                string s3key = evnt.StackId + "/Tasks/" + evnt.LogicalResourceId + "-" + evnt.ResourceStatus;
                //context.Logger.LogLine("I am looking for the key: " + s3key);

                // Try to read that object out of the operations bucket
                string responseActionString; 
                StackStatusResponse responseAction;
                try
                {
                    GetObjectResponse ActionResponse = await S3Client.GetObjectAsync(OperationsBucket, s3key);
                    responseActionString = new StreamReader(ActionResponse.ResponseStream).ReadToEnd();
                    responseAction = JsonConvert.DeserializeObject<StackStatusResponse>(new StreamReader(ActionResponse.ResponseStream).ReadToEnd(), SerilaizerSettings);
                    context.Logger.LogLine("Found an object for: " + s3key);
                    context.Logger.LogLine(JsonConvert.SerializeObject(responseAction, SerilaizerSettings));
                }
                catch (Exception e)
                {
                    if (e.Message.Equals("The specified key does not exist."))
                    { return "No action could be found meeting the resource-status criterion"; }
                    else
                    {
                        context.Logger.Log(e.Message);
                        context.Logger.Log(e.StackTrace);
                        context.Logger.Log("No Action could be found");
                        throw e;
                    }
                }

                // Get details of my stack
                var StackResources = await CloudformationClient.DescribeStackResourcesAsync(new Amazon.CloudFormation.Model.DescribeStackResourcesRequest()
                {
                    StackName = evnt.StackName,
                });
                context.Logger.LogLine(JsonConvert.SerializeObject(StackResources));


                





            }
            return input.ToString();
        }
    }
}
