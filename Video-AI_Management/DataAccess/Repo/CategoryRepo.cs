using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;

namespace DataAccess.Repo
{
    public class CategoryRepo : BaseRepo<Category>, ICategoryRepo
    {
        public CategoryRepo(DataContext context) : base(context)
        {

        }
    }
}
