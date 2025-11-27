using DataAccess.Data;
using Database_Video.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Web_Video.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.AddApplicationServices();
builder.AddAuthenticationServices();

// Thêm CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100_000_000; // 100MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100_000_000; // 100MB
});

// FIX: Tăng giới hạn upload file (ví dụ: 500MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 524288000; // 500MB (tính bằng bytes)
    options.MemoryBufferThreshold = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
// Thêm dòng này trước app.UseRouting() hoặc app.UseEndpoints()
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering(); // Cho phép đọc lại body (cần cho file lớn)
    await next();
});
app.UseRouting();
app.UseCors("AllowAll"); // Thêm CORS middleware

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Gọi hàm Seed Data
//await InitializeContextAsync();

app.Run();

//async Task InitializeContextAsync()
//{
//    using (var scope = app.Services.CreateScope())
//    {
//        var services = scope.ServiceProvider;
//        try
//        {
//            var context = services.GetRequiredService<DataContext>();
//            var userManager = services.GetRequiredService<UserManager<AppUser>>();

//            // --- THÊM DÒNG NÀY: Lấy RoleManager ---
//            var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

//            var env = services.GetRequiredService<IWebHostEnvironment>();

//            // --- CẬP NHẬT DÒNG NÀY: Truyền roleManager vào Constructor ---
//            var seeder = new AdvancedDataSeeder(context, userManager, roleManager, env);

//            await seeder.SeedAllAsync();
//        }
//        catch (Exception ex)
//        {
//            var logger = services.GetRequiredService<ILogger<Program>>();
//            logger.LogError(ex, "An error occurred while seeding the database.");
//        }
//    }
//}