using Database_Video.Entities;
using System.Linq.Expressions;

namespace Database_Video.IRepo
{
    public interface IBaseRepo<T> where T : BaseEntity
    {
        void Add(T entity);
        void Update(T source, T destination);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        Task<bool> AnyAsync(Expression<Func<T, bool>> criteria);
        Task<T> GetByIdAsync(Guid id, string includeProperties = null);
        Task<T> GetFirstOrDefaultAsync(Expression<Func<T, bool>> criteria, string includeProperties = null);
        Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> criteria = null, string includeProperties = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderedBy = null);
        Task<int> CountAsync(Expression<Func<T, bool>> criteria = null);
    }
}
