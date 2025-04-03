using System.Collections.Generic;
using System;

namespace Web_Video.ViewModels.Admin
{
    public class UserDisplayGridViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime JoinDate { get; set; }
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; }
        public bool IsLocked { get; set; }
        public DateTime LockEndsIn { get; set; }
        public IEnumerable<string> AssignedRoles { get; set; }
    }
}
