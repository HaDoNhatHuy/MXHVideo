﻿using Microsoft.AspNetCore.Identity;

namespace Database_Video.Entities
{
    public class AppUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? Avatar { get; set; }
        public DateTime? JoinDate { get; set; } = DateTime.UtcNow;
        public Channel? Channel { get; set; }
        public ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();
        public ICollection<Subscribe> Subscriptions { get; set; } = new HashSet<Subscribe>();
        public ICollection<LikeDislike> LikeDislikes { get; set;} = new HashSet<LikeDislike>();
        public ICollection<VideoView> Histories { get; set;} = new HashSet<VideoView>();
    }
}
