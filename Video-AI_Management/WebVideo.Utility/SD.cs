using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Cryptography;

namespace WebVideo.Utility
{
    public static class SD
    {
        public const string AdminRole = "admin";
        public const string ModeratorRole = "moderator";
        public const string UserRole = "user";
        public static readonly List<string> Roles = new List<string> { AdminRole, ModeratorRole, UserRole };
        public const int MB = 1000000;
        public static string IsActive(this IHtmlHelper html, string controller, string action, string cssClass = "active")
        {
            var routeData = html.ViewContext.RouteData;
            var routeAction = routeData.Values["action"]?.ToString();
            var routeController = routeData.Values["controller"]?.ToString();

            var returnActive = controller == routeController && action == routeAction;
            return returnActive ? cssClass : string.Empty;
        }
        //public static string GetRandomName()
        //{
        //    var randomNumber = new byte[10];
        //    using var rng = RandomNumberGenerator.Create();
        //    rng.GetBytes(randomNumber);

        //    return Convert.ToBase64String(randomNumber);
        //}
        public static int GetRandomNumber(int minValue, int maxValue, int seed)
        {
            Random random = new Random(seed);
            return random.Next(minValue, maxValue);
        }
        public static DateTime GetRandomDate(DateTime minDate, DateTime maxDate, int seed)
        {
            Random random = new Random(seed);
            int range = (maxDate - minDate).Days;
            return minDate.AddDays(random.Next(range + 1));

        }
    }
}
