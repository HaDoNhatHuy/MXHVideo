using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.IO;
using System;
using Web_Video.Services.IServices;

namespace Web_Video.Services
{
    public class PhotoService : IPhotoService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        public PhotoService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public void DeletePhotoLocally(string photoUrl)
        {
            string webRootPath=_webHostEnvironment.WebRootPath;
            string uploadsDirectory = Path.Combine(webRootPath, @"images\thumbnails");
            //delete the image
            var oldImagePath = Path.Combine(webRootPath, photoUrl.TrimStart('\\'));
            if(File.Exists(oldImagePath))
            {
                File.Delete(oldImagePath);
            }
        }

        public string UploadPhotoLocally(IFormFile photo, string oldPhotoPath = "")
        {
            string webRootPath = _webHostEnvironment.WebRootPath;
            string uploadsDriectory = Path.Combine(webRootPath, @"images\thumbnails");

            // check if the directory exists, othewise create
            if (!Directory.Exists(uploadsDriectory))
            {
                Directory.CreateDirectory(uploadsDriectory);
            }

            string fileName = Guid.NewGuid().ToString();
            var extension = Path.GetExtension(photo.FileName);

            if (!string.IsNullOrEmpty(oldPhotoPath))
            {
                // replace the image
                var oldImagePath = Path.Combine(webRootPath, oldPhotoPath.TrimStart('\\'));
                if (File.Exists(oldImagePath))
                {
                    File.Delete(oldImagePath);
                }
            }

            using var fileStream = new FileStream(Path.Combine(uploadsDriectory, fileName + extension), FileMode.Create);
            photo.CopyTo(fileStream);

            return @"\images\thumbnails\" + fileName + extension;
        }
    }
}
