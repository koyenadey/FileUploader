
using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using FileStorage.Services.DTO;
using FileStorage.Services.ValueObject;
using ListFiles.DTO;
using UploadFilesToS3.DTO;


namespace FileStorage.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonDynamoDB _dynamoDbClient;
        private readonly string _dynamoTableName = "Files";
        private readonly string _s3BucketName = "storage";
        public FileStorageService(IAmazonS3 s3Client, IAmazonDynamoDB dynamoDBClient)
        {
            _s3Client = s3Client;
            _dynamoDbClient = dynamoDBClient;
        }
        public async Task<UploadFileToS3Dto> UploadFilesToS3(IFormFile file)
        {
            var responseFileUpload = new UploadFileToS3Dto(Status.Error, String.Empty);
            try
            {
                //Generate a unique file name for key
                var fileKey = $"{Guid.NewGuid()}_{file.FileName}";

                // information required to initiate the multipart upload
                var iniateUploadReq = new InitiateMultipartUploadRequest
                {
                    BucketName = "storage",
                    Key = fileKey,
                    ContentType = file.ContentType
                };

                var initiateUploadRes = await _s3Client.InitiateMultipartUploadAsync(iniateUploadReq);
                var uploadId = initiateUploadRes.UploadId;
                var partSize = 5 * 1024 * 1024; //5MB
                var partNumber = 1;
                var partETags = new List<PartETag>();

                // the byte bucket to transfer data
                var buffer = new byte[partSize];

                // Initialize SHA-256 hash computation
                using var sha256 = SHA256.Create();

                //Open the file to read
                using (var fileStream = file.OpenReadStream())
                {
                    var readCount = 0;
                    while ((readCount = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Compute SHA-256 incrementally for the chunk
                        sha256.TransformBlock(buffer, 0, readCount, null, 0);

                        //Prepare request object parts
                        var uploadRequestPart = new UploadPartRequest
                        {
                            BucketName = _s3BucketName,
                            Key = fileKey,
                            UploadId = uploadId,
                            PartNumber = partNumber,
                            PartSize = readCount,
                            InputStream = new MemoryStream(buffer, 0, readCount)
                        };

                        UploadPartResponse uploadPartResponse = await _s3Client.UploadPartAsync(uploadRequestPart);

                        partETags.Add(new PartETag(partNumber, uploadPartResponse.ETag));

                        partNumber++;
                    }

                    // Compute the final SHA-256 computation entirely
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                }

                // Get the final SHA-256 hash
                string fileHash = BitConverter.ToString(sha256.Hash).Replace("-", "").ToLower();

                // Complete multipart upload
                var completeReqUpload = new CompleteMultipartUploadRequest
                {
                    BucketName = "storage",
                    Key = fileKey,
                    UploadId = uploadId,
                    PartETags = partETags
                };

                var completedUploadRes = await _s3Client.CompleteMultipartUploadAsync(completeReqUpload);

                Console.WriteLine("Multipart upload completed successfully.");

                // Return a custom object to the response
                var successMsg = "File upload completed successfully.";
                responseFileUpload = new UploadFileToS3Dto(Status.Success, successMsg, fileKey, fileHash);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during multipart upload: {ex.Message}");
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
                Console.WriteLine("Error updating data to DynamoDb", ex.Message);
            }
            return shaResponseDto;
        }

        public async Task<ListFileDto> ListAllFiles(string? hashCode)
        {
            Console.WriteLine("In Service list all files");
            var validFiles = new List<DynamoDBFile>();
            ListFileDto fileDto = new(Status.Error, 0);

            try
            {
                var foundFileList = await (String.IsNullOrEmpty(hashCode) ?
                    GetAllFiles() : GetAllFilesBySha(hashCode));

                foreach (var foundFile in foundFileList)
                {
                    Console.WriteLine("X:" + foundFile.FileName);
                    var isValid = await IsFileInS3Bucket(foundFile.FileName);
                    if (isValid)
                        validFiles.Add(foundFile);
                }
                // Return the list of valid files
                fileDto = new ListFileDto(Status.Success, validFiles.Count(), validFiles);

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
                Console.WriteLine($"File '{fileName}' does not exist in DynamoDB but is still in S3. Skipping...");
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
                var item = queryResponse.Items[0];
                var foundFile = new DynamoDBFile
                {
                    FileName = item["Filename"].S,
                    FileHash = item["FileHash"].S,
                    UploadedAt = item["UploadedAt"].S
                };
                foundFiles.Add(foundFile);
            }
            return foundFiles;
        }

        public async Task<List<DynamoDBFile>> GetAllFiles()
        {
            Console.WriteLine("GetAllFiles");
            //Prepare the scan request
            var scanRequest = new ScanRequest
            {
                TableName = _dynamoTableName
            };

            // Scan the DynamoDB table to get all file records
            var scanResponse = await _dynamoDbClient.ScanAsync(scanRequest);

            var foundFiles = new List<DynamoDBFile>();

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
            Console.WriteLine("Exiting GetAllFiles" + foundFiles[0].FileName);
            return foundFiles;
        }


    }
}