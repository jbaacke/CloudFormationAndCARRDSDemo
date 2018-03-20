Param(
    [String]$SourceDirectory,
    [String]$accesskey,
    [String]$secretkey,
    [String]$region,
    [String]$s3bucket,
    [String]$s3path
)

#region Class Definitions
# The AWSInfo Class is a central repository for variables I will need to access 
# throuout the program
Class AWSInfo
{
    [String]$AccessKey
    [String]$SecretKey
    [String]$Region
    [String]$Path
    [String]$S3Bucket
    [String]$SourceDirectory
}
#endregion


#region Global Variables

$global:AWSParameters

#endregion


#region Functions

# Function to upload a file
Function UploadToS3($PathToLocalFile, $PathExtension)
{

    Write-Host ("Uploading File: " + $PathToLocalFile)
	
	$S3PathFull = "s3://" + $global:AWSParameters.S3Bucket + "/" + $global:AWSParameters.Path + $PathExtension

    Write-Host ("To: " + $S3PathFull)

    aws s3 cp $PathToLocalFile $S3PathFull --region $global:AWSParameters.Region --profile $global:AWSParameters.AccessKey
}

# Function to preform all Lambda uploads
Function BuildAndUploadLambda($PathToCSProj)
{
    # Navigate to the folder with my CSProj file
    cd $PathToCSProj

    # Package the Lambda
    Write-Host ("Restoring Lambda: " + $PathToCSProj)
    dotnet restore
    echo ("Building Lambda" + $PathToCSProj)
    dotnet lambda package

    Write-Host "uploading Lambda"

    # Get and upload the zip file
    $zipfiles = Get-ChildItem ($PathToCSProj + "\bin\Release\netcoreapp1.0\")*.zip
    foreach ($file in $zipfiles)
    {
        $filepath = $file.FullName
        $PathExtension = "/Resources/" + $file.Name
        UploadToS3 $filepath $PathExtension
    }
    Write-Host ("finished uploading Lambda" + $PathToCSProj)

    # Go back to my original directory
    cd $global:AWSParameters.SourceDirectory

}

# Function to preform all Lambda uploads
Function UploadCloudFormationTemplate($PathToTemplate)
{

    # Get the name of the template being uploaded
    $TemplateName = $FilePathParts[-1]
    Write-Host ("Uploading CloudFormation Template: " + $TemplateName)
        
    UploadToS3 $PathToTemplate ("/Resources/" + $TemplateName)


}

# Recursively search for CloudFormation Templates
Function FindAndUploadCloudFormationTemplates($DirectoryToSearch)
{
    # Go to the directory I want to search
    cd $DirectoryToSearch

    foreach ($file in dir)
    {
        #Recursively search through each directory
        if (!$file.Directory)
        {
            FindAndUploadCloudFormationTemplates $file.FullName
            continue
        }

        if ($file.Name.EndsWith(".template"))
        {
            # Do nothing if it is the master template
            if ($file.Name.EndsWith("master.template"))
            {
                UploadToS3 $file.FullName  ("/" + $file.Name)
            }

            # Upload the template
            UploadToS3 $file.FullName ("/Resources/" + $file.Name)
        }
    }
    cd $global:AWSParameters.SourceDirectory
}

# Search for Lambda functions in all of the solution files in the working directory
Function FindLambdas($DirectoryToSearch)
{
    # Go to the directory I want to search
    cd $DirectoryToSearch

    # Create an empty array for the projfiles
    $projfiles = [System.Collections.ArrayList]@()

    # Create an empty array for the lambda projects
    $lambdas = [System.Collections.ArrayList]@()

    # Go through all of the files in the directory that end with .sln and read them for their proj files
    foreach ($file in dir)
    {
        if ($file.Name.EndsWith(".sln"))
        {
            foreach ($line in [System.IO.File]::ReadLines($file.name)) 
            {
                if ($line.StartsWith("Project("))
                {
                    $commaarray = $line.Split(",")
                    $projfiles.Add($commaarray[1].Substring(2,$commaarray[1].Length - 3 ))
                }
            }
        }

    }

    # For each of the proj files find if they are a lambda
    foreach ($proj in $projfiles)
    {
        foreach ($line in [System.IO.File]::ReadLines($proj)) 
        {
            # This magic string indicates something is a lambda function
            if ($line.Contains("Amazon.Lambda.Tools"))
            {
                $lambdas.Add($proj.Substring(0,$proj.LastIndexOf("\")))
             }
         }
    }

    # For each of the lambdas upload it using my upload function
    foreach ($lambda in $lambdas)
    { 
        $lambdapath = $SourceDirectory + "\" +  $lambda
        BuildAndUploadLambda $lambdapath ("/Resources/" + $lambda)
    }

    
    cd $global:AWSParameters.SourceDirectory
}

Function SetParameterValue($CARRDSData, [string]$ParameterKey, [string]$ParameterValue)
{
    foreach($parameter in $CARRDSData.Parameters)
    {
        if ($parameter.ParameterKey.Equals($ParameterKey))
        {
            $parameter.ParameterValue = $ParameterValue
        }
    }
    return $CARRDSData
}

# Search for Lambda functions in all of the solution files in the working directory
Function FindCARRDS($DirectoryToSearch)
{
    # Go to the directory I want to search
    cd $DirectoryToSearch

    # Go through all of the files in the directory that end with .sln and read them for their proj files
    foreach ($file in dir)
    {
        if ($file.Name.EndsWith("CARRDS.json"))
        {
            # I want to update the build information components of the CARRDS file
            $CARRDSData = Get-Content $file.FullName -raw | ConvertFrom-Json

            # Put in the information about the build
            $CARRDSData.BuildInformation.SourceBranchName = $Env:BUILD_SOURCEBRANCHNAME
            $CARRDSData.BuildInformation.CommitID = $Env:BUILD_SOURCEVERSION
            $CARRDSData.BuildInformation.BuildNumber = $Env:BUILD_BUILDNUMBER
            $CARRDSData.BuildInformation.QueuedBy = $Env:BUILD_QUEUEDBY

            # Set my typical parameters
            $CARRDSData = SetParameterValue $CARRDSData "EnvironmentName" $Env:BUILD_SOURCEBRANCHNAME
            $CARRDSData = SetParameterValue $CARRDSData "StackSourceBucket" $global:AWSParameters.S3Bucket
            $CARRDSData = SetParameterValue $CARRDSData "StackSourcePath" $global:AWSParameters.Path

            # Write the CARRDS file back 
            $CARRDSData | ConvertTo-Json -Depth 10 | set-content $file.FullName -Encoding UTF8
            
            UploadToS3 $file.FullName ("/" + $file.Name)
        }
    }

    cd $global:AWSParameters.SourceDirectory
}

#endregion


#region Set my AWS parameters
$global:AWSParameters = [AWSInfo]::new()
$global:AWSParameters.Path = $s3path
$global:AWSParameters.Region = $region
$global:AWSParameters.S3Bucket = $s3bucket
$global:AWSParameters.SourceDirectory = $SourceDirectory
$global:AWSParameters.AccessKey = $accesskey
#endregionup

#region Set my Access keys
aws configure set AWS_ACCESS_KEY_ID $global:AWSParameters.AccessKey --profile $global:AWSParameters.AccessKey
aws configure set AWS_SECRET_ACCESS_KEY  $secretkey --profile $global:AWSParameters.AccessKey
echo "access keys set"
#endregion

# Find any lambdas that happen to by lying around.
FindLambdas $global:AWSParameters.SourceDirectory

# Search through the directory for any subortinate cloudformation templates and upload them
FindAndUploadCloudFormationTemplates $global:AWSParameters.SourceDirectory

# Get and upload my CARRDS file. 
FindCARRDS $global:AWSParameters.SourceDirectory

#region clear my access keys
aws configure set AWS_ACCESS_KEY_ID NoValue --profile $global:AWSParameters.AccessKey
aws configure set AWS_SECRET_ACCESS_KEY NoValue --profile $global:AWSParameters.AccessKey
echo "Access Keys removed"
#endregion
