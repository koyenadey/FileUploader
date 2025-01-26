using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileStorage.DTO
{
    public class FileStorageResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string FileKey { get; set; }
        public string FileHash { get; set; }

    }
}