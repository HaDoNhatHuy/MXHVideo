using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;

namespace DataAccess.Repo
{
    public class PlaylistRepo : BaseRepo<Playlist>, IPlaylistRepo
    {
        public PlaylistRepo(DataContext context) : base(context)
        {
        }
    }
}