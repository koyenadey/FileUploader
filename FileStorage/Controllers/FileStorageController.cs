using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FileStorage.Services;
using FileStorage.Services.DTO;
using FileStorage.Services.Shared.Attributes;
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
        var uploadToDynamoDb = await _fileStorageService.SaveHashToDynamoDb(uploadToS3.FileKey, uploadToS3.FileHash);
        return Ok(uploadToDynamoDb);
    }

    [HttpGet("download/{fileKey}")]
    [ValidateFileKey]
    public async Task<IActionResult> DownloadFile([FromRoute] string fileKey)
    {
        var result = await _fileStorageService.DownloadFileFromS3(fileKey);

        return result.StatusCode switch
        {
            StatusCodes.Status404NotFound => NotFound(result),
            StatusCodes.Status400BadRequest => BadRequest(result),
            StatusCodes.Status500InternalServerError => StatusCode(StatusCodes.Status500InternalServerError, result),
            _ => Ok(result)
        };
    }

}