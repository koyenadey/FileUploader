using FileStorage.Services.Shared.Attributes;
using FileStorage.Services.ValueObject;

namespace FileStorage.Services.DTO
{
    public class DynamoDBFile
    {
        public string FileName { get; set; } = String.Empty;
        public string FileHash { get; set; } = String.Empty;
        public string UploadedAt { get; set; } = String.Empty;
    }

    public class ListFileResponseDto
    {
        public Status Status { get; set; }
        public int TotalFileCount { get; set; }
        public List<DynamoDBFile> Files { get; set; } = new();

        public ListFileResponseDto(Status status, int totalCount)
        {
            Status = status;
            TotalFileCount = totalCount;
        }
        public ListFileResponseDto(Status status, int totalCount, List<DynamoDBFile> files)
        {
            Status = status;
            TotalFileCount = totalCount;
            Files = files;
        }
    }

    public class ListFileInputDto
    {
        [ValidateHashCode]
        public string? hashCode { get; set; } = string.Empty;
    }
}