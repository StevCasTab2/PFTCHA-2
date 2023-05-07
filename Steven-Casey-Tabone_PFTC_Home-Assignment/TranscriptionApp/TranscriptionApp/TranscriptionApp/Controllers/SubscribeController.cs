using Microsoft.AspNetCore.Mvc;
using Google.Cloud.PubSub.V1;
using Google.Cloud.Speech.V1;
using Google.Cloud.Firestore;
using TranscriptionApp.Models;
using System.Net.NetworkInformation;
using System.Diagnostics;
using Google.Cloud.Storage.V1;
using Google.Apis.Auth.OAuth2;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Diagnostics.AspNetCore3;
using System;
using Google.Cloud.Logging.V2;
using Google.Api;
using Google.Cloud.Logging.Type;

namespace TranscriptionApp.Controllers
{
    public class SubscribeController
    {
        private SubscriberServiceApiClient subscriber;
        private SubscriptionName subscriptionName;
        private Timer timer;
        private Timer timerTrack;
        private FirestoreDb _db;
        private readonly GoogleCredential googleCredential;
        private readonly StorageClient storageClient;
        private int timerint = 0;
        private IExceptionLogger exceptionLogger;

        public SubscribeController(ILogger<SubscribeController> _logger)
        {

            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "theta-solution-377011-94bfb5b80ee9.json");
            _db = FirestoreDb.Create("theta-solution-377011");
            googleCredential = GoogleCredential.FromFile("theta-solution-377011-94bfb5b80ee9.json");
            storageClient = StorageClient.Create(googleCredential);
            // Create a new subscriber client
            subscriber = SubscriberServiceApiClient.Create();
            subscriptionName = new SubscriptionName("theta-solution-377011", "Transcription");
            // Create a timer that triggers every minute
            //subscriber.CreateSubscription(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 60);
        }

        public string Index()
        {
            timer = new Timer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            timerTrack = new Timer(TrackTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            return "Starting Count";
        }

        private void TrackTimer(object state)
        {
            timerint += 1;
            Console.WriteLine(timerint);
            Debug.WriteLine(timerint);

            if(timerint >= 5)
            {
                timerint = 0;
            }
        }
        private async void TimerCallback(object state)
        {
            TopicName topicName = new TopicName("theta-solution-377011", "Transcription");
            bool Check = false;
            ProjectName projectName = ProjectName.FromProject("theta-solution-377011");
            foreach (Subscription s in subscriber.ListSubscriptions(projectName))
            {
                if(s.Name == "projects/theta-solution-377011/subscriptions/Transcription-sub")
                {
                    Check = true;
                }
            }

            if (!Check)
            {
                subscriber.CreateSubscription(subscriptionName, topicName, pushConfig: null, ackDeadlineSeconds: 60);
            }

            PullResponse pullResponse = subscriber.Pull(subscriptionName, maxMessages: 10);

            if (pullResponse.ReceivedMessages.Count > 0)
            {
                string VidIdHolder = "";
           
                try
                {
                    Debug.WriteLine("Message Found");
                    foreach (ReceivedMessage received in pullResponse.ReceivedMessages)
                    {
                        PubsubMessage msg = received.Message;

                        string VidId = msg.Data.ToStringUtf8();
                        VidIdHolder = VidId;
                        /*
                        string[] datasplit = Data.Split(",");
                        string VidName = datasplit[0];
                        string VidOwner = datasplit[1];*/

                        var speech = SpeechClient.Create();
                        var config = new RecognitionConfig
                        {
                            Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
                            SampleRateHertz = 44100,
                            LanguageCode = LanguageCodes.English.UnitedStates,
                            EnableWordConfidence = true,
                            EnableWordTimeOffsets = true
                        };

                        Query vidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
                        QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
                        VideoInfoForDatabase VD = VidVidQuery.Documents[0].ConvertTo<VideoInfoForDatabase>();
                        WriteLogEntry("Transcription","Transcribing" + VD.owner + "'s  Video: " + VD.VideoName);

                        string VidName = VD.VideoStorageName.Replace(".mp4", "");
                        VidName = VidName.Replace(".flac", "");
                        string VideoUrl = "gs://processed_audiofiles/" + VidName + ".flac";

                        var audio = RecognitionAudio.FromStorageUri(VideoUrl);

                        var response = speech.Recognize(config, audio);

                        foreach (var result in response.Results)
                        {
                            foreach (var alternative in result.Alternatives)
                            {
                                List<WordInfo> words = new List<WordInfo>();
                                foreach (WordInfo w in alternative.Words)
                                {
                                    words.Add(w);
                                }
                                SaveTranscription(alternative.Transcript, words, VidId);
                            }
                        }
                        WriteLogEntry("Transcription","Finished Transcribing" + VD.owner + "'s  Video: " + VD.VideoName);
                        await DeleteFileAsync(VidName + ".flac");

                        WriteLogEntry("Transcription","Deleted " + VD.owner + "'s  Video: " + VD.VideoName);
                        subscriber.Acknowledge(subscriptionName, pullResponse.ReceivedMessages.Select(m => m.AckId));
                        WriteLogEntry("Transcription","Transcription finished for " + VD.owner + "'s video: " + VD.VideoName);
                    }
                }
                catch(Exception e)
                {
                    try
                    {
                        Query vidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidIdHolder);
                        QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
                        VideoInfoForDatabase VD = VidVidQuery.Documents[0].ConvertTo<VideoInfoForDatabase>();
                        subscriber.Acknowledge(subscriptionName, pullResponse.ReceivedMessages.Select(m => m.AckId));
                        WriteLogEntry("Transcription","Transcription FAILED for " + VD.owner + "'s video: " + VD.VideoName);
                        WriteLogEntry("Transcription","Purging video data for " + VD.owner + "'s video: " + VD.VideoName);
                        await PurgeRecords(VD);
                    }
                    catch(Exception f)
                    {
                        WriteLogEntry("Transcription","Transcription FAILED. Video Record in Database could not be found");
                        subscriber.Acknowledge(subscriptionName, pullResponse.ReceivedMessages.Select(m => m.AckId));
                    }
                }
            }
            else
            {
                Console.WriteLine("No Messages Found");
                Debug.WriteLine("No Messages Found");
            }
        }


