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
using Amazon.S3.Model;

namespace Base2.AWS
{
    public class AWSCommon
    {

        public String GetS3ContextAsText(string bucket, string key){
            var s3client = new Amazon.S3.AmazonS3Client();
            var response = s3client.GetObjectAsync(new GetObjectRequest {BucketName=bucket,Key=key});
            response.Wait();
            return new StreamReader(response.Result.ResponseStream).ReadToEnd();
        }
        public List<Image> GetAllOwnedPrivateAMIs(ILambdaContext context)
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

            var imagesResponse = ec2Client.DescribeImagesAsync(getImagesRequest);
            imagesResponse.Wait();
            
            return imagesResponse.Result.Images;
        }
    }
}
