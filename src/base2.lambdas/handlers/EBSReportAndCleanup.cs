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
using System.Text.RegularExpressions;
using Base2.Lambdas.Models;
using System.Threading.Tasks;
using Base2.AWS;

namespace Base2.Lambdas.Handlers
{
    public class EBSReportAndCleanup
    {

        private ILambdaContext context;

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public Stream CleanupFromReport(S3Input input, ILambdaContext context)
        {
            AmazonEC2Client ec2Client = new AmazonEC2Client();
            String[] lines = new AWSCommon().GetS3ContextAsText(input.BucketName,input.Key).Split("\n".ToCharArray());
            List<Task<DeleteSnapshotResponse>> deleteTasks = new List<Task<DeleteSnapshotResponse>>();
            foreach(String line in lines)
            {
                string[] cells = line.Split(',');
                if(cells.Length > 1){
                    string snapshotId = cells[1];
                    if(new Regex("snap-(.*)").Match(snapshotId).Success){
                        context.Logger.LogLine($"Deleting snapshot {snapshotId}..");
                        deleteTasks.Add(ec2Client.DeleteSnapshotAsync(new DeleteSnapshotRequest(){SnapshotId=snapshotId}));
                    } else {
                        context.Logger.LogLine($"Snapshot id {snapshotId} not in snap-xx format");
                    }
                }
            }
            deleteTasks.ForEach(t => {
                t.Wait();
            });
            return new MemoryStream(Encoding.UTF8.GetBytes("OK"));
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public Stream UploadEBSReport(S3Input input, ILambdaContext context)
        {
            this.context = context;
            uploadContent(getImagesAsCsv(context), input.BucketName, input.Key);
            return new MemoryStream(Encoding.UTF8.GetBytes("OK"));
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
                context.Logger.LogLine($"Uploaded s3://{bucket}/{key} ETAG {uploadPromise.Result.ETag}");
            }
        }

        private String getImagesAsCsv(ILambdaContext context)
        {
            String accountId = context.InvokedFunctionArn.Split(':')[4];
            var ec2Client = new AmazonEC2Client();
            var getSnapshotsRequest = new DescribeSnapshotsRequest();
            getSnapshotsRequest.MaxResults = 1000;
            getSnapshotsRequest.Filters.Add(new Amazon.EC2.Model.Filter("status", new List<string> { "completed" }));
			getSnapshotsRequest.Filters.Add(new Amazon.EC2.Model.Filter("owner-id", new List<string> { accountId }));
            List<Snapshot> snapshots = new List<Snapshot>();
            do
            {
                var taskResponse = ec2Client.DescribeSnapshotsAsync(getSnapshotsRequest);
                taskResponse.Wait();
                snapshots.AddRange(taskResponse.Result.Snapshots);
                context.Logger.LogLine($"Added {taskResponse.Result.Snapshots.Count} snapshots to results list");
                getSnapshotsRequest.NextToken = taskResponse.Result.NextToken;
            } while (getSnapshotsRequest.NextToken != null);
         
            var awsCommon = new AWSCommon();
            var allAMIs = awsCommon.GetAllOwnedPrivateAMIs(context);
            var sb = new StringBuilder();
            sb.Append("DateCreated,SnapID,Name,Description,AMIRelated,AMIExists,AMI-ID,Tags\n");
            snapshots.Sort((s1,s2)=>-1*s1.StartTime.CompareTo(s2.StartTime));
            snapshots.ForEach(snapshot => {
              
                var nameTag = snapshot.Tags.Find(t=>t.Key.Equals("Name"));
                var name = nameTag != null ? nameTag.Value : "";
                var notNameTags = String.Join(" ", snapshot.Tags.FindAll(t=>t.Key!="Name").Select(t=>$"{t.Key}={t.Value}"));
                bool isAmiRelated = false;
                bool amiExists = false;
                String amiId = "";

                if(snapshot.Description != null){
                    var amiRegex = new Regex("Created by (.*) for ami-(.*) from (.*)").Match(snapshot.Description);
                    isAmiRelated = amiRegex.Success;
                    amiId = isAmiRelated ? "ami-" + amiRegex.Groups[2] : "";
                    amiExists = allAMIs.Find(i => i.ImageId.Equals(amiId)) != null;
                }
                if(isAmiRelated && !amiExists){
                    sb.Append($"{snapshot.StartTime},{snapshot.SnapshotId},{name},{snapshot.Description},{isAmiRelated},{amiExists},{amiId},{notNameTags}\n");
                }else if(isAmiRelated && amiExists){
                    context.Logger.LogLine($"Skipping snap {snapshot.SnapshotId} as AMI {amiId} exists");
                }else if(!isAmiRelated){
                    context.Logger.LogLine($"Skipping snap {snapshot.SnapshotId} as non-ami related snapshot");
                }
            });
               
            return sb.ToString();
        }
    }
}
