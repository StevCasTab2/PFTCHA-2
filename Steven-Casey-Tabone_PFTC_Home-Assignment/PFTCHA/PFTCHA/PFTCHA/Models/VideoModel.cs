using PFTCHA.Validations;

namespace PFTCHA.Models
{
    public class VideoModel
    {
        public string VideoName { get; set; }

        [MaxFileSize(5 * 1024 * 1024)]
        [AllowedExtensions(new string[] {".png",".jpg"})]
        public IFormFile Thumbnail { get; set; }

        [MaxFileSize(20 * 1024 * 1024)]
        [AllowedExtensions(new string[] { ".wav", ".mp4" })]
        public IFormFile VideoFile { get; set; }

        public string owner { get; set; }
    }
}
