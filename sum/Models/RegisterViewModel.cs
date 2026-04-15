using System.ComponentModel.DataAnnotations;

namespace sum.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Uživatelské jméno je povinné.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Uživatelské jméno musí mít 3–50 znaků.")]
        [Display(Name = "Uživatelské jméno")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-mail je povinný.")]
        [EmailAddress(ErrorMessage = "Neplatný formát e-mailu.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Celé jméno")]
        [StringLength(100)]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "Heslo je povinné.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Heslo musí mít alespoň 6 znaků.")]
        [DataType(DataType.Password)]
        [Display(Name = "Heslo")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Potvrzení hesla je povinné.")]
        [DataType(DataType.Password)]
        [Display(Name = "Potvrzení hesla")]
        [Compare("Password", ErrorMessage = "Hesla se neshodují.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
