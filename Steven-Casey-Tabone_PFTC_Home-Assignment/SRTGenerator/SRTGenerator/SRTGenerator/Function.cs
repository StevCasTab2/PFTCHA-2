using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using SRTGenerator.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace SRTGenerator
{
    public class Function : IHttpFunction
    {
        private readonly GoogleCredential googleCredential = GoogleCredential.FromFile("../../../theta-solution-377011-94bfb5b80ee9.json");
        private StorageClient storageClient;
        private FirestoreDb _db;
        private ILogger<Function> _logger;


        /// <summary>
        /// Logic for your function goes here.
        /// </summary>
        /// <param name="context">The HTTP context, containing the request and the response.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task HandleAsync(HttpContext context)
        {
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "../../../theta-solution-377011-94bfb5b80ee9.json");
            _db = FirestoreDb.Create("theta-solution-377011");

            storageClient = StorageClient.Create(googleCredential);

            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            using TextReader reader = new StreamReader(request.Body);
            string json = await reader.ReadToEndAsync();
            VideoInfoForDatabase vd = JsonConvert.DeserializeObject<VideoInfoForDatabase>(json);
            await GenerateSRT(vd);
        }

        public async Task GenerateSRT(VideoInfoForDatabase VD)
        {
            string path = VD.VideoName + ".srt";
            using (StreamWriter sw = System.IO.File.CreateText(path)) ;
            string transcription = VD.TranscriptionString;
            transcription = transcription.Replace("^", Environment.NewLine);

            using (StreamWriter writer = new StreamWriter(path))
            {
                for (int i = 0; i < transcription.Length; i++)
                {
                    string c = transcription.Substring(i, 1);
                    if (c == "^")
                    {
                        writer.WriteLine();
                    }
                    else if (c == "~")
                    {
                        writer.Write(" ");
                    }
                    else
                    {
                        writer.Write(c);
                    }
                }
            }

            IFormFile srtFile;
            var stream = System.IO.File.OpenRead(path);
            srtFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));

            string n = VD.VideoStorageName;
            n = n.Replace(".flac", "");
            string SRTUrl = await UploadFileToProcessedAsync(srtFile, n + ".srt");

            await UpdateSRTUrl(VD, SRTUrl);
            stream.Dispose();
            System.IO.File.Delete(srtFile.FileName);
        }

        public async Task<VideoInfoForDatabase> GetVideoModel(string VidId)
        {

            Query VidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
            QuerySnapshot VidSnapshot = await VidQuery.GetSnapshotAsync();
            VideoInfoForDatabase VD = VidSnapshot.Documents[0].ConvertTo<VideoInfoForDatabase>();

            return VD;
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

        public async Task UpdateSRTUrl(VideoInfoForDatabase VD, string SRTURL)
        {
            Query vidQuery = _db.Collection("Videos").WhereEqualTo("VideoStorageName", VD.VideoStorageName);
            QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
            DocumentReference DR = _db.Collection("Videos").Document(VidVidQuery.Documents[0].Id);

            await DR.UpdateAsync("SRTUrl", SRTURL);
        }
    }
}