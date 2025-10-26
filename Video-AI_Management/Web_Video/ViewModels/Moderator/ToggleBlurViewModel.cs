using System;

namespace Web_Video.ViewModels.Moderator
{
    public class ToggleBlurViewModel
    {
        public Guid VideoId { get; set; }
        public bool Activate { get; set; }
        public string CelebrityName { get; set; }
    }
}