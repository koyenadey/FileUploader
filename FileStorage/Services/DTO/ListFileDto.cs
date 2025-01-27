using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileStorage.Services.ValueObject;

namespace ListFiles.DTO
{
    public class DynamoDBFile
    {
        public string FileName { get; set; }
        public string FileHash { get; set; }
        public string UploadedAt { get; set; }
    }

    public class ListFileDto
    {
        public Status Status { get; set; }
        public int TotalFileCount { get; set; }
        public List<DynamoDBFile> Files { get; set; } = new();

        public ListFileDto(Status status, int totalCount)
        {
            Status = status;
            TotalFileCount = totalCount;
        }
        public ListFileDto(Status status, int totalCount, List<DynamoDBFile> files)
        {
            Status = status;
            TotalFileCount = totalCount;
            Files = files;
        }

    }
}