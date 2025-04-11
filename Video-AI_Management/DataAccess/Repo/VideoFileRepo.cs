using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;

namespace DataAccess.Repo
{
    public class VideoFileRepo : BaseRepo<VideoFile>, IVideoFileRepo
    {
        public VideoFileRepo(DataContext context) : base(context)
        {

        }
    }
}
