using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using FileStorage.Services;
using FileStorage.Services.DTO;
using FileStorage.Services.ValueObject;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace FileStorage.Tests
{
    public class FileStorageServiceTests
    {
        private FileStorageService _filestorageService; // SUT

        //fake the dependencies from MOQ
        private Mock<IAmazonS3> _s3MockClient;
        private Mock<IAmazonDynamoDB> _dynamoDbMockClient;
        private readonly Mock<IConfiguration> _config;
        private readonly Mock<ITransferUtility> _transferUtilityMock;
        private readonly Mock<ILogger<FileStorageService>> _loggerMock;

        public FileStorageServiceTests()
        {
            _s3MockClient = new Mock<IAmazonS3>();
            _dynamoDbMockClient = new Mock<IAmazonDynamoDB>();
            _loggerMock = new Mock<ILogger<FileStorageService>>();

            _config = new Mock<IConfiguration>();
            _config.Setup(c => c["MyConfigKey"]).Returns("http://localstack:4566");

            _transferUtilityMock = new Mock<ITransferUtility>();

            _filestorageService =
                new FileStorageService(_s3MockClient.Object, _dynamoDbMockClient.Object, _config.Object, _transferUtilityMock.Object, _loggerMock.Object);
        }


        [Fact]
        public async Task UploadFiles_ToAmazonS3_ReturnsSuccess_WhenCompletes()
        {
            // Arrange
            var fileName = "text.txt";
            var content = "Hello World.";
            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            // Set up the file mock
            var mockfile = new Mock<IFormFile>();
            mockfile.Setup(f => f.FileName).Returns(fileName);
            mockfile.Setup(f => f.ContentType).Returns("text/plain");
            mockfile.Setup(f => f.OpenReadStream()).Returns(fileStream);
            mockfile.Setup(f => f.Length).Returns(fileStream.Length);

            CancellationToken cancellationToken = default;

            // Set up the mock for TransferUtility.UploadAsync with expected parameters
            _transferUtilityMock.Setup(mtu =>
                mtu.UploadAsync(
                    It.IsAny<TransferUtilityUploadRequest>(),
                    cancellationToken))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _filestorageService.UploadFilesToS3(mockfile.Object); // Here, you pass the mock file object to the method

            // Assert
            Assert.NotNull(result.FileHash);
            Assert.Equal(Status.Success, result.Status);
            Assert.Equal("File upload completed successfully.", result.Message);

            // Verify that UploadAsync was called with correct parameters
            _transferUtilityMock.Verify(utility => utility.UploadAsync(
                It.Is<TransferUtilityUploadRequest>(req =>
                    req.BucketName == "storage" &&
                    req.Key.Contains("text.txt") &&
                    req.InputStream != null
                ),
                cancellationToken), Times.Once);
        }


        [Fact]
        public async Task UploadHash_ToDynamoDB_ReturnsSuccess()
        {
            //Arrange
            var fileKey = "a-mock-file-key-234";
            var fileHash = "abc7dghi55jk00kl";

            CancellationToken cancellationToken = default;

            _dynamoDbMockClient.Setup(
                d => d.PutItemAsync(
                    It.IsAny<PutItemRequest>(), cancellationToken)
                ).ReturnsAsync(new PutItemResponse());

            //Act
            var result = await _filestorageService.SaveHashToDynamoDb(fileKey, fileHash);

            //Assert
            Assert.Equal("File Metadata saved successfully!!", result.Message);
        }

        [Fact]
        public async Task TestingListAllFilesMethod_ByShaValue_ReturnsTheFileDetails_OfExistingFiles()
        {
            //Arrange
            var mockFileStorageService =
                new Mock<IFileStorageService>();

            var allFilesbySha = new List<DynamoDBFile>
            {
                new DynamoDBFile
                {
                    FileName="file1.txt",
                    FileHash = "So456me87Ha67s11hV56akkl45u23e",
                    UploadedAt = "2025-01-01"
                },
                new DynamoDBFile
                {
                    FileName="file1.txt",
                    FileHash = "So456me87Ha67s11hV56akkl45u23e",
                    UploadedAt = "2025-01-01"
                },

            };

            var shaValue = "So456me87Ha67s11hV56akkl45u23e";

            var itemsAttributes = new List<Dictionary<string, AttributeValue>>();
            var item = new Dictionary<string, AttributeValue>
            {
                { "Filename", new AttributeValue { S = "file1.txt" } },
                { "FileHash", new AttributeValue { S = "So456me87Ha67s11hV56akkl45u23e" } },
                { "UploadedAt", new AttributeValue{ S = "2025-01-01" } }
            };
            itemsAttributes.Add(item);
            itemsAttributes.Add(item);
            var queryResponse = new QueryResponse()
            {
                Items = itemsAttributes
            };

            mockFileStorageService.Setup(mfs => mfs.GetAllFilesBySha(shaValue)).ReturnsAsync(allFilesbySha);
            mockFileStorageService.Setup(mfs => mfs.IsFileInS3Bucket("file1.txt")).ReturnsAsync(true);
            _dynamoDbMockClient.Setup(d => d.QueryAsync(
                It.IsAny<QueryRequest>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(queryResponse);

            //Act

            var result = await _filestorageService.ListAllFiles(shaValue);

            //Assert
            Assert.Equal(2, result.TotalFileCount);
            Assert.Equal(Status.Success, result.Status);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task TestingListAllFilesMethod_ByNoShaValue_ReturnsTheFileDetails_OfAllFiles()
        {
            //Arrange
            var mockFileStorageService =
                new Mock<IFileStorageService>();

            var itemsAttributes = new List<Dictionary<string, AttributeValue>>();
            var item = new Dictionary<string, AttributeValue>
            {
                { "Filename", new AttributeValue { S = "file1.txt" } },
                { "FileHash", new AttributeValue { S = "some1453hash" } },
                { "UploadedAt", new AttributeValue{ S = "2025-01-01" } }
            };
            itemsAttributes.Add(item);

            var scanResponse = new ScanResponse()
            {
                Items = itemsAttributes
            };

            var listFiles = new List<DynamoDBFile>();
            listFiles.Add(new DynamoDBFile
            {
                FileName = "file1.txt",
                FileHash = "some1453hash",
                UploadedAt = "2025-01-01"
            });


            _dynamoDbMockClient.Setup(d =>
                 d.ScanAsync(It.IsAny<ScanRequest>(),
                 It.IsAny<CancellationToken>()))
                 .ReturnsAsync(scanResponse);

            mockFileStorageService.Setup(mfs => mfs.GetAllFiles()).ReturnsAsync(listFiles);
            mockFileStorageService.Setup(mfs => mfs.IsFileInS3Bucket("file1.txt")).ReturnsAsync(true);

            //Act
            var result = await _filestorageService.ListAllFiles(String.Empty);

            //Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.TotalFileCount);
            Assert.Equal("file1.txt", result.Files[0].FileName);
        }

        [Fact]
        public async Task GetAllFileByHash_FromDynamoDb_WithExistingHash_ReturnsListOfFiles()
        {
            var itemsAttributes = new List<Dictionary<string, AttributeValue>>();
            var item = new Dictionary<string, AttributeValue>
            {
                { "Filename", new AttributeValue { S = "file1.txt" } },
                { "FileHash", new AttributeValue { S = "some1453hash" } },
                { "UploadedAt", new AttributeValue{ S = "2025-01-01" } }
            };
            itemsAttributes.Add(item);
            var queryResponse = new QueryResponse()
            {
                Items = itemsAttributes
            };

            _dynamoDbMockClient.Setup(d =>
                    d.QueryAsync(It.IsAny<QueryRequest>(),
                    It.IsAny<CancellationToken>())).
                    ReturnsAsync(queryResponse);

            //Act
            var result = await _filestorageService.GetAllFilesBySha("some1453hash");

            //Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("file1.txt", result[0].FileName);
            Assert.Equal("some1453hash", result[0].FileHash);
            Assert.Equal("2025-01-01", result[0].UploadedAt);
        }

        [Fact]
        public async Task GetAllFilesByHash_FromDynamoDb_ThatDoesNotExist_ReturnsEmptyList()
        {
            var itemsAttributes = new List<Dictionary<string, AttributeValue>>();
            var queryResponse = new QueryResponse()
            {
                Items = itemsAttributes
            };

            _dynamoDbMockClient.Setup(d =>
                    d.QueryAsync(It.IsAny<QueryRequest>(),
                    It.IsAny<CancellationToken>())).
                    ReturnsAsync(queryResponse);

            //Act
            var result = await _filestorageService.GetAllFilesBySha("some1453hash");

            //Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task DownloadFilesByValidKey_ReturnsFileUrl_IfExists()
        {
            var fileGuid = Guid.NewGuid();
            var mockFileKey = $"{fileGuid}_example.txt";

            _s3MockClient.Setup(s =>
                s.GetPreSignedURLAsync(
                    It.IsAny<GetPreSignedUrlRequest>()))
                    .ReturnsAsync($"https://localhost:5000/{mockFileKey}");

            var result = await _filestorageService.DownloadFromS3(mockFileKey);

            Assert.Equal($"https://localhost:5000/{mockFileKey}", result.DownloadUrl);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal(Status.Success, result.Status);
        }

        [Fact]
        public async Task DownloadFileByInValidKey_ReturnsError()
        {
            var fileGuid = Guid.NewGuid();
            var mockFileKey = $"{fileGuid}_example.txt";
            var fileKey = $"{Guid.NewGuid()}_example.txt";

            Console.WriteLine("file: " + fileKey);

            _s3MockClient
                .Setup(mfs => mfs.GetObjectMetadataAsync("storage", fileKey, default))
                .Throws(new Exception("Not Found"));

            var responseObj = new DownloadFileFromS3Dto(Status.Error, 404, "", "Not Found");

            var result = await _filestorageService.DownloadFromS3(fileKey);

            Assert.Equal(404, result.StatusCode);
            Assert.Equal("Not Found", result.DownloadUrl);
            Assert.Equal(Status.Error, result.Status);
        }
    }
}