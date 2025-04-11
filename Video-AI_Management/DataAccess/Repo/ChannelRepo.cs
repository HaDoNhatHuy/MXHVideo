using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repo
{
    public class ChannelRepo : BaseRepo<Channel>, IChannelRepo
    {
        private readonly DataContext _context;
        public ChannelRepo(DataContext context) : base(context)
        {
            _context = context;
        }
        public async Task<Guid> GetChannelIdByUserId(string userId)
        {
            return await _context.Channels.Where(x => x.AppUserId == userId).Select(x => x.Id).FirstOrDefaultAsync();
        }
    }
}
