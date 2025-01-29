using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FileStorage.Services.ValueObject;

namespace FileStorage.Services.DTO
{
    public class DownloadFileFromS3Dto
    {
        public Status Status { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = String.Empty;
        public string DownloadUrl { get; set; } = String.Empty;

        public DownloadFileFromS3Dto(Status status, int statuscode, string message, string url)
        {
            Status = status;
            StatusCode = statuscode;
            Message = message;
            DownloadUrl = url;
        }
    }
}