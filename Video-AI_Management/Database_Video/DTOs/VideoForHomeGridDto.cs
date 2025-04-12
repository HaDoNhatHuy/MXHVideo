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
        public Guid ChannelId { get; set; }
        public Guid CategoryId { get; set; }
    }
}
