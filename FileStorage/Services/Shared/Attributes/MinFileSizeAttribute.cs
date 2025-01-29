using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace FileStorage.Services.Shared
{
    public class MinFileSizeAttribute : ValidationAttribute
    {
        private readonly int _minFileSize;
        public MinFileSizeAttribute(int minFileSize)
        {
            _minFileSize = minFileSize;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var file = value as IFormFile;
            if (file != null && file.Length < _minFileSize)
            {
                return new ValidationResult(GetErrorMessage());
            }
            return ValidationResult.Success;
        }

        public string GetErrorMessage()
        {
            return $"Minimum allowed file size is {_minFileSize / 1024} KB.";
        }

    }
}