using FileStorage.Services.DTO;
using ListFiles.DTO;
using UploadFilesToS3.DTO;


namespace FileStorage.Services
{
    public interface IFileStorageService
    {
        public Task<UploadFileToS3Dto> UploadFilesToS3(IFormFile file);
        public Task<ShaResponseDto> SaveHashToDynamoDb(string fileKey, string fileHash);
        public Task<ListFileDto> ListAllFiles(string? hashCode);
    }
}