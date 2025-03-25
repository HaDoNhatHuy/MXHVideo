using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repo
{
    public class VideoRepo : BaseRepo<Video>, IVideoRepo
    {
        private readonly DataContext _context;
        public VideoRepo(DataContext context) : base(context)
        {
            _context = context;
        }

        public async Task<string> GetUserIdByVideoId(Guid videoId)
        {
            return await _context.Videos
                .Where(x => x.Id == videoId)
                .Select(x => x.Channel.AppUserId)
                .FirstOrDefaultAsync();
        }
    }
}
