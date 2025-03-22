using DataAccess.Data;
using Database_Video.IRepo;
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

        public UnitOfWork(DataContext context)
        {
            _context = context;
        }

        public IChannelRepo ChannelRepo => new ChannelRepo(_context);
        public ICategoryRepo CategoryRepo => new CategoryRepo(_context);
        public IVideoRepo VideoRepo => new VideoRepo(_context);

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
