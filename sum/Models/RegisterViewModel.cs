using System.ComponentModel.DataAnnotations;

namespace sum.Models
{
    public class RegisterViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "E-mail je povinný.")]
        [EmailAddress(ErrorMessage = "Neplatný formát e-mailu.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Heslo je povinné.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Heslo musí mít alespoň 6 znaků.")]
        [DataType(DataType.Password)]
        [Display(Name = "Heslo")]
        public string Password { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(Email) && !Email.EndsWith("@zschvalk.cz", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    "Registrace je povolena pouze pro školní e-maily (@zschvalk.cz).",
                    new[] { nameof(Email) });
            }
        }
    }
}
