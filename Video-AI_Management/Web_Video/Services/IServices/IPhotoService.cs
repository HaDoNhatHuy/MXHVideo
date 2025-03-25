using Microsoft.AspNetCore.Http;

namespace Web_Video.Services.IServices
{
    public interface IPhotoService
    {
        string UploadPhotoLocally(IFormFile photo, string oldPhotoPath = "");
    }
}
