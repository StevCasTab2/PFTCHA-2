using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Storage.V1;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using FileConversion.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Google.Api;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using Google.Cloud.Diagnostics.Common;
using Google.Cloud.Logging.V2;
using Google.Cloud.Logging.Type;

namespace FileConversion
{
    public class Function : IHttpFunction
    {
        private readonly GoogleCredential googleCredential = GoogleCredential.FromFile("../../../projectforpftc-a2c8e69e6062.json");
        private StorageClient storageClient;
        private FirestoreDb _db;
        private ILogger<Function> _logger;
        /// <summary>
        /// Logic for your function goes here.
        /// </summary>
        /// <param name="context">The HTTP context, containing the request and the response.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// 
        public Function(ILogger<Function> logger)
        {
            _logger = logger; 
        }
        public async Task HandleAsync(HttpContext context)
        {
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "../../../projectforpftc-a2c8e69e6062.json");

            storageClient = StorageClient.Create(googleCredential);
            Microsoft.AspNetCore.Http.HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            string VidId = null;
            string User = null;

            using TextReader reader = new StreamReader(request.Body);
            string json = await reader.ReadToEndAsync();
            //JsonElement body = JsonSerializer.Deserialize<JsonElement>(json);
            /*if (body.TryGetProperty("User", out JsonElement property) && property.ValueKind == JsonValueKind.String)
            {
                User = property.GetString();
            }
            if (body.TryGetProperty("VidId", out JsonElement property2) && property2.ValueKind == JsonValueKind.String)
            {
                VidId = property2.GetString();
            }

            if(User != null && VidId != null)
            {

            }*/
            VideoInfoForDatabase vd = JsonConvert.DeserializeObject<VideoInfoForDatabase>(json);
            await DownloadVid(vd);
            //Console.WriteLine(body.ToString());
            storageClient = null;
        }

        private void ConvertAudiofromVideo(string vidname, string OldExtension, string newExtension)
        {
            var inputFile = new MediaFile { Filename = vidname + OldExtension };
            var outputFile = new MediaFile { Filename = vidname + newExtension };

            var conversionOptions = new ConversionOptions
            {
                AudioSampleRate = AudioSampleRate.Hz22050
            };

            using (var engine = new Engine())
            {
                engine.ConvertProgressEvent += Engine_ConvertProgressEvent;
                engine.Convert(inputFile, outputFile);//, conversionOptions);
            }

            System.IO.File.Delete(vidname + OldExtension);
        }

        private void Engine_ConvertProgressEvent(object sender, MediaToolkit.ConvertProgressEventArgs e)
        {
            Console.WriteLine(e.ProcessedDuration);
        }

        private async Task DownloadVid(VideoInfoForDatabase VD)
        {
            _db = FirestoreDb.Create("projectforpftc");

            /*Query VidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
            QuerySnapshot VidSnap = await VidQuery.GetSnapshotAsync();
            DocumentReference vidref = _db.Collection(User).Document(VidSnap.Documents[0].Id);
            VideoInfoForDatabase VD = VidSnap.Documents[0].ConvertTo<VideoInfoForDatabase>();*/


            var outputFile = File.OpenWrite("../../../VidsHolder/" + VD.VideoName + ".mp4");
            var outputFile2 = File.OpenWrite("../../../VidsHolder/" + VD.ImgStorageName);
            try
            {
                WriteLogEntry("Conversion","Conversion: Downloading " + VD.owner + "'s video");
                await storageClient.DownloadObjectAsync("unconv_videos2", VD.VideoStorageName, outputFile);
                await storageClient.DownloadObjectAsync("unconv_videos2", VD.ImgStorageName, outputFile2);
                outputFile.Dispose();
                outputFile2.Dispose();
                WriteLogEntry("Conversion","Conversion: Converting " + VD.owner + "'s video to wav");
                ConvertAudiofromVideo("../../../VidsHolder/" + VD.VideoName, ".mp4", ".wav");
                WriteLogEntry("Conversion","Conversion: Converting " + VD.owner + "'s video to flac");
                ConvertAudiofromVideo("../../../VidsHolder/" + VD.VideoName, ".wav", ".flac");

                WriteLogEntry("Conversion","Conversion: Conversion Complete for " + VD.owner + "'s video: " + VD.VideoName);
                /*IFormFile VidFile;
                using (var stream = System.IO.File.OpenRead("../../../VidsHolder/" + VD.VideoName + ".flac"))
                {
                    VidFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
                    stream.Dispose();
                }
                IFormFile ImgFile;
                using (var stream = System.IO.File.OpenRead("../../../VidsHolder/" + VD.ImgStorageName))
                {
                    ImgFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
                    stream.Dispose();
                }*/

                IFormFile VidFile;
                IFormFile ImgFile;

                var stream = System.IO.File.OpenRead("../../../VidsHolder/" + VD.VideoName + ".flac");
                VidFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));
                stream = System.IO.File.OpenRead("../../../VidsHolder/" + VD.ImgStorageName);
                ImgFile = new FormFile(stream, 0, stream.Length, null, Path.GetFileName(stream.Name));

                if (VidFile != null && ImgFile != null)
                {
                    string[] vid = VD.VideoStorageName.Split(".");
                    WriteLogEntry("Conversion","Conversion: Uploading " + VD.owner + "'s flac file: " + VD.VideoName);
                    string vidpath = await UploadFileAsync(VidFile, vid[0] + ".flac", "processed_audiofiles2");
                    string imgpath = await UploadFileAsync(ImgFile, VD.ImgStorageName, "processed_audiofiles2");
                    WriteLogEntry("Conversion","Conversion: Uploaded " + VD.owner + "'s flac file: " + VD.VideoName);
                    Query vidQuery = _db.Collection("Videos").WhereEqualTo("VideoStorageName", VD.VideoStorageName);
                    QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
                    DocumentReference DR = _db.Collection("Videos").Document(VidVidQuery.Documents[0].Id);
                    WriteLogEntry("Conversion","Conversion: Updating data for " + VD.owner + "'s flac file: " + VD.VideoName);
                    await DR.UpdateAsync("VideoUrl", null);
                    await DR.UpdateAsync("ThumbnailUrl", imgpath);
                    await DR.UpdateAsync("FlacUrl", vidpath);
                    await DR.UpdateAsync("VideoStorageName", vid[0] + ".flac");
                    WriteLogEntry("Conversion","Conversion: Updated data for " + VD.owner + "'s flac file: " + VD.VideoName);
                    WriteLogEntry("Conversion","Conversion: Deleting old files for " + VD.owner + "'s flac file: " + VD.VideoName);
                    storageClient.DeleteObject("unconv_videos2", VD.VideoStorageName);
                    storageClient.DeleteObject("unconv_videos2", VD.ImgStorageName);
                    WriteLogEntry("Conversion","Conversion: Deleted old files for " + VD.owner + "'s flac file: " + VD.VideoName);

                    WriteLogEntry("Conversion","Conversion COMPLETED: " + VD.owner + "'s flac file: " + VD.VideoName);
                }


                stream.Dispose();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                /*Query vidref = _db.Collection("Videos").WhereEqualTo("VideoStorageName", VD.VideoStorageName);
                QuerySnapshot vd = await vidref.GetSnapshotAsync();
                DocumentReference DR = _db.Collection("Videos").Document(vd.Documents[0].Id);
                await DR.DeleteAsync();*/

                if(File.Exists("../../../VidsHolder/" + VD.VideoName + ".flac"))
                {
                    File.Delete("../../../VidsHolder/" + VD.VideoName + ".flac");
                }
                if(File.Exists("../../../VidsHolder/" + VD.VideoName + ".mp4"))
                {
                    File.Delete("../../../VidsHolder/" + VD.VideoName + ".mp4");
                }
                if(File.Exists("../../../VidsHolder/" + VD.VideoName + ".wav"))
                {
                    File.Delete("../../../VidsHolder/" + VD.VideoName + ".wav");
                }
                if(File.Exists("../../../VidsHolder/" + VD.ImgStorageName))
                {
                    File.Delete("../../../VidsHolder/" + VD.ImgStorageName);
                }
            }

            _db = null;
        }

        private async Task<string> UploadFileAsync(IFormFile file, string fileName, string bucketname)
        {
            var mStream = new MemoryStream();
            await file.CopyToAsync(mStream);
            var dataObject = await storageClient.UploadObjectAsync(bucketname, fileName, null, mStream);
            return dataObject.MediaLink;
        }

        private void WriteLogEntry(string logId, string message)
        {
            var client = LoggingServiceV2Client.Create();
            LogName logName = new LogName("projectforpftc", logId);
            LogEntry logEntry = new LogEntry
            {
                LogNameAsLogName = logName,
                Severity = LogSeverity.Info,
                TextPayload = $"{message}"
            };
            MonitoredResource resource = new MonitoredResource { Type = "global" };
            IDictionary<string, string> entryLabels = new Dictionary<string, string>
            {
                { "size", "large" },
                { "color", "red" }
            };
            client.WriteLogEntries(logName, resource, entryLabels,
                new[] { logEntry });
            Console.WriteLine($"Created log entry in log-id: {logId}.");
        }
    }
}