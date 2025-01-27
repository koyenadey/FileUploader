using FileStorage.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FileStorage.Controllers;

[ApiController]
[Route("api/filestorage")]
public class FileStorageController : ControllerBase
{
    /*
     * TODO: Place your code here, do not hesitate use the whole solution to implement code for the assignment
     * AWS Resources are placed in us-east-1 region by default
     */

    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<FileStorageController> _logger;

    public FileStorageController(IFileStorageService fileStorageService, ILogger<FileStorageController> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }


    [HttpGet("listfiles")]
    public async Task<IActionResult> ListAllFiles([FromQuery] string? hashCode)
    {
        _logger.LogInformation("Getting all the files from S3...");
        var fileNames = await _fileStorageService.ListAllFiles(hashCode);
        return Ok(fileNames);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
    {
        //Check if the file is null or empty
        if (file == null || file.Length == 0)
        {
            return BadRequest("The file is empty");
        }
        var uploadToS3 = await _fileStorageService.UploadFilesToS3(file);
        var uploadToDynamoDb = await _fileStorageService.SaveHashToDynamoDb(uploadToS3.FileKey, uploadToS3.FileHash);
        return Ok(uploadToDynamoDb);
    }

}