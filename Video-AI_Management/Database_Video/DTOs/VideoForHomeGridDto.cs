namespace Database_Video.DTOs
{
    public class VideoForHomeGridDto
    {
        public Guid Id { get; set; }
        public string Thumbnail { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Views { get; set; }
        public string ChannelName { get; set; }
        public string Duration { get; set; } // Thêm mới: Trường Duration
        public Guid ChannelId { get; set; }
        public Guid CategoryId { get; set; }
        public string CreatedAtTimeAgo { get; set; } // Thêm mới: Thời gian tương đối

    }
}
