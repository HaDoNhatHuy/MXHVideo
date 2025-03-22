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
    public class VideoRepo : BaseRepo<Video>, IVideoRepo
    {
        public VideoRepo(DataContext context) : base(context)
        {

        }
    }
}
