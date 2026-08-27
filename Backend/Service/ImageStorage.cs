using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace API.Service
{
    /// <summary>
    /// Single source of truth for where uploaded images live: UploadController writes there and
    /// Program.cs serves the same folder at <c>/Image</c>. Keeping both sides on this helper is
    /// what guarantees an uploaded file is actually reachable afterwards.
    /// </summary>
    public static class ImageStorage
    {
        /// <summary>
        /// <c>Images:Root</c> when configured, otherwise the historical
        /// <c>&lt;ContentRoot&gt;/wwwroot/Image</c>. The override exists for the same reason
        /// <c>ExcelExport:StorageRoot</c> does: in PROD the content root is the <c>M:</c> file
        /// share, which the app may not be able to write to.
        /// </summary>
        public static string ResolveRoot(IWebHostEnvironment environment, IConfiguration configuration)
        {
            var configured = configuration["Images:Root"];

            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(environment.ContentRootPath, "wwwroot", "Image")
                : Path.GetFullPath(configured);
        }
    }
}
