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
using Amazon.S3;
using Amazon.Lambda;
using Newtonsoft.Json;

namespace Base2.Lambdas.Handlers
{
    public class AMIReportAndCleanup
    {

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public String DeregisterReportedAMIs(AMICleanupInput input, ILambdaContext context){
            var ec2Client = new AmazonEC2Client();
            var s3client = new AmazonS3Client();
            String[] lines = new AWSCommon().GetS3ContextAsText(input.BucketName,input.Key).Split("\n".ToCharArray());
            int index =0;
            foreach(String line in lines){
                if(input.StartIndex > index){
                    if(index == input.StartIndex -1){
                        context.Logger.LogLine($"Skipped processing up to index #{index}");
                    }
                    index++;
                    continue;
                }

                if(context.RemainingTime.Seconds < 10){
                    context.Logger.LogLine($"Less than 10 seconds for lambda execution, starting function recursively..");
                    var lambdaClient = new Amazon.Lambda.AmazonLambdaClient();
                    input.StartIndex = index;
                    lambdaClient.InvokeAsync(new Amazon.Lambda.Model.InvokeRequest()
                    {
                        InvocationType = Amazon.Lambda.InvocationType.Event,
                        FunctionName = context.FunctionName,
                        Payload = JsonConvert.SerializeObject(input)
                    }).Wait();
                    return "Started recursively with index=" + index;
                }

                index = index + 1;

                String[] cells = line.Split(',');
                if(cells.Length >= 3){
                    String amiId = cells[2];
                    if(amiId.StartsWith("ami-")){
                        try {
                            var describeResponse = ec2Client.DescribeImagesAsync(new DescribeImagesRequest(){
                                ImageIds = new List<String>(){amiId} 
                            });
                            describeResponse.Wait();
                            
                            context.Logger.LogLine($"De-registering AMI {amiId}");
                            ec2Client.DeregisterImageAsync(new DeregisterImageRequest(){
                                ImageId = amiId
                            }).Wait();

                            describeResponse.Result.Images[0].BlockDeviceMappings.ForEach(mapping=>{
                                if(mapping.Ebs != null && mapping.Ebs.SnapshotId != null){
                                    context.Logger.LogLine($"Deleting snapshot {mapping.Ebs.SnapshotId} for ami {amiId}");
                                    ec2Client.DeleteSnapshotAsync(new DeleteSnapshotRequest(){
                                        SnapshotId = mapping.Ebs.SnapshotId
                                    }).Wait();
                                }
                            });
                        } catch(Exception ex){
                            context.Logger.LogLine($"Failed to delete ami {amiId} with following error:");
                            context.Logger.LogLine(ex.ToString());
                        }
                    } else {
                        context.Logger.LogLine($"Skppingg non-ami id : {amiId}");
                    }
                }
            }


            return "OK";
        }

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
