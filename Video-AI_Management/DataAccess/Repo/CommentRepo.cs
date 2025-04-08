using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;

namespace DataAccess.Repo
{
    public class CommentRepo : BaseRepo<Comment>, ICommentRepo
    {
        public CommentRepo(DataContext context): base(context)
        {
            
        }
    }
}
