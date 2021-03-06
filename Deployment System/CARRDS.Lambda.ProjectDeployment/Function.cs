using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

using System.IO;

using Amazon.Lambda.Core;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;

using Amazon.S3;
using Amazon.S3.Util;
using Amazon.S3.Model;

using CARRDS.Library.Models;
using Newtonsoft.Json.Linq;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CARRDS.Lambda.ProjectDeployment
{
    public class Function
    {
        #region Function constructors and setup
        // Get my relevant environment variables
        string StackStatusSNS = Environment.GetEnvironmentVariable("StackStatusSNS");
        string OperationsBucket = Environment.GetEnvironmentVariable("OperationsBucket");



        // My S3 Client
        static IAmazonS3 S3Client;

        // My CloudFormation Client
        static AmazonCloudFormationClient CloudformationClient;


        private static ILambdaSerializer serializer;

        static Function()
        {
            S3Client = new AmazonS3Client();

            CloudformationClient = new AmazonCloudFormationClient();

            serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();
        }

        #endregion

        public async Task<string> FunctionHandler(S3EventNotification evnt, ILambdaContext context)
        {

            #region Setup
            // S3 Entity is more convenient for handling the event.
            S3EventNotification.S3Entity s3Event = new S3EventNotification.S3Entity();
            
            // A flag to determine if I want to create a new stack or update an existing one
            bool newstack = new bool();

            // The name of the stack which will be generated by the deployment process. 
            string TargetStackName;

            // The fixed file name values the deployment process will require. If all three files are not present no deployment will take place. 
            string DeploymentFileName = "master.template";
            string CARRDSFileName = "CARRDS.json";

            // Serializer settings are apparantly important. 
            var SerilaizerSettings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects
            };


            // Write details of the s3 event to my logger
            s3Event = evnt.Records?[0].S3;
            context.Logger.Log("CARRDS File Upload Event Recieved. Processing deployment.");
            context.Logger.Log("Bucket: " + s3Event.Bucket.Name);
            context.Logger.Log("Key: " + s3Event.Object.Key);
            string SourcePath = s3Event.Object.Key.Remove(s3Event.Object.Key.LastIndexOf("/"));

            #endregion

            #region Get the CARRDS File
            // Retrieve the CARRDS file
            CARRDSConfiguration CARRDSConfiguration;
            try
            {
                context.Logger.Log("Attempting to retrieve CARRDS object.");
                GetObjectResponse CARRDSResponse = await S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                CARRDSConfiguration = JsonConvert.DeserializeObject<CARRDSConfiguration>(new StreamReader(CARRDSResponse.ResponseStream).ReadToEnd(), SerilaizerSettings);
                context.Logger.Log("CARRDS File Retrieved");
                context.Logger.LogLine(JsonConvert.SerializeObject(CARRDSConfiguration, SerilaizerSettings));
            }
            catch (Exception e)
            {
                context.Logger.Log(e.Message);
                context.Logger.Log(e.StackTrace);
                context.Logger.Log("Could not retrieve the CARRDS file. Aborting Deployment");
                throw e;
            }
            #endregion

            #region Update CloudFormation Template Lambda Code Pointers
            // Retrieve any templates in the directory and update their lambda references inside this bucket to their current version

            // List all of the versions of the different files in the directory
            ListVersionsResponse SourceVersions = new ListVersionsResponse();
            try
            {
                context.Logger.Log("Obtaining Current S3 file versions.");
                SourceVersions = await S3Client.ListVersionsAsync(new ListVersionsRequest()
                {
                    BucketName = s3Event.Bucket.Name,
                    Prefix = SourcePath
                });
            }
            catch (Exception e)
            {
                context.Logger.Log("An error occured obtaining the current s3 file versions.");
                context.Logger.Log(e.Message);
                context.Logger.Log(e.StackTrace);
            }

            // Get the list of objects in this directory
            ListObjectsV2Response RepoObjects = new ListObjectsV2Response();
            try
            {
                RepoObjects = await S3Client.ListObjectsV2Async(new ListObjectsV2Request()
                {
                    BucketName = s3Event.Bucket.Name,
                    Prefix = SourcePath
                });
            }
            catch (Exception e)
            {
                context.Logger.Log("An error occured getting the repository object list");
                context.Logger.Log(e.Message);
                context.Logger.Log(e.StackTrace);
            }


            foreach (S3Object obj in RepoObjects.S3Objects)
            {
                // If the object is a cloudformation template I want to read it in and correct any lambdas it may have
                if (obj.Key.EndsWith(".template"))
                {
                    // List off the template name and its hash id
                    context.Logger.Log("Template: " + obj.Key + " " + obj.ETag);

                    // Get the template
                    GetObjectResponse GetTemplate = new GetObjectResponse();
                    try
                    {
                        GetTemplate = await S3Client.GetObjectAsync(new GetObjectRequest()
                        {
                            BucketName = s3Event.Bucket.Name,
                            Key = obj.Key
                        });
                    }
                    catch (Exception e)
                    {
                        context.Logger.Log("An error occured getting template: " + obj.Key);
                        context.Logger.Log(e.Message);
                        context.Logger.Log(e.StackTrace);
                    }
                    
                    // Desearialize the template to a json object I can work with
                    JObject template = JsonConvert.DeserializeObject<JObject>(new StreamReader(GetTemplate.ResponseStream).ReadToEnd());
                    context.Logger.Log(template.ToString());
                    try
                    {
                        // I really only care about the Resources and this will make it easier to deal with
                        JObject resources = (JObject)template["Resources"];

                        // Go through all of the values in the jobject looking for lambdas
                        foreach (JToken resource in resources.Children())
                        {
                            // I only want to modify objects that are Lambdas
                            if (resource.First["Type"].ToString().Equals("AWS::Lambda::Function"))
                            {
                                // Pass out which object I am dealing with
                                context.Logger.Log("Updating Lambda to latest s3 object version code: " + resource.First.Path.ToString());

                                // If it does not appear to be referencing this bucket skip it
                                if (!resource.First["Properties"]["Code"]["S3Bucket"].ToString().Contains("StackSourceBucket"))
                                {
                                    context.Logger.Log("This lambda appears to be referencing code in another bucket. Use the \"StackSourceBucket\" reference parameter to target this bucket.");
                                    continue;
                                }

                                // Construct the s3 key assuming the StackSourcePath parameter has been used: 
                                int startind = resource.First["Properties"]["Code"]["S3Key"].ToString().IndexOf("/");
                                int endind = resource.First["Properties"]["Code"]["S3Key"].ToString().LastIndexOf("\"");
                                string constructedkey = SourcePath + resource.First["Properties"]["Code"]["S3Key"].ToString().Substring(startind, endind - startind);

                                // Find the latest version id for that key
                                string latestversion = SourceVersions.Versions.Find(x => (x.Key == constructedkey) && (x.IsLatest == true)).VersionId;

                                // Add the s3objectversion key 
                                resource.First["Properties"]["Code"].Children().Last().AddAfterSelf(new JProperty("S3ObjectVersion", latestversion));
                            }
                        }
                        //Write the template back
                        try
                        {
                            PutObjectResponse templatewriteresult = await S3Client.PutObjectAsync(new PutObjectRequest()
                            {
                                BucketName = s3Event.Bucket.Name,
                                Key = obj.Key,
                                ContentBody = template.ToString()
                            });
                        }
                        catch (Exception e)
                        {
                            context.Logger.Log("An error occured rewriting the template: " + obj.Key);
                            context.Logger.Log(e.Message);
                            context.Logger.Log(e.StackTrace);
                        };

                    }
                    catch (Exception e)
                    {
                        context.Logger.Log("I was unable to update one of the cloudformation templates");
                        context.Logger.Log(e.Message);
                    }
                }
            }
            #endregion

            #region Get ready for my call

            // Gets a presigned url for the cloudformation client so it can access the master.template document.
            string PresignedS3Key = "";
            try
            {
                context.Logger.Log("Obtaining the Presigned URL.");
                PresignedS3Key = S3Client.GetPreSignedURL(new GetPreSignedUrlRequest()
                {
                    BucketName = s3Event.Bucket.Name,
                    Key = s3Event.Object.Key.Replace(CARRDSFileName, DeploymentFileName),
                    Expires = DateTime.Now.AddMinutes(5),
                });
            }
            catch (Exception e)
            {
                context.Logger.Log("An error occured obtaining the presigned url. ");
                context.Logger.Log(e.Message);
                context.Logger.Log(e.StackTrace);
            }

            // The name of the stack will be based on the folder structure containing the master.template document. 
            // As an example, a template deployed to the S3 key Knect/RCC/master.template would generate the stack Knect-RCC
            TargetStackName = SourcePath.Replace("/", "-");
            context.Logger.Log("Cloudformation Stack Name: " + TargetStackName);

            // If a stack with the target name already exists I want to update it. Otherwise I want to create a new stack. 
            try
            {
                DescribeStacksResponse CurrentStacksResponse = await CloudformationClient.DescribeStacksAsync(new DescribeStacksRequest() {StackName = TargetStackName});
                context.Logger.Log("A stack for the target name already exists. The existing stack will be updated.");

                newstack = false;
            }
            catch
            {
                context.Logger.Log("No stack with the target name exists. A new stack will be created.");

                newstack = true;
            }

            #endregion

            #region Create or Update Stack
            string stackidresult = "";
            // If there is an existing stack I will update it. Otherwise I will create a new stack
            if (newstack == true)
            {
                // Create a new stack
                CreateStackRequest CreateStack = new CreateStackRequest()
                {
                    StackName = TargetStackName,
                    TemplateURL = PresignedS3Key,
                    Parameters = CARRDSConfiguration.Parameters,
                    NotificationARNs = new List<string>()
                };
                CreateStack.NotificationARNs.Add(StackStatusSNS);
                CreateStack.Capabilities.Add("CAPABILITY_NAMED_IAM");

                try
                {
                    CreateStackResponse StackCreate = await CloudformationClient.CreateStackAsync(CreateStack);
                    stackidresult = StackCreate.StackId;
                }
                catch (Exception e)
                {
                    context.Logger.Log("An error occured trying to create the stack. ");
                    context.Logger.Log(e.Message);
                    context.Logger.Log(e.StackTrace);
                }
            }
            else
            {
                UpdateStackRequest updatereq = new UpdateStackRequest()
                {
                    StackName = TargetStackName,
                    TemplateURL = PresignedS3Key,
                    Parameters = CARRDSConfiguration.Parameters,
                    NotificationARNs = new List<string>()
                };
                updatereq.NotificationARNs.Add(StackStatusSNS);
                updatereq.Capabilities.Add("CAPABILITY_NAMED_IAM");

                try
                {
                    UpdateStackResponse StackUpdate = await CloudformationClient.UpdateStackAsync(updatereq);
                    stackidresult = StackUpdate.StackId;
                }
                catch (Exception e)
                {
                    context.Logger.Log("An error occured trying to update the stack. ");
                    context.Logger.Log(e.Message);
                    context.Logger.Log(e.StackTrace);
                }
            }
            context.Logger.LogLine("Created Stack: " + stackidresult);
            #endregion
            
            #region Write stack status responses
            context.Logger.LogLine("Writing CARRDS Actions");
            foreach (var response in CARRDSConfiguration.StackStatusResponses)
            {
                context.Logger.LogLine("Writing CARRDS Action: " + response.GetName());
                try
                {
                    PutObjectResponse putAction = await S3Client.PutObjectAsync(new PutObjectRequest()
                    {
                        BucketName = OperationsBucket,
                        Key = stackidresult + "/Tasks/" + response.GetName(),
                        ContentBody = JsonConvert.SerializeObject(response, SerilaizerSettings)
                    });
                }
                catch (Exception e)
                {
                    context.Logger.Log("An error occured trying to write the CARRDS action. " + response.GetName());
                    context.Logger.Log(e.Message);
                    context.Logger.Log(e.StackTrace);
                }
                context.Logger.LogLine(JsonConvert.SerializeObject(response, SerilaizerSettings));
            }

            #endregion

            return "StackID: " + stackidresult;

        }
    }
}

