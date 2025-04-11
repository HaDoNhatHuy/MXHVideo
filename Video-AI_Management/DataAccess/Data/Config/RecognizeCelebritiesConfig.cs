using Database_Video.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Data.Config
{
    public class RecognizeCelebritiesConfig : IEntityTypeConfiguration<RecognizeCelebrities>
    {
        public void Configure(EntityTypeBuilder<RecognizeCelebrities> builder)
        {
            //defining the primary key which is a combination of both AppUserId and VideoId
            builder.HasKey(x => new { x.CelebrityId, x.VideoId });
            builder.HasOne(a => a.Celebrity).WithMany(c => c.RecognizeCelebrities).HasForeignKey(c => c.CelebrityId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(a => a.Video).WithMany(c => c.RecognizeCelebrities).HasForeignKey(c => c.VideoId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
