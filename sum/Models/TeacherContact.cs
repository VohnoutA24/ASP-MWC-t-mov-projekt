using System.ComponentModel.DataAnnotations;

namespace sum.Models
{
    public class TeacherContact
    {
        public int Id { get; set; }

        [Display(Name = "Jméno")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Display(Name = "Kabinet/Místnost")]
        public string? Office { get; set; }

        [Display(Name = "Specializace")]
        public string? Specialization { get; set; }

        [Display(Name = "Předměty")]
        public List<string> Subjects { get; set; } = new List<string>();

        [Display(Name = "Dostupnost")]
        public string? Availability { get; set; }

        [Display(Name = "Fotografie")]
        public byte[]? Photo { get; set; }

        [Display(Name = "Popis")]
        public string? Description { get; set; }

        [Display(Name = "Konsultační hodiny")]
        public string? ConsultationHours { get; set; }
    }
}
