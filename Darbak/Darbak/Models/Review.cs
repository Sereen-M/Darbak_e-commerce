using System.ComponentModel.DataAnnotations;
using Darbak.Models.Enums;



namespace Darbak.Models
{
    public class Review
    {
        public int Id { get; set; }

        [Required]
        [StringLength(1000)]
        public string Comment { get; set; } = null!;

        [Range(1, 5)]
        public int Rating { get; set; }

        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;
    }
}
