using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace sum.Models
{
    public class ComposeMessageViewModel
    {
        [Required(ErrorMessage = "Vyberte příjemce.")]
        [Display(Name = "Příjemce")]
        public int RecipientId { get; set; }

        [Required(ErrorMessage = "Předmět je povinný.")]
        [StringLength(200, ErrorMessage = "Předmět může mít maximálně 200 znaků.")]
        [Display(Name = "Předmět")]
        public string Subject { get; set; } = string.Empty;

        [Display(Name = "Zpráva")]
        public string? Body { get; set; }

        [Display(Name = "Příloha (max 25 MB)")]
        public IFormFile? Attachment { get; set; }
    }
}
