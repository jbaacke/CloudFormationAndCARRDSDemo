using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

using Amazon.APIGateway;
using Amazon.Lambda.APIGatewayEvents;

using Amazon.S3;
using Amazon.S3.Util;
using Amazon.S3.Model;
using Amazon;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace CARRDS.Lambda.GetAuthorization
{
    public class Function
    {

        static IAmazonS3 S3Client;

        string InternalSourceBucket;



        public Function()
        {

            S3Client = new AmazonS3Client();
            InternalSourceBucket = Environment.GetEnvironmentVariable("InternalSourceBucket");
        }

        public Function(string apikey, string apisecret, RegionEndpoint region)
        {

            S3Client = new AmazonS3Client(apikey, apisecret, region);
            
            InternalSourceBucket = "bucketname";

        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
        {
            // Log my activities

            LambdaLogger.Log("I am in the function");
            LambdaLogger.Log("Path: " + input.Path);
            LambdaLogger.Log("Body: " + input.Body);


            GetPreSignedUrlRequest PresignedKeyRequest = new GetPreSignedUrlRequest()
            {
                BucketName = InternalSourceBucket,
                Expires = DateTime.Now.AddMinutes(5)
            };
            string PresignedS3Key = S3Client.GetPreSignedURL(PresignedKeyRequest);


            APIGatewayProxyResponse Response = new APIGatewayProxyResponse()
            {
                Body = PresignedS3Key,
                StatusCode = 200
            };

            return Response;
        }
    }
}
