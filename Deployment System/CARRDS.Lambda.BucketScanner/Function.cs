using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

using Amazon.S3;
using Amazon.S3.Util;
using Amazon.S3.Model;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.IO;
using Amazon;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace CARRDS.Lambda.BucketScanner
{
    public class Function
    {

        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        public Function(string apikey, string apisecret, RegionEndpoint region)
        {

            S3Client = new AmazonS3Client(apikey, apisecret, region);

            InternalSourceBucket = "bucketname";

            TargetCARRDSAPIEndpoint = "https://3br5xc50x8.execute-api.us-west-2.amazonaws.com/LATEST/Authorization/getauthorization";

        }

        string InternalSourceBucket = Environment.GetEnvironmentVariable("InternalSourceBucket");

        string TargetCARRDSAPIEndpoint = Environment.GetEnvironmentVariable("TargetCARRDSAPIEndpoint");

        string PreAuthedString;


        IAmazonS3 S3Client;

        HttpClient client = new HttpClient();

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(ILambdaContext context)
        {

            // Log my activities
            context.Logger.Log("I am in the function");
            //context.Logger.LogLine("Input: " + input);


            // This doesn't work to retrieve the pre authed string, but I can get it in PostMan. So I will ask Mike about it when I get in and move on for now
            // I am working with this documentation for the preauthed string: http://docs.aws.amazon.com/sdkfornet1/latest/apidocs/html/T_Amazon_S3_Model_GetPreSignedUrlRequest.htm
            // This also looks useful: http://docs.aws.amazon.com/AmazonS3/latest/dev/UploadObjectPreSignedURLDotNetSDK.html
            
            
            #region Getting Preauthed String
            client.BaseAddress = new Uri(TargetCARRDSAPIEndpoint);
            try
            {
                var respnose = client.PostAsync(TargetCARRDSAPIEndpoint, null);

                respnose.Wait();

                var content = respnose.Result.Content.ReadAsStringAsync();

                content.Wait();

                context.Logger.LogLine(content.ToString());

                PreAuthedString = content.Result;
            }
            catch (Exception err)
            {
                context.Logger.LogLine(err.Message);
                throw err;
            }
            
            #endregion




            #region Scan the bucket
            context.Logger.LogLine(PreAuthedString);

            Task<string> allobjects = GetContentsAsync(PreAuthedString);
            allobjects.Wait();
            
            context.Logger.Log(allobjects.Result);




            #endregion
            IAmazonS3 PreauthedS3Client = new AmazonS3Client();

            #region Move something
            #endregion


            return "success!";



            //return input?.ToUpper();
        }


        public static async Task<string> GetContentsAsync(string path)
        {
            HttpWebRequest request = HttpWebRequest.Create(path) as HttpWebRequest;
            WebResponse response = await request.GetResponseAsync();
            

            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

    }
}
