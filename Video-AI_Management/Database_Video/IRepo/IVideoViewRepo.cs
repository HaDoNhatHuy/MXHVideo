﻿using Database_Video.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.IRepo
{
    public interface IVideoViewRepo : IBaseRepo<VideoView>
    {
        Task HandleVideoViewAsync(string userId, Guid videoId, string ipAddress);
    }
}
