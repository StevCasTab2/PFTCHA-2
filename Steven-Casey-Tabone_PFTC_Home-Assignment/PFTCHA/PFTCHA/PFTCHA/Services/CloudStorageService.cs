namespace PFTCHA.Services
{
    public class CloudStorageService
    {
        public interface ICloudStorageService
        {
            Task<string> GetSignedUrlAsync(string filetoread, int timeOutInMinutes = 30);
            Task<string> UploadFileAsync(IFormFile file, string fileName);
            Task DeleteFileAsync(string videoname);
        }
    }
}
