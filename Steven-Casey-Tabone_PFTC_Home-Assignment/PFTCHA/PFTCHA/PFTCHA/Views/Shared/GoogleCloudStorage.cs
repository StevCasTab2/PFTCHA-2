using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using PFTCHA.Services;
using static PFTCHA.Services.CloudStorageService;

namespace PFTCHA.Views.Shared
{
    public class GoogleCloudStorage : ICloudStorageService
    {
        private readonly GoogleCredential googleCredential;
        private readonly StorageClient storageClient;
        private readonly string bucketName;

        public GoogleCloudStorage(IConfiguration config)
        {
            googleCredential = GoogleCredential.FromFile(config.GetValue<string>("Authentication:Google:Credentials"));
            storageClient = StorageClient.Create(googleCredential);
            bucketName = config.GetValue<string>("Authentication:Google:CloudStorageBucket");
        }
        public Task<string> GetSignedUrlAsync(string filetoread, int timeOutInMinutes = 30)
        {
            throw new NotImplementedException();
        }

        public async Task<string> UploadFileAsync(IFormFile file, string fileName)
        {
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var dataObject = await storageClient.UploadObjectAsync("unconv_videos", fileName, null, memoryStream);
                return dataObject.MediaLink;
            }
        }
        public async Task<string> UploadFileToProcessedAsync(IFormFile file, string fileName)
        {
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var dataObject = await storageClient.UploadObjectAsync("processed_audiofiles", fileName, null, memoryStream);
                return dataObject.MediaLink;
            }
        }
        public async Task DeleteFileAsync(string videoname)
        {
            await storageClient.DeleteObjectAsync("unconv_videos", videoname);
        }
    }
}
