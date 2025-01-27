using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FileStorage.Services.ValueObject;

namespace UploadFilesToS3.DTO
{
    public class UploadFileToS3Dto
    {
        public Status Status { get; set; }
        public string Message { get; set; } = String.Empty;
        public string FileKey { get; set; } = String.Empty;
        public string FileHash { get; set; } = String.Empty;

        public UploadFileToS3Dto(Status status, string message, string filekey, string filehash)
        {
            Status = status;
            Message = message;
            FileKey = filekey;
            FileHash = filehash;
        }

        public UploadFileToS3Dto(Status status, string message)
        {
            Status = status;
            Message = message;
        }
    }
}