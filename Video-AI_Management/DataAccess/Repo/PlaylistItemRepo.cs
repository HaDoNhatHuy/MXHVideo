using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Repo
{
    // Không kế thừa BaseRepo<T> vì khóa chính kết hợp
    public class PlaylistItemRepo : IPlaylistItemRepo
    {
        private readonly DataContext _context;

        public PlaylistItemRepo(DataContext context)
        {
            _context = context;
        }

        public void Add(PlaylistItem entity)
        {
            _context.PlaylistItems.Add(entity);
        }

        public void Remove(PlaylistItem entity)
        {
            _context.PlaylistItems.Remove(entity);
        }

        public async Task<PlaylistItem> GetByKeysAsync(Guid playlistId, Guid videoId)
        {
            return await _context.PlaylistItems
                .Where(x => x.PlaylistId == playlistId && x.VideoId == videoId)
                .FirstOrDefaultAsync();
        }
    }
}