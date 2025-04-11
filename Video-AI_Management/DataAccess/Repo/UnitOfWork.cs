using DataAccess.Data;
using Database_Video.IRepo;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repo
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;

        public UnitOfWork(DataContext context,IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public IChannelRepo ChannelRepo => new ChannelRepo(_context);
        public ICategoryRepo CategoryRepo => new CategoryRepo(_context);
        public IVideoRepo VideoRepo => new VideoRepo(_context);
        public IVideoFileRepo VideoFileRepo => new VideoFileRepo(_context);
        public ICommentRepo CommentRepo => new CommentRepo(_context);
        public IVideoViewRepo VideoViewRepo => new VideoViewRepo(_context,_config);
        public ICelebrityRepo CelebrityRepo => new CelebrityRepo(_context);

        public async Task<bool> CompleteAsync()
        {
            bool result = false;
            if (_context.ChangeTracker.HasChanges())
            {
                result = await _context.SaveChangesAsync() > 0;
            }
            return result;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
