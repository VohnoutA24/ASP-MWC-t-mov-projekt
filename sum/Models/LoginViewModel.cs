using System.ComponentModel.DataAnnotations;

namespace sum.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Uživatelské jméno je povinné.")]
        [Display(Name = "Uživatelské jméno")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Heslo je povinné.")]
        [DataType(DataType.Password)]
        [Display(Name = "Heslo")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Zapamatovat si mě")]
        public bool RememberMe { get; set; }
    }
}
