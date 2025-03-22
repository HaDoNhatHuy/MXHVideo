using Database_Video.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Database_Video.IRepo;
using DataAccess.Data;

namespace DataAccess.Repo
{
    public class ChannelRepo : BaseRepo<Channel>, IChannelRepo
    {
        public ChannelRepo(DataContext context) : base(context)
        {

        }
    }
}
