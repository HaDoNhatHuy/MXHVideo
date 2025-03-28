﻿using Database_Video.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.IRepo
{
    public interface IChannelRepo : IBaseRepo<Channel>
    {
        Task<Guid> GetChannelIdByUserId(string userId);
    }
}
