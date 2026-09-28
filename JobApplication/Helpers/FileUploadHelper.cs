using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;

namespace JobApplication.Helpers
{
    public static class FileUploadHelper
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg",
            // Documents
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv"
        };

        public static bool IsAllowed(IFormFile file, out string errorMessage)
        {
            errorMessage = "";

            if (file == null || file.Length == 0)
            {
                return true;
            }

            if (file.Length > MaxFileSizeBytes)
            {
                errorMessage = "File size must not exceed 5 MB.";
                return false;
            }

            var extension = Path.GetExtension(file.FileName);

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                errorMessage = "Only image and document formats are allowed (JPG, PNG, GIF, PDF, DOC, XLS, TXT, etc.).";
                return false;
            }

            return true;
        }
    }
}