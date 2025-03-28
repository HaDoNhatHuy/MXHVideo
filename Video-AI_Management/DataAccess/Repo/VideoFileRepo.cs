using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Repo
{
    public class VideoFileRepo : BaseRepo<VideoFile>, IVideoFileRepo
    {
        public VideoFileRepo(DataContext context) : base(context)
        {

        }
    }
}
