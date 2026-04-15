using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sum.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public int SenderId { get; set; }

        [ForeignKey("SenderId")]
        public User? Sender { get; set; }

        [Required]
        public int RecipientId { get; set; }

        [ForeignKey("RecipientId")]
        public User? Recipient { get; set; }

        [Required(ErrorMessage = "Předmět je povinný.")]
        [StringLength(200)]
        [Display(Name = "Předmět")]
        public string Subject { get; set; } = string.Empty;

        [Display(Name = "Zpráva")]
        public string? Body { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Original filename of the attachment (null if no attachment).
        /// </summary>
        [StringLength(255)]
        public string? AttachmentFileName { get; set; }

        /// <summary>
        /// Stored filename on disk (GUID-based, null if no attachment).
        /// </summary>
        [StringLength(255)]
        public string? AttachmentStoredName { get; set; }

        /// <summary>
        /// MIME content type of the attachment.
        /// </summary>
        [StringLength(100)]
        public string? AttachmentContentType { get; set; }

        /// <summary>
        /// Size of the attachment in bytes.
        /// </summary>
        public long? AttachmentSize { get; set; }
    }
}
