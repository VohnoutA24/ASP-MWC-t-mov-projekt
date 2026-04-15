using System.ComponentModel.DataAnnotations;

namespace sum.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Uživatelské jméno je povinné.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Uživatelské jméno musí mít 3–50 znaků.")]
        [Display(Name = "Uživatelské jméno")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-mail je povinný.")]
        [EmailAddress(ErrorMessage = "Neplatný formát e-mailu.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Display(Name = "Celé jméno")]
        [StringLength(100)]
        public string? FullName { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; } = "Student";

        [Display(Name = "Datum registrace")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
