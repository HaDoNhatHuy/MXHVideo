using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.IRepo
{
    public interface IUnitOfWork : IDisposable
    {
        IChannelRepo ChannelRepo { get; }
        ICategoryRepo CategoryRepo { get; }
        IVideoRepo VideoRepo { get; }
        IVideoFileRepo VideoFileRepo { get; }
        ICommentRepo CommentRepo { get; }
        IVideoViewRepo VideoViewRepo { get; }
        ICelebrityRepo CelebrityRepo { get; }
        Task<bool> CompleteAsync();
    }
}
