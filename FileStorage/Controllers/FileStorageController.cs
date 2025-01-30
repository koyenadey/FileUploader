using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using FileStorage.Services;
using FileStorage.Services.DTO;
using FileStorage.Services.Shared.Attributes;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FileStorage.Controllers;

[ApiController]
[Route("api/filestorage")]
public class FileStorageController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;

    public FileStorageController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }


    [HttpGet("listfiles")]
    public async Task<IActionResult> ListAllFiles([FromQuery] ListFileInputDto? fileInputDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var fileNames = await _fileStorageService.ListAllFiles(fileInputDto?.HashCode);
        return Ok(fileNames);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] UploadFileInputDto uploadFileDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var file = uploadFileDto.UploadFile;

        var uploadToS3 = await _fileStorageService.UploadFilesToS3(file);
        Console.WriteLine("ABCD" + uploadToS3.FileKey);
        var uploadToDynamoDb = await _fileStorageService.SaveHashToDynamoDb(uploadToS3.FileKey, uploadToS3.FileHash);
        return Ok(uploadToDynamoDb);
    }

    [HttpGet("downloadfile/{fileKey}")]
    public async Task<IActionResult> DownloadFileFromS3([FromRoute] string fileKey)
    {
        var result = await _fileStorageService.DownloadFromS3(fileKey);

        return result.StatusCode switch
        {
            StatusCodes.Status404NotFound => NotFound(result),
            StatusCodes.Status400BadRequest => BadRequest(result),
            StatusCodes.Status500InternalServerError => StatusCode(StatusCodes.Status500InternalServerError, result),
            _ => Ok(result)
        };
    }

    [HttpGet("download/{fileKey}")]
    public async Task<IActionResult> DownloadFile(string fileKey)
    {
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        try
        {
            var s3Response = await _fileStorageService.GetS3ClientResponseObject(fileKey);

            Response.Headers.Append("Content-Disposition", $"attachment; filename={fileKey}");
            Response.Headers.Append("Content-Type", s3Response.Headers.ContentType);
            Response.Headers.Append("Content-Length", s3Response.Headers.ContentLength.ToString());


            Response.ContentType = s3Response.Headers.ContentType;
            Response.ContentLength = s3Response.Headers.ContentLength;

            await using (var responseStream = s3Response.ResponseStream)
            {
                await responseStream.CopyToAsync(Response.Body, bufferSize: 81920); // Use a large buffer size (e.g., 80 KB)
            }
            return new EmptyResult();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound($"File with key '{fileKey}' not found in S3 bucket.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }

    }

}