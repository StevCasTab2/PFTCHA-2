using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Storage.V1;
using PFTCHA.Models;
using System.ComponentModel;
using System.Diagnostics;
using static Google.Cloud.Firestore.V1.StructuredQuery.Types;

namespace PFTCHA.Services
{
    public class FirestoreRepository
    {
        private FirestoreDb _db;
        private readonly GoogleCredential googleCredential;
        private readonly StorageClient storageClient;
        private readonly string bucketName;
        private readonly string _projectId;

        public FirestoreRepository(IConfiguration config)
        {
            _projectId = config.GetValue<string>("Authentication:Google:ProjectId");
            _db = FirestoreDb.Create(config.GetValue<string>("Authentication:Google:ProjectId"));
            googleCredential = GoogleCredential.FromFile(config.GetValue<string>("Authentication:Google:Credentials"));
            storageClient = StorageClient.Create(googleCredential);
            bucketName = config.GetValue<string>("Authentication:Google:CloudStorageBucket");
        }

        public async Task AddVideo(string User, VideoInfoForDatabase p)
        {
            await _db.Collection("Videos").AddAsync(p);
        }

        public async Task DeleteVideo(string User, string id)
        {
            Query VidQuery = _db.Collection("Videos").WhereEqualTo("VidId", id);
            QuerySnapshot VidSnap = await VidQuery.GetSnapshotAsync();
            DocumentReference vidref = _db.Collection("Videos").Document(VidSnap.Documents[0].Id);
            VideoInfoForDatabase VD = VidSnap.Documents[0].ConvertTo<VideoInfoForDatabase>();

            try
            {
                await storageClient.DeleteObjectAsync("unconv_videos", VD.ImgStorageName);
            }
            catch (Exception ex)
            {
                try
                {
                    await storageClient.DeleteObjectAsync("processed_audiofiles", VD.ImgStorageName);
                }
                catch(Exception e)
                {

                }
            }
            try
            {
                await storageClient.DeleteObjectAsync("unconv_videos", VD.VideoStorageName);
            }
            catch(Exception ex)
            {
                try
                {
                    await storageClient.DeleteObjectAsync("processed_audiofiles", VD.VideoStorageName);
                }
                catch(Exception e)
                {

                }
            }

            await vidref.DeleteAsync();
        }

        public async Task<List<VideoInfoForDatabase>> GetVideos(string User)
        {
            List<VideoInfoForDatabase> videos = new List<VideoInfoForDatabase>();
            Query allPostsQuery = _db.Collection("Videos").WhereEqualTo("owner", User);
            QuerySnapshot allPostsQuerySnapshot = await allPostsQuery.GetSnapshotAsync();
            
            foreach (DocumentSnapshot s in allPostsQuerySnapshot.Documents)
            {
                VideoInfoForDatabase p = s.ConvertTo<VideoInfoForDatabase>();
                videos.Add(p);
            }
            return videos;
        }