        private async Task<string> GetVideoUrl(string VidId)
        {
            Query vidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
            QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
            
            VideoInfoForDatabase VD = VidVidQuery.Documents[0].ConvertTo<VideoInfoForDatabase>();

            return VD.FlacUrl;
        }

        private async Task SaveTranscription(string Transcriptiontext,List<WordInfo> words, string VidId)
        {
            Query vidQuery = _db.Collection("Videos").WhereEqualTo("VidId", VidId);
            QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
            string Formatted = "";
            float interval = 5;
            int wordinterval = 0;
            int SequenceNumber = 1;
            foreach(WordInfo word in words)
            {
                string wordStart = word.StartTime.ToString();
                wordStart = wordStart.Replace("\\", "");
                wordStart = wordStart.Replace("s", "");
                wordStart = wordStart.Replace("\"", "");
                string wordEnd = word.EndTime.ToString();
                wordEnd = wordEnd.Replace("\\", "");
                wordEnd = wordEnd.Replace("s", "");
                wordEnd = wordEnd.Replace("\"", "");
                float WordStartTime = float.Parse(wordStart);
                float WordEndTime = float.Parse(wordEnd);
                if (WordEndTime <= interval)
                {
                    if (wordinterval == 0)
                    {
                        int finaltimeindex = -99;
                        for(int i = 0; i < words.Count; i++)
                        {
                            string wordEndTemp = words[i].EndTime.ToString();
                            wordEndTemp = wordEndTemp.Replace("\\", "");
                            wordEndTemp = wordEndTemp.Replace("s", "");
                            wordEndTemp = wordEndTemp.Replace("\"", "");
                            float wordEndTempFloat = float.Parse(wordEndTemp);
                            if (wordEndTempFloat > interval)
                            {

                                finaltimeindex = (i-1);
                                break;
                            }

                            if(i == words.Count-1 && finaltimeindex == -99)
                            {
                                finaltimeindex = words.Count - 1;
                                break;
                            }
                        }

                        string tempholderforendtime = words[finaltimeindex].EndTime.ToString();
                        tempholderforendtime = tempholderforendtime.Replace("\\", "");
                        tempholderforendtime = tempholderforendtime.Replace("s", "");
                        tempholderforendtime = tempholderforendtime.Replace("\"", "");

                        double StartTimeDouble = (double)WordStartTime;
                        double EndTimeDouble = Convert.ToDouble(tempholderforendtime);
                        TimeSpan Start = TimeSpan.FromSeconds(StartTimeDouble);
                        TimeSpan End = TimeSpan.FromSeconds(EndTimeDouble);
                        string StartString = Start.ToString();
                        StartString = StartString.Replace(".", ",");
                        string EndString = End.ToString();
                        EndString = EndString.Replace(".", ",");
                        string ModifStartString = StartString.Substring(0, 12);
                        string ModifEndString = EndString.Substring(0, 12);
                        float TempFloatHolder = float.Parse(tempholderforendtime);
                        Formatted += SequenceNumber + "^" + ModifStartString + "~-->~" + ModifEndString + "^" ;
                        Formatted += word.Word;
                    }
                    else
                    {
                        Formatted += "~" + word.Word;
                    }

                    wordinterval += 1;
                }

                if(WordStartTime > interval)
                {
                    Formatted += "^^";
                    interval += 5;
                    wordinterval = 0;
                    SequenceNumber += 1;
                }
            }

            DocumentReference vidref = _db.Collection("Videos").Document(VidVidQuery.Documents[0].Id);
            vidref.UpdateAsync("TranscriptionString", Formatted);
            vidref.UpdateAsync("Status", "DoneTranscribing");
        }

        public async Task DeleteFileAsync(string videoname)
        {
            await storageClient.DeleteObjectAsync("processed_audiofiles", videoname);
        }

        public async Task PurgeRecords(VideoInfoForDatabase vd)
        {
            try
            {
                await storageClient.DeleteObjectAsync("processed_audiofiles", vd.VideoStorageName);
            }
            catch(Exception e)
            {

            }

            try
            {
                await storageClient.DeleteObjectAsync("processed_audiofiles", vd.ImgStorageName);
            }
            catch(Exception e)
            {

            }

            try
            {
                Query vidQuery = _db.Collection("Videos").WhereEqualTo("VidId", vd.VidId);
                QuerySnapshot VidVidQuery = await vidQuery.GetSnapshotAsync();
                DocumentReference vid = _db.Collection("Videos").Document(VidVidQuery.Documents[0].Id);

                await vid.DeleteAsync();
            }
            catch(Exception e)
            {

            }
        }

        private void WriteLogEntry(string logId, string message)
        {
            var client = LoggingServiceV2Client.Create();
            LogName logName = new LogName("theta-solution-377011", logId);
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


