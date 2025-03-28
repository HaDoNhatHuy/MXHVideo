using DataAccess.Data;
using Database_Video.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Services.IServices;
using WebVideo.Utility;

namespace Web_Video.Seed
{
    public static class ContextInitializer
    {
        public static async Task InitializeAsync(DataContext context,
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IPhotoService photoService)
        {
            if (context.Database.GetPendingMigrations().Count() > 0)
            {
                context.Database.Migrate();
            }
            if (!roleManager.Roles.Any())
            {
                foreach (var role in SD.Roles)
                {
                    await roleManager.CreateAsync(new AppRole { Name = role });
                }
            }
            if (!userManager.Users.Any())
            {
                var admin = new AppUser
                {
                    FullName = "Admin",
                    Email = "admin@example.com",
                    UserName = "admin"
                };
                await userManager.CreateAsync(admin, "password123");
                await userManager.AddToRolesAsync(admin, [SD.AdminRole, SD.UserRole, SD.ModeratorRole]);

                var john = new AppUser
                {
                    FullName = "John",
                    Email = "john@example.com",
                    UserName = "john"
                };
                await userManager.CreateAsync(john, "password123");
                await userManager.AddToRoleAsync(john, SD.UserRole);

                var johnChannel = new Channel
                {
                    ChannelName = "John's Channel",
                    About = "This is John's Channel",
                    AppUserId = john.Id
                };
                context.Channels.Add(johnChannel);

                var peter = new AppUser
                {
                    FullName = "Peter",
                    Email = "peter@example.com",
                    UserName = "peter"
                };
                await userManager.CreateAsync(peter, "password123");
                await userManager.AddToRoleAsync(peter, SD.UserRole);

                var peterChannel = new Channel
                {
                    ChannelName = "Peter's Channel",
                    About = "This is Peter's Channel",
                    AppUserId = peter.Id
                };
                context.Channels.Add(peterChannel);

                var mary = new AppUser
                {
                    FullName = "Mary",
                    Email = "mary@example.com",
                    UserName = "mary"
                };
                await userManager.CreateAsync(mary, "password123");
                await userManager.AddToRoleAsync(mary, SD.ModeratorRole);


                // adding some categories into the database
                var animals = new Category { Id = Guid.Parse("AAB1C154-6338-41B0-981B-C1CC5465D770"), CategoryName = "Animal" };
                var food = new Category { Id = Guid.Parse("32780986-DAFB-4641-9E1F-FEA1257EB057"), CategoryName = "Food" };
                var game = new Category { Id = Guid.Parse("DD488F44-3BCE-4D02-998B-DF48B22B7AED"), CategoryName = "Game" };
                var nature = new Category { Id = Guid.Parse("463ECBAA-14A4-4D72-AAEC-27551A546241"), CategoryName = "Nature" };
                var news = new Category { Id = Guid.Parse("A3B68743-C049-4449-807C-C29531EE78DF"), CategoryName = "News" };
                var sport = new Category { Id = Guid.Parse("B0B863E6-E86C-40A8-9AA1-230122EEFC61"), CategoryName = "Sport" };

                context.Categories.Add(animals);
                context.Categories.Add(food);
                context.Categories.Add(game);
                context.Categories.Add(nature);
                context.Categories.Add(news);
                context.Categories.Add(sport);

                await context.SaveChangesAsync();

                // adding some videos and images into the database
                var imageDirectory = new DirectoryInfo("Seed/Files/Thumbnails");
                var videoDirectory = new DirectoryInfo("Seed/Files/Videos");
                FileInfo[] imageFiles = imageDirectory.GetFiles();
                FileInfo[] videoFiles = videoDirectory.GetFiles();

                var description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed non risus. Suspendisse lectus tortor, dignissim sit amet, adipiscing nec, ultricies sed, dolor. Cras elementum ultrices diam. Maecenas ligula massa, varius a, semper congue, euismod non, mi.";
                for (int i = 0; i < 30; i++)
                {
                    var allNames = videoFiles[i].Name.Split("-");
                    var categoryName = allNames[0];
                    var title = allNames[2].Split(".")[0];
                    var categoryId = await context.Categories.Where(x => x.CategoryName.ToLower() == categoryName).Select(x => x.Id).FirstOrDefaultAsync();

                    IFormFile imageFile = ConvertToFile(imageFiles[i]);
                    IFormFile videoFile = ConvertToFile(videoFiles[i]);

                    var videoToAdd = new Video
                    {
                        Id = Guid.NewGuid(),
                        Title = title,
                        Description = description,
                        CategoryId = categoryId,
                        VideoFile = new VideoFile
                        {
                            Id = Guid.NewGuid(),
                            ContentType = SD.GetContentType(videoFiles[i].Extension),
                            Contents = GetContentsAsync(videoFile).GetAwaiter().GetResult(),
                            Extension = videoFiles[i].Extension,
                        },
                        Thumbnail = photoService.UploadPhotoLocally(imageFile),
                        ChannelId = (i % 2 == 0) ? johnChannel.Id : peterChannel.Id,
                        UploadDate = SD.GetRandomDate(new DateTime(2015, 1, 1), DateTime.Now, i),
                    };
                    context.Videos.Add(videoToAdd);
                }
                await context.SaveChangesAsync();
            }
        }
        #region Private Helper Methods
        private static IFormFile ConvertToFile(FileInfo fileInfo)
        {
            //open the file stream
            var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
            //create the IFormFile instance using the file stream
            IFormFile formFile = new FormFile(
                stream,                                 // File Stream
                0,                                      // Start Position of Stream
                fileInfo.Length,                        // File Length
                fileInfo.Name,                          // Name of the form field
                fileInfo.Name                           // File Name
            );
            return formFile;
        }
        private static async Task<byte[]> GetContentsAsync(IFormFile file)
        {
            byte[] contents;
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            contents = ms.ToArray();
            return contents;
        }
        #endregion
    }
}
