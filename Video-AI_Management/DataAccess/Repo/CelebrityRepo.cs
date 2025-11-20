using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;

namespace DataAccess.Repo
{
    public class CelebrityRepo : BaseRepo<Celebrity>, ICelebrityRepo
    {
        public CelebrityRepo(DataContext context) : base(context)
        {

        }
    }
}
