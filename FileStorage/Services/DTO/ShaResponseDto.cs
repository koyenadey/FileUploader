using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileStorage.Services.ValueObject;

namespace FileStorage.Services.DTO
{
    public class ShaResponseDto
    {
        public Status Status { get; set; } = Status.Error;
        public string FileHash { get; set; } = String.Empty;
        public string Message { get; set; } = String.Empty;


        public ShaResponseDto(Status status, string fileHash, string message)
        {
            Status = status;
            FileHash = fileHash;
            Message = message;
        }
    }
}