using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;

namespace SRTGenerator.Models
{
    [FirestoreData]
    public class VideoInfoForDatabase
    {

        [Required]
        [FirestoreProperty]
        public string VidId { get; set; }

        [Required]
        [FirestoreProperty]
        public string VideoName { get; set; }

        [Required]
        [FirestoreProperty]
        public string VideoUrl { get; set; }

        [Required]
        [FirestoreProperty]
        public string ThumbnailUrl { get; set; }

        [Required]
        [FirestoreProperty]
        public string VideoStorageName { get; set; }

        [Required]
        [FirestoreProperty]
        public string ImgStorageName { get; set; }

        [Required]
        [FirestoreProperty]
        public string owner { get; set; }

        [Required]
        [FirestoreProperty]
        public string DateTimeUploaded { get; set; }

        [FirestoreProperty]
        public string TranscriptionUrl { get; set; }

        [FirestoreProperty]
        public string FlacUrl { get; set; }

        [FirestoreProperty]
        public string SRTUrl { get; set; }

        [FirestoreProperty]
        public string TranscriptionString { get; set; }

        [FirestoreProperty]
        public string Status { get; set; }
    }
}
