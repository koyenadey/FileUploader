using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;


namespace FileStorage.Services.Shared.Attributes
{
    public class ValidateFileKey : ActionFilterAttribute
    {
        private readonly string Pattern = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.TryGetValue("fileKey", out var keyVal) && keyVal is string fileKey)
            {
                if (!Regex.IsMatch(fileKey, Pattern))
                {
                    context.Result = new BadRequestObjectResult("Invalid key format. Expected format: <GUID>_filename.txt");
                    return;
                }
            }
            else
            {
                context.Result = new BadRequestObjectResult("Invalid file key.");
                return;
            }
        }
    }
}