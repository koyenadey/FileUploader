using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FileStorage.Services.Shared;
using FileStorage.Services.ValueObject;

namespace FileStorage.Services.DTO
{

    public class UploadFileInputDto
    {
        [Required(ErrorMessage = "Please upload a file!")]
        [MinFileSize(128 * 1024)]
        [MaxFileSize(2L * 1024 * 1024 * 1024)]
        public required IFormFile UploadFile { get; set; }
    }
    public class ResponseFileUploadDto
    {
        public Status Status { get; set; }
        public string Message { get; set; } = String.Empty;
        public string FileKey { get; set; } = String.Empty;
        public string FileHash { get; set; } = String.Empty;

        public ResponseFileUploadDto(Status status, string message, string filekey, string filehash)
        {
            Status = status;
            Message = message;
            FileKey = filekey;
            FileHash = filehash;
        }

        public ResponseFileUploadDto(Status status, string message)
        {
            Status = status;
            Message = message;
        }
    }
}