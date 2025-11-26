using DataAccess.Data;
using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using WebVideo.Utility;

namespace DataAccess.Repo
{
    public class VideoViewRepo : BaseRepo<VideoView>, IVideoViewRepo
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly DataContext _context;
        public VideoViewRepo(DataContext context, IConfiguration config) : base(context)
        {
            _config = config;
            _context = context;
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri("https://api.ip2location.io")
            };
        }
        public async Task HandleVideoViewAsync(string userId, Guid videoId, string ipAddress, string referer)
        {
            // Lấy tất cả VideoView của user với video này
            var existingViews = await _context.VideoViews
                .Where(x => x.AppUserId == userId && x.VideoId == videoId)
                .ToListAsync();

            DateTime now = DateTime.UtcNow;

            if (!existingViews.Any())
            {
                // Chưa có lịch sử → Tạo mới
                await AddVideoViewAsync(userId, videoId, ipAddress, referer);
            }
            else
            {
                // Đã có lịch sử → Chỉ giữ 1 entry duy nhất cho mỗi video
                var latestView = existingViews.OrderByDescending(x => x.LastVisit).First();

                // Xóa các entry cũ (nếu có nhiều hơn 1)
                if (existingViews.Count > 1)
                {
                    var oldEntries = existingViews.Where(x => x.Id != latestView.Id).ToList();
                    _context.VideoViews.RemoveRange(oldEntries);
                }

                // Cập nhật entry hiện tại
                latestView.LastVisit = now;
                latestView.IpAddress = ipAddress;
                latestView.NumberOfVisit++; // Tăng số lần xem
                // CẬP NHẬT REFERER CHO LƯỢT TRUY CẬP MỚI
                latestView.RefererUrl = referer;

                await _context.SaveChangesAsync();
            }
        }

        #region Private Methods
        private async Task AddVideoViewAsync(string userId, Guid videoId, string ipAddress, string referer)
        {
            var ip2LocationResult = await GetIP2LocationResultAsync(ipAddress);
            var videoViewToAdd = new VideoView
            {
                Id = Guid.NewGuid(),
                AppUserId = userId,
                VideoId = videoId,
                IpAddress = ipAddress,
                Country = ip2LocationResult.Country_Name,
                City = ip2LocationResult.City_Name,
                PostalCode = ip2LocationResult.Zip_Code,
                Is_Proxy = ip2LocationResult.Is_Proxy,
                ProgressSeconds = 0,
                LastVisit = DateTime.UtcNow,
                NumberOfVisit = 1,
                // LƯU REFERER VÀO DB
                RefererUrl = referer
            };
            _context.VideoViews.Add(videoViewToAdd);
        }
        private async Task<IP2LocationResultDto> GetIP2LocationResultAsync(string ipAddress)
        {
            try
            {
                if (SD.LocalIpAddress.Contains(ipAddress))
                {
                    return new IP2LocationResultDto();
                }
                else
                {
                    var result = await _httpClient.GetFromJsonAsync<IP2LocationResultDto>($"?Key={_config["IP2LocationAPIKey"]}&ip={ipAddress}&format=json");
                    return result;
                }
            }
            catch (Exception)
            {
                return new IP2LocationResultDto();
            }
        }
        #endregion
    }
}
