using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sum.Models
{
    public class Homework
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Název úkolu je povinný.")]
        [StringLength(200)]
        [Display(Name = "Název úkolu")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Popis úkolu")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Předmět je povinný.")]
        [StringLength(100)]
        [Display(Name = "Předmět")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Termín odevzdání je povinný.")]
        [Display(Name = "Termín odevzdání")]
        public DateTime Deadline { get; set; }

        [Required]
        public int TeacherId { get; set; }

        [ForeignKey("TeacherId")]
        public User? Teacher { get; set; }

        [Display(Name = "Datum vytvoření")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
