using System.ComponentModel.DataAnnotations;

namespace Web_Video.ViewModels.Account
{
    public class LoginViewModel
    {
        private string _username;
        [Display(Name = "UserName or Email")]
        [Required(ErrorMessage = "User name is required")]
        public string UserName 
        { 
            get => _username; 
            set => _username = !string.IsNullOrEmpty(value) ? value.ToLower() : null; 
        }
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
        public string ReturnUrl { get; set; } 

    }
}