        public async Task DownloadVid(string User, string VidId)
        {
            Query VidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
            QuerySnapshot VidSnap = await VidQuery.GetSnapshotAsync();
            DocumentReference vidref = _db.Collection(User).Document(VidSnap.Documents[0].Id);
            VideoInfoForDatabase VD = VidSnap.Documents[0].ConvertTo<VideoInfoForDatabase>();


            int index = 0;
            bool found = true;
            if (VD.SRTUrl == null)
            {
                while (found)
                {
                    Debug.WriteLine(@"C:\Users\Public\Downloads\" + VD.VideoName + ".mp4");
                    if(index == 0)
                    {
                        if(!File.Exists(@"C:\Users\Public\Downloads\" + VD.VideoName + ".mp4"))
                        {
                            found = false;
                        }
                        else
                        {
                            Debug.WriteLine("Found file " + index);
                        }
                    }
                    else if(index > 0)
                    {
                        if(!File.Exists(@"C:\Users\Public\Downloads\" + VD.VideoName + index + ".mp4"))
                        {
                            found = false;
                        }
                        else
                        {
                            Debug.WriteLine("Found file " + index);
                        }
                    }

                    if (found)
                    {
                        index++;
                    }
                }

                string fileDownloadPath = "";
                if (index == 0)
                {
                    fileDownloadPath = @"C:\Users\Public\Downloads\" + VD.VideoName + ".mp4";
                }
                else
                {
                    fileDownloadPath = @"C:\Users\Public\Downloads\" + VD.VideoName + index + ".mp4";
                }
                using var outputFile = File.OpenWrite(fileDownloadPath);
                await storageClient.DownloadObjectAsync("unconv_videos", VD.VideoStorageName, outputFile);

                List<ToConvertTime> times = new List<ToConvertTime>();
                Query allPostsQuery = _db.Collection("Videos").Document(VidSnap.Documents[0].Id).Collection("Downloads");
                QuerySnapshot allPostsQuerySnapshot = await allPostsQuery.GetSnapshotAsync();

                foreach (DocumentSnapshot s in allPostsQuerySnapshot.Documents)
                {
                    ToConvertTime p = s.ConvertTo<ToConvertTime>();
                    times.Add(p);
                }

                string temp = (times.Count + 1).ToString();
                await _db.Collection("Videos").Document(VidSnap.Documents[0].Id).Collection("Downloads").Document(temp).CreateAsync(new { Time = DateTime.Now.ToString() });
            }
            else
            {

                while (found)
                {
                    Debug.WriteLine(@"C:\Users\Public\Downloads\" + VD.VideoName + ".srt");
                    if (index == 0)
                    {
                        if (!File.Exists(@"C:\Users\Public\Downloads\" + VD.VideoName + ".srt"))
                        {
                            found = false;
                        }
                        else
                        {
                            Debug.WriteLine("Found file " + index);
                        }
                    }
                    else if (index > 0)
                    {
                        if (!File.Exists(@"C:\Users\Public\Downloads\" + VD.VideoName + index + ".srt"))
                        {
                            found = false;
                        }
                        else
                        {
                            Debug.WriteLine("Found file " + index);
                        }
                    }

                    if (found)
                    {
                        index++;
                    }
                }

                string fileDownloadPath = "";
                if (index == 0)
                {
                    fileDownloadPath = @"C:\Users\Public\Downloads\" + VD.VideoName + ".srt";
                }
                else
                {
                    fileDownloadPath = @"C:\Users\Public\Downloads\" + VD.VideoName + index + ".srt";
                }
                using var outputFile = File.OpenWrite(fileDownloadPath);
                string file = VD.VideoStorageName;
                file = file.Replace(".flac", ".srt");
                await storageClient.DownloadObjectAsync("processed_audiofiles", file, outputFile);
            }
        }

        public async Task UploadTranscription(string VideoId)
        {

            Query VidId = _db.Collection("Videos").WhereEqualTo("VidId", VideoId);
            QuerySnapshot VidSnapshot = await VidId.GetSnapshotAsync();
            DocumentReference vidref = _db.Collection("Videos").Document(VidSnapshot.Documents[0].Id);
            VideoInfoForDatabase VD = VidSnapshot.Documents[0].ConvertTo<VideoInfoForDatabase>();

            if (VD.Status == "NotTranscribed")
            {

                vidref.UpdateAsync("Status", "Transcribing");
                TopicName topicName = new TopicName(_projectId, "Transcription");
                PublisherClient publisher = await PublisherClient.CreateAsync(topicName);
                string messageId = await publisher.PublishAsync(VD.VidId);

                await publisher.ShutdownAsync(TimeSpan.FromSeconds(15));
            }
            
        }

        public async Task<VideoInfoForDatabase> GetVideoModel(string VidId)
        {

            Query VidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
            QuerySnapshot VidSnapshot = await VidQuery.GetSnapshotAsync();
            VideoInfoForDatabase VD = VidSnapshot.Documents[0].ConvertTo<VideoInfoForDatabase>();

            return VD;
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

[FirestoreData]
public class ToConvertTime
{
    [FirestoreProperty]
    public string Temporary { get; set; }
}