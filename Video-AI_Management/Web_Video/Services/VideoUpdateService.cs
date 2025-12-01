using DataAccess.Data;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Web_Video.Services
{
    public class VideoUpdateService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public VideoUpdateService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

                        // 1. TÍNH TỔNG VIEW BẰNG SQL THUẦN (Rất nhanh)
                        // Câu lệnh này update toàn bộ bảng Video dựa trên bảng VideoView
                        // Thay vì load vào RAM, ta để SQL Server tự tính
                        await context.Database.ExecuteSqlRawAsync(@"
                        UPDATE Video 
                        SET CachedViews = (
                            SELECT COALESCE(SUM(NumberOfVisit), 0)
                            FROM VideoView 
                            WHERE VideoView.VideoId = Video.Id
                        )
                    ", stoppingToken);

                        // 2. CHUYỂN ĐỔI DURATION SANG GIÂY (Chạy 1 lần cho video mới)
                        // (Bạn có thể viết logic parse string 'mm:ss' ở đây nếu cần, 
                        // nhưng tốt nhất là tính DurationSeconds ngay lúc Upload Video)
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi nếu có
                    Console.WriteLine($"Lỗi Background Update: {ex.Message}");
                }

                // Chờ 5 phút trước khi chạy lại
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
