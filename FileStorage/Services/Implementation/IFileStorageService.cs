using Amazon.S3;
using Amazon.S3.Model;
using FileStorage.Services.DTO;

namespace FileStorage.Services
{
    public interface IFileStorageService
    {
        public Task<ResponseFileUploadDto> UploadFilesToS3(IFormFile file);
        public Task<ShaResponseDto> SaveHashToDynamoDb(string fileKey, string fileHash);
        public Task<ListFileResponseDto> ListAllFiles(string? hashCode);
        public Task<DownloadFileFromS3Dto> DownloadFromS3(string fileKey);
        public Task<GetObjectResponse> GetS3ClientResponseObject(string fileKey);
        public Task<bool> IsFileInS3Bucket(string fileName);
        public Task<List<DynamoDBFile>> GetAllFilesBySha(string hashCode);
        public Task<List<DynamoDBFile>> GetAllFiles();
    }
}