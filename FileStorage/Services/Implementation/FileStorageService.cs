
using System.Net;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FileStorage.Services.DTO;
using FileStorage.Services.ValueObject;


namespace FileStorage.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly IConfiguration _config;
        private readonly string _dynamoTableName;
        private readonly string _s3BucketName;
        private readonly ITransferUtility _transferUtility;
        private readonly ILogger<FileStorageService> _logger;
        public FileStorageService(
            IAmazonS3 s3Client,
            IAmazonDynamoDB dynamoDBClient,
            IConfiguration config,
            ITransferUtility transferUtility,
            ILogger<FileStorageService> logger)
        {
            _s3Client = s3Client;
            _dynamoDbClient = dynamoDBClient;
            _config = config;
            _dynamoTableName = _config["DynamoDb:Tablename"] ?? "Files";
            _s3BucketName = _config["S3Bucket:BucketName"] ?? "storage";
            _transferUtility = transferUtility;
            _logger = logger;
        }
        public async Task<ResponseFileUploadDto> UploadFilesToS3(IFormFile file)
        {
            ResponseFileUploadDto responseFileUpload;
            var fileHash = "";
            try
            {
                //Generate a unique file name for key
                var fileKey = $"{Guid.NewGuid()}_{file.FileName}";

                using (var filestream = file.OpenReadStream())
                using (var sha256 = SHA256.Create())
                using (var cryptoStream = new CryptoStream(filestream, sha256, CryptoStreamMode.Read))
                {
                    // Creating TransferUtilityUploadRequest for streaming upload
                    var uploadReq = new TransferUtilityUploadRequest
                    {
                        InputStream = cryptoStream,
                        BucketName = _s3BucketName,
                        Key = fileKey,
                        ContentType = file.ContentType,
                        PartSize = 5 * 1024 * 1024,
                        AutoCloseStream = true
                    };
                    await _transferUtility.UploadAsync(uploadReq);

                    if (sha256.Hash != null)
                        fileHash = BitConverter.ToString(sha256.Hash).Replace("-", "").ToLowerInvariant();
                }

                // Return a custom object to the response
                var successMsg = "File upload completed successfully.";

                responseFileUpload = new ResponseFileUploadDto(Status.Success, successMsg, fileKey, fileHash);

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during file upload: {ex}");
                responseFileUpload = new ResponseFileUploadDto(Status.Success, "Error");
            }

            return responseFileUpload;
        }

        public async Task<ShaResponseDto> SaveHashToDynamoDb(string fileKey, string fileHash)
        {
            var tableName = _dynamoTableName;
            var shaResponseDto = new ShaResponseDto(Status.Error, "Error in file hash", "Error happened");

            var putRequest = new PutItemRequest
            {
                TableName = tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    {"Filename",new AttributeValue{ S = fileKey }},
                    { "UploadedAt",new AttributeValue { S = DateTime.UtcNow.ToString("o") } },
                    { "FileHash", new AttributeValue { S = fileHash} }
                }
            };
            try
            {
                await _dynamoDbClient.PutItemAsync(putRequest);
                var successMessage = "File Metadata saved successfully!!";
                shaResponseDto = new ShaResponseDto(Status.Success, fileHash, successMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating data to DynamoDb:" + fileKey + "//" + ex);
            }
            return shaResponseDto;
        }

        public async Task<ListFileResponseDto> ListAllFiles(string? hashCode)
        {
            var validFiles = new List<DynamoDBFile>();
            ListFileResponseDto fileDto = new(Status.Error, 0);

            try
            {
                var foundFileList = await (String.IsNullOrEmpty(hashCode) ?
                    GetAllFiles() : GetAllFilesBySha(hashCode));

                foreach (var foundFile in foundFileList)
                {
                    var isValid = await IsFileInS3Bucket(foundFile.FileName);
                    if (isValid)
                    {
                        validFiles.Add(foundFile);
                    }

                }
                // Return the list of valid files
                fileDto = new ListFileResponseDto(Status.Success, validFiles.Count, validFiles);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Internal server error: {ex.Message}");
            }

            return fileDto;
        }

        public async Task<bool> IsFileInS3Bucket(string fileName)
        {
            bool isFound = false;
            try
            {
                var s3Response = await _s3Client.GetObjectMetadataAsync(_s3BucketName, fileName);
                isFound = true;

            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError($"File '{fileName}' does not exist in S3..." + ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"File '{fileName}' does not exist in S3..." + ex);
            }

            return isFound;
        }

        public async Task<List<DynamoDBFile>> GetAllFilesBySha(string hashCode)
        {
            var foundFiles = new List<DynamoDBFile>();

            var queryRequest = new QueryRequest
            {
                TableName = _dynamoTableName,
                IndexName = "FileHashIndex",
                KeyConditionExpression = "FileHash =:hashCode",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    {":hashCode",new AttributeValue { S = hashCode }}
                }
            };
            var queryResponse = await _dynamoDbClient.QueryAsync(queryRequest);


            if (queryResponse.Items.Count != 0)
            {
                var items = queryResponse.Items;

                foreach (var item in items)
                {
                    var foundFile = new DynamoDBFile
                    {
                        FileName = item["Filename"].S,
                        FileHash = item["FileHash"].S,
                        UploadedAt = item["UploadedAt"].S
                    };
                    foundFiles.Add(foundFile);
                }
            }
            return foundFiles;
        }

        public async Task<List<DynamoDBFile>> GetAllFiles()
        {
            var foundFiles = new List<DynamoDBFile>();

            //Prepare the scan request
            var scanRequest = new ScanRequest
            {
                TableName = _dynamoTableName,
                ProjectionExpression = "Filename, FileHash, UploadedAt",
                Limit = 100
            };

            do
            {
                // Scan the DynamoDB table to get all file records
                var scanResponse = await _dynamoDbClient.ScanAsync(scanRequest);
                foreach (var item in scanResponse.Items)
                {
                    // Extract file name from the DynamoDB record
                    var fileName = item["Filename"].S;
                    var fileHash = item["FileHash"].S;
                    var uploadedAt = item["UploadedAt"].S;

                    var foundFile = new DynamoDBFile
                    {
                        FileName = fileName,
                        FileHash = fileHash,
                        UploadedAt = uploadedAt
                    };
                    foundFiles.Add(foundFile);
                }
                // Variable to keep track of the LastEvaluatedKey for pagination
                scanRequest.ExclusiveStartKey = scanResponse.LastEvaluatedKey;
            }
            while (scanRequest.ExclusiveStartKey != null && scanRequest.ExclusiveStartKey.Count > 0);

            return foundFiles;
        }

        public async Task<DownloadFileFromS3Dto> DownloadFromS3(string fileKey)
        {
            DownloadFileFromS3Dto fileDownloadDto;

            var isFileInS3Bucket = await IsFileInS3Bucket(fileKey);

            if (!isFileInS3Bucket)
            {
                return new DownloadFileFromS3Dto(Status.Error, StatusCodes.Status404NotFound, "File does not exist.", "Not Found");
            }

            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _s3BucketName,
                    Key = fileKey,
                    Expires = DateTime.UtcNow.AddMinutes(120),
                    Verb = HttpVerb.GET,
                    Protocol = Protocol.HTTP,
                    ResponseHeaderOverrides = new ResponseHeaderOverrides
                    {
                        ContentDisposition = "attachment; filename=" + fileKey // Ensure this header is set for downloading
                    }
                };
                string url = await _s3Client.GetPreSignedURLAsync(request);

                fileDownloadDto = new DownloadFileFromS3Dto(Status.Success, StatusCodes.Status200OK, "File Downloaded Successfully", url);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error happened: " + ex);
                fileDownloadDto = new DownloadFileFromS3Dto(Status.Error, StatusCodes.Status500InternalServerError, ex.Message, String.Empty);
            }
            return fileDownloadDto;
        }

        public async Task<GetObjectResponse> GetS3ClientResponseObject(string fileKey)
        {
            var request = new GetObjectRequest
            {
                BucketName = _s3BucketName,
                Key = fileKey
            };
            var s3Response = await _s3Client.GetObjectAsync(request);
            return s3Response;
        }

    }
}