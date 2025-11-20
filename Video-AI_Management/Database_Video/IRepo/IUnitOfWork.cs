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
        IPlaylistRepo PlaylistRepo { get; }
        IPlaylistItemRepo PlaylistItemRepo { get; } // THÊM MỚI
        Task<bool> CompleteAsync();
    }
}
