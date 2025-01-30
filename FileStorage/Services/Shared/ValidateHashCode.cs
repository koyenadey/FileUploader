using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FileStorage.Services.Shared.Attributes
{
    public class ValidateHashCode : ValidationAttribute
    {
        // SHA-256 regex pattern for 64 hexadecimal characters
        private const string Pattern = @"^[a-fA-F0-9]{64}$";

        public override bool IsValid(object? value)
        {
            if (string.IsNullOrEmpty(value as string))
            {
                return true;
            }

            if (value is string hashVal)
            {
                return Regex.IsMatch(hashVal, Pattern);
            }

            return false;
        }

    }
}