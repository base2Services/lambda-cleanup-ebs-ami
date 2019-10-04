## Lambda functions to report and cleanup EBS snapshots and AMIs

1 - build lambda locally
2 - deploy using built 'Base2.Lambdas.zip' package (manaully for now)
3 - run the function to generate report. use payload from test/run
section of this README to see parameters
4 - run the function to cleanup orphaned AMI EBS snapshosts 

# Requirements

## Build

You will need docker engine and `zip` utility to build project. Also, build script uses `bash` shell
If you have `dotnet` cli locally installed you may use `scripts/build_native.sh`, but docker build is 
recommended way for automating builds. 

```
$ scripts/build_docker.sh
  Restoring packages for /project/Base2.Lambdas.csproj...
  Lock file has not changed. Skipping lock file write. Path: /project/obj/project.assets.json
  Restore completed in 2.06 sec for /project/Base2.Lambdas.csproj.

  NuGet Config files used:
      /root/.nuget/NuGet/NuGet.Config

  Feeds used:
      https://api.nuget.org/v3/index.json
Microsoft (R) Build Engine version 15.1.1012.6693
Copyright (C) Microsoft Corporation. All rights reserved.

  Base2.Lambdas -> /project/bin/Debug/netcoreapp2.1/Base2.Lambdas.dll
  adding: AWSSDK.AutoScaling.dll (deflated 70%)
  adding: AWSSDK.Core.dll (deflated 66%)
  adding: AWSSDK.EC2.dll (deflated 70%)
  adding: AWSSDK.S3.dll (deflated 63%)
  adding: Amazon.Lambda.Core.dll (deflated 57%)
  adding: Amazon.Lambda.Serialization.Json.dll (deflated 56%)
  adding: Base2.Lambdas.deps.json (deflated 74%)
  adding: Base2.Lambdas.dll (deflated 55%)
  adding: Base2.Lambdas.pdb (deflated 40%)
  adding: Newtonsoft.Json.dll (deflated 60%)
  adding: System.Collections.NonGeneric.dll (deflated 60%)
  adding: System.Runtime.Serialization.Primitives.dll (deflated 48%)

```

## Automated deployment

You will need serverless framework, version `> 1.15` to deploy lambda functions automatically. Use `sls deploy`, 
in comnbination with properly set environment variables:

```
$ export REGION=ap-southeast-2
$ export SOURCE_BUCKET=automation.cleanup.base2.services
$ sls deploy
Serverless: Packaging service...
Serverless: Uploading CloudFormation file to S3...
Serverless: Uploading artifacts...
Serverless: Validating template...
Serverless: Creating Stack...
Serverless: Checking Stack create progress...
.........................................
Serverless: Stack create finished...
Service Information
service: manualawscleanup
stage: dev
region: ap-southeast-2
api keys:
  None
endpoints:
  None
functions:
  AMIReport: manualawscleanup-dev-AMIReport
  AMICleanup: manualawscleanup-dev-AMICleanup
  EBSReport: manualawscleanup-dev-EBSReport
  EBSCleanup: manualawscleanup-dev-EBSCleanup
```

## Lambda configuration

Note that all of configurtion below is now implemented through serverless framework, and thus 

### Code Package

`scripts/build_docker.sh` script will create lambda package in root directory called `Base2.Lambdas.zip`.
This package is referenced in serverless project as code package.

### Handler

Use following entry points (Lambda function handlers)

- Report generation for EBS - `Base2.Lambdas::Base2.Lambdas.Handlers.EBSReportAndCleanup::UploadEBSReport`
- Report generation for AMI - `Base2.Lambdas::Base2.Lambdas.Handlers.AMIReportAndCleanup::UploadAMIReport`
- Cleanup from CSV info for EBS - `Base2.Lambdas::Base2.Lambdas.Handlers.EBSReportAndCleanup::CleanupFromReport`
- Celanup from CSV info for AMIs - `Base2.Lambdas::Base2.Lambdas.Handlers.AMIReportAndCleanup::DeregisterReportedAMIs`

### IAM Role

Iam role configured for lambda should have following policies

- read only access to EC2 service
- write acces to S3 bucket passed in as argument
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "Stmt1497509441000",
            "Effect": "Allow",
            "Action": [
                "s3:*"
            ],
            "Resource": [
                "arn:aws:s3:::aws.amis-cleanup.reports.example.com/*"
            ]
        }
    ]
}
```
- DeleteSnapshot permissions

```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "Stmt1497854974000",
            "Effect": "Allow",
            "Action": [
                "ec2:DeleteSnapshot"
            ],
            "Resource": [
                "*"
            ]
        }
    ]
}
```
- Invoke lambda permission, to invoke itself recursively for long running
deletions
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "Stmt1497921290000",
            "Effect": "Allow",
            "Action": [
                "lambda:InvokeAsync",
                "lambda:InvokeFunction"
            ],
            "Resource": [
                "*"
            ]
        }
    ]
}
```


### Timeout

All of operations can be time consuming, so it's recommended to set all runtimes to 5 minutes

### Runtime

Use C# as runtime

### Memory

This functions do not require more than 128MB of memory, even when working with ~10k EBS snapshots (highest tested value)

### Other

There is no VPC configuration required

## Test / Run

Both report generation and cleanup tasks are accepting location of csv file to write/read
in event parameters. For report generation there is optional parameter `OnlyAMIOrphans` which default to 
`true`. This parameter determines whether only AMI orphans get reported or ALL EBS snapshots
(danger zone, as you don't want to delete all snapshots, but you may want to delete some that are not
orphans, thus need for this functionality)

e.g.
```
{
    "BucketName":"aws.amis-cleanup.reports.base2.services",
    "Key":"ebs_report_prod.csv",
    "OnlyAMIOrphans": true
}
```