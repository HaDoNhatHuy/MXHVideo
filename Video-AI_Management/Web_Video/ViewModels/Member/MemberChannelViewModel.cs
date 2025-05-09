﻿using System;

namespace Web_Video.ViewModels.Member
{
    public class MemberChannelViewModel
    {
        public Guid ChannelId { get; set; }
        public string Name { get; set; }
        public string About { get; set; }
        public DateTime CreatedAt { get; set; }
        public int NumberOfSubscribers { get; set; }
        public int NumberOfAvailableVideos { get; set; }
        public bool UserIsSubscribed { get; set; }
    }
}
