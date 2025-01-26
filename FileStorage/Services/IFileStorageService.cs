using FileStorage.DTO;


namespace FileStorage.Services
{
    public interface IFileStorageService
    {
        public Task<FileStorageResponseDto> UploadFilesToS3(IFormFile file);
        public Task<string> SaveHashToDynamoDb(string fileKey, string fileHash);

        public Task<ListFileDto> ListAllFiles();
    }
}