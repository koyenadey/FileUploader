
using System.Security.Cryptography;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using FileStorage.DTO;


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
        public async Task<FileStorageResponseDto> UploadFilesToS3(IFormFile file)
        {
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
                            BucketName = "storage",
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
                return new FileStorageResponseDto
                {
                    IsSuccess = true,
                    FileKey = fileKey,
                    Message = "File upload completed successfully.",
                    FileHash = fileHash
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during multipart upload: {ex.Message}");
                return new FileStorageResponseDto
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }

        }


        public async Task<string> SaveHashToDynamoDb(string fileKey, string fileHash)
        {
            bool IsSuccess = false;

            var tableName = _dynamoTableName;

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
                IsSuccess = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error updating data to DynamoDb", ex.Message);
            }
            return IsSuccess ? "File Metadata saved successfully!!" : "Error saving data to DynamoDb";
        }


        public async Task<ListFileDto> ListAllFiles()
        {
            ListFileDto fileDto = new(false, 0);
            try
            {
                //Prepare the scan request
                var scanRequest = new ScanRequest
                {
                    TableName = _dynamoTableName
                };

                // Scan the DynamoDB table to get all file records
                var scanResponse = await _dynamoDbClient.ScanAsync(scanRequest);

                var validFiles = new List<DynamoDBFile>();


                foreach (var item in scanResponse.Items)
                {
                    // Extract file name from the DynamoDB record
                    var fileName = item["Filename"].S;
                    var fileHash = item["FileHash"].S;
                    var uploadedAt = item["UploadedAt"].S;

                    // Check if the file exists in the S3 bucket
                    try
                    {
                        var s3Response = await _s3Client.GetObjectMetadataAsync("storage", fileName);

                        // If file exists, add it to the valid files list
                        validFiles.Add(new DynamoDBFile
                        {
                            FileName = fileName,
                            FileHash = fileHash,
                            UploadedAt = uploadedAt
                        });
                    }
                    catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"File '{item["Filename"].S}' does not exist in DynamoDB but is still in S3. Skipping...");

                        //Continue to next item
                        continue;

                        /* It is not advisable to delete data from GET request
                        Console.WriteLine("File not found in S3, remove the corresponding record from DynamoDB");
                        var deleteRequest = new DeleteItemRequest
                        {
                            TableName = "Files",
                            Key = new Dictionary<string, AttributeValue>
                            {
                                { "Filename", new AttributeValue { S = fileName } },
                                // { "FileHash", new AttributeValue { S = fileHash } },
                                { "UploadedAt", new AttributeValue { S = uploadedAt } }
                            }
                        };

                        await _dynamoDbClient.DeleteItemAsync(deleteRequest); */
                    }
                }

                // Return the list of valid files
                fileDto = new ListFileDto(true, validFiles.Count(), validFiles);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Internal server error: {ex.Message}");
            }

            return fileDto;
        }
    }
}