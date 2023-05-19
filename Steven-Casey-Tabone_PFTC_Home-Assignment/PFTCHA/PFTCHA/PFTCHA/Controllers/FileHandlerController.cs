using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PFTCHA.Models;
using PFTCHA.Services;
using PFTCHA.Views.Shared;
using System;
using System.Collections;
using System.IO.Compression;

namespace PFTCHA.Controllers
{
    public class FileHandlerController : Controller
    {
        private readonly GoogleCloudStorage _cloudstorage;
        private FirestoreRepository rep;
        private readonly UrlSigner _urlSigner;
        private readonly string bucketName;
        private readonly string projectid;
        public FileHandlerController(GoogleCloudStorage cloudstorage, FirestoreRepository repo, IConfiguration config)
        {
            _cloudstorage = cloudstorage;
            rep = repo;
            _urlSigner = UrlSigner.FromCredentialFile(config.GetValue<string>("Authentication:Google:Credentials"));
            bucketName = config.GetValue<string>("Authentication:Google:CloudStorageBucket");
            projectid = config.GetValue<string>("Authentication:Google:projectid");

        }

        [Authorize]
        public IActionResult Index()
        {
            Task<List<VideoInfoForDatabase>> vids = rep.GetVideos(User.Identity.Name);
            var list = vids.Result;
            foreach(VideoInfoForDatabase v in list)
            {
                string url = "";
                if (v.FlacUrl != null || v.TranscriptionString != null)
                {
                    url = _urlSigner.Sign("processed_audiofiles2", v.ImgStorageName, TimeSpan.FromHours(1), HttpMethod.Get);
                }
                else
                {
                    url = _urlSigner.Sign("unconv_videos2", v.ImgStorageName, TimeSpan.FromHours(1), HttpMethod.Get);
                }

                v.ThumbnailUrl = url;
            }
            return View(list);
        }



        [Authorize]
        public IActionResult UploadVideo()
        {
            return View();
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("VideoName,Thumbnail,VideoFile,owner")] VideoModel video)
        {
            if (ModelState.IsValid)
            {
                if(video.VideoFile != null)
                {
                    var fileExtension = Path.GetExtension(video.VideoFile.FileName);
                    if (fileExtension.ToLower() == ".mp4")
                    {
                        TopicName topicName = new TopicName(projectid, "UploadFile");
                        PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

                        string messageId = await publisher.PublishAsync(User.Identity.Name + " - " + video.VideoName);

                        await publisher.ShutdownAsync(TimeSpan.FromSeconds(15));

                        await UploadFile(User.Identity.Name, video);
                        return RedirectToAction("Index");
                    }
                }
            }

            return null;
        }

        [Authorize]
        private async Task UploadFile(string User, VideoModel video)
        {
            Guid t = Guid.NewGuid();
            VideoInfoForDatabase newvid = new VideoInfoForDatabase();
            newvid.VidId = t.ToString();
            string filename = FormFileName(video.VideoName, video.VideoFile.FileName);
            newvid.VideoUrl = await _cloudstorage.UploadFileAsync(video.VideoFile, filename);
            string imgname = FormFileName(video.VideoName, video.Thumbnail.FileName);
            newvid.ThumbnailUrl = await _cloudstorage.UploadFileAsync(video.Thumbnail, imgname);

            newvid.ImgStorageName = imgname.Replace(";","");
            newvid.VideoName = video.VideoName.Replace(";","");
            newvid.VideoStorageName = filename;
            newvid.DateTimeUploaded = DateTime.Now.ToString();
            newvid.owner = video.owner;
            newvid.Status = "NotTranscribed";
            await rep.AddVideo(User, newvid);
        }

        private static string FormFileName(string title, string filename)
        {
            var fileExtension = Path.GetExtension(filename);
            var name = $"{title}-{DateTime.Now.ToString("yyyyMMddHHmmss")}{fileExtension}";
            return name;
        }

        /*[HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string User, VideoInfoForDatabase VD)
        {
            await rep.DeleteVideo(User, VD.VidId);

            return RedirectToAction("Index");
        }*/

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteVideo(IFormCollection form)
        {
            string User = form["User"];
            string VidId = form["VidId"];
            string VideoName = form["VidName"];

            TopicName topicName = new TopicName(projectid, "DeleteVideo");
            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

            string messageId = await publisher.PublishAsync(User + " - " + VideoName);

            await publisher.ShutdownAsync(TimeSpan.FromSeconds(15));

            await rep.DeleteVideo(User, VidId);

            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> DownloadVideo(string User, string VidId)
        {
            await rep.DownloadVid(User, VidId);

            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> UploadForTranscription(string VidId)
        {
            await rep.UploadTranscription(VidId);

            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> HttpCall(string VidId)
        {
            HttpClient http = new HttpClient();
            VideoInfoForDatabase VD = await rep.GetVideoModel(VidId);

            var content = new StringContent(JsonConvert.SerializeObject(VD), System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await http.PatchAsync("http://127.0.0.1:8080", content);

            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> GenerateSRT(string VidId)
        {
            HttpClient http = new HttpClient();
            VideoInfoForDatabase VD = await rep.GetVideoModel(VidId);

            var content = new StringContent(JsonConvert.SerializeObject(VD), System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await http.PatchAsync("http://127.0.0.1:8080", content);

            return RedirectToAction("Index");
        }


    }
}
