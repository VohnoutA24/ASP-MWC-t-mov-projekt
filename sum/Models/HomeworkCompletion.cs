using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sum.Models
{
    public class HomeworkCompletion
    {
        public int Id { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public User? Student { get; set; }

        [Required]
        public int HomeworkId { get; set; }

        [ForeignKey("HomeworkId")]
        public Homework? Homework { get; set; }

        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }
}
