using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Lambda.Core;
using Base2.Lambdas.Models;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Base2.AWS;

namespace Base2.Lambdas.Handlers
{
    public class AMIReportAndCleanup
    {

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public String UploadAMIReport(S3Input input, ILambdaContext context)
        {
            uploadContent(getImagesAsCsv(context), input.BucketName, input.Key);
            return "OK";
        }

        private void uploadContent(String content, String bucket, String key)
        {

            using (var s3client = new Amazon.S3.AmazonS3Client())
            {
                var uploadPromise = s3client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    ContentBody = content
                });

                uploadPromise.Wait();

                Console.WriteLine($"Uploaded s3://{bucket}/{key} ETAG {uploadPromise.Result.ETag}");
            }
        }
        private String getImagesAsCsv(ILambdaContext context)
        {
            String accountId = context.InvokedFunctionArn.Split(':')[4];
            var ec2Client = new AmazonEC2Client();
            var asClient = new AmazonAutoScalingClient();
            var getImagesRequest = new DescribeImagesRequest();
            var getLCRequest = new DescribeLaunchConfigurationsRequest();
            getLCRequest.MaxRecords = 100;


            getImagesRequest.Filters.Add(new Amazon.EC2.Model.Filter
            {
                Name = "is-public",
                Values = new List<string>() { "false" }
            });
			getImagesRequest.Filters.Add(new Amazon.EC2.Model.Filter
			{
				Name = "owner-id",
                Values = new List<string>() { accountId }
			});

            var lcResponse = asClient.DescribeLaunchConfigurationsAsync(getLCRequest);
            var imagesResponse = ec2Client.DescribeImagesAsync(getImagesRequest);
            imagesResponse.Wait();
            lcResponse.Wait();
            imagesResponse.Result.Images.Sort((image1,image2) => -1 * image1.CreationDate.CompareTo(image2.CreationDate));
            List<LaunchConfiguration> launchConfigIds = lcResponse.Result.LaunchConfigurations;

            var sb = new StringBuilder();
            sb.Append("DateCreated,AMI-Name,AMI-ID,OwnerID,LaunchConfigExists,LaunchConfigName,Description\n");
            imagesResponse.Result.Images.ForEach(i => {

                List<String> lcs = launchConfigIds.FindAll(lc => lc.ImageId.Equals(i.ImageId)).Select(lc=>lc.LaunchConfigurationName).ToList();
                bool lcExists = lcs.Count > 0;
                String sLcs = lcExists ? String.Join(",", lcs) : String.Empty;    
                sb.Append($"{i.CreationDate},{i.Name},{i.ImageId},{i.OwnerId},{lcExists},{sLcs},{i.Description}\n");
            });
            return sb.ToString();
        }
    }
}
