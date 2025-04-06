using DataAccess.Data.Config;
using Database_Video.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Data
{
    public class DataContext : IdentityDbContext<AppUser, AppRole, string>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<VideoView> VideoViews { get; set; }
        public DbSet<Subscribe> Subscribes { get; set; }
        public DbSet<LikeDislike> LikeDislikes { get; set; }
        public DbSet<Comment> Comments { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //shorter way of applying the manual configuarion
            // $ builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            //longer way of applying the manual configuarion
            builder.ApplyConfiguration(new CommentConfig());
            builder.ApplyConfiguration(new SubscribeConfig());
            builder.ApplyConfiguration(new LikeDislikeConfig());
            builder.ApplyConfiguration(new VideoViewConfig());
        }
    }
}
