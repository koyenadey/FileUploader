using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using FileStorage.Services;
using Moq;
using Xunit;

namespace FileStorage.Tests
{
    public class FileStorageServiceTests
    {
        private FileStorageService _filestorageService; // SUT

        //fake the dependencies from MOQ
        private Mock<IAmazonS3> _s3MockClient;
        private Mock<IAmazonDynamoDB> _dynamoDbMockClient;
        public FileStorageServiceTests()
        {
            _s3MockClient = new Mock<IAmazonS3>();
            _dynamoDbMockClient = new Mock<IAmazonDynamoDB>();
            _filestorageService = new FileStorageService(_s3MockClient.Object, _dynamoDbMockClient.Object);
        }


        [Fact]
        public async Task UploadFiles_ToAmazonS3_ReturnsSuccess_WhenCompletes()
        {
            //Arrange
            var mockfile = new Mock<IFormFile>();
            var fileName = "text.txt";
            var content = "Hello World!";
            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

            mockfile.Setup(f => f.FileName).Returns(fileName);
            mockfile.Setup(f => f.ContentType).Returns("text/plain");
            mockfile.Setup(f => f.OpenReadStream()).Returns(fileStream);

            CancellationToken cancellationToken = default;

            _s3MockClient
                .Setup(s => s.InitiateMultipartUploadAsync(
                    It.IsAny<InitiateMultipartUploadRequest>(),
                    cancellationToken)
                ).ReturnsAsync(new InitiateMultipartUploadResponse { UploadId = "12345" });


            _s3MockClient
                .Setup(s => s.UploadPartAsync(
                    It.IsAny<UploadPartRequest>(),
                    cancellationToken)
                ).ReturnsAsync(new UploadPartResponse { PartNumber = 1, ETag = "e-tag1" });

            _s3MockClient
                .Setup(s => s.CompleteMultipartUploadAsync(
                    It.IsAny<CompleteMultipartUploadRequest>(),
                    cancellationToken)
                ).ReturnsAsync(new CompleteMultipartUploadResponse());

            //Act
            var result = await _filestorageService.UploadFilesToS3(mockfile.Object);

            //Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("File upload completed successfully.", result.Message);
            Assert.NotEmpty(result.FileHash);
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
            Assert.Equal("File Metadata saved successfully!!", result);
        }
    }
}