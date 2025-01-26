using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileStorage.DTO
{
    public class DynamoDBFile
    {
        public string FileName { get; set; }
        public string FileHash { get; set; }
        public string UploadedAt { get; set; }
    }

    public class ListFileDto
    {
        public bool IsSuccess { get; set; }
        public int TotalFileCount { get; set; }
        public List<DynamoDBFile> Files = new();

        public ListFileDto(bool status, int totalCount)
        {
            IsSuccess = status;
            TotalFileCount = totalCount;
        }
        public ListFileDto(bool status, int totalCount, List<DynamoDBFile> files)
        {
            IsSuccess = status;
            TotalFileCount = totalCount;
            Files = files;
        }

    }
}