using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Darbak.Models.Enums;

namespace Darbak.Models
{
    public class Order
    {
        public int Id { get; set; }

        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        public DateTime OrderDate { get; set; } =
            DateTime.UtcNow;

        public OrderStatus Status { get; set; } =
            OrderStatus.Processing;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(200)]
        public string ShippingAddress { get; set; } =
            null!;

        [Required]
        [StringLength(100)]
        public string City { get; set; } =
            null!;

        [Required]
        [StringLength(30)]
        public string PhoneNumber { get; set; } =
            null!;

        public PaymentStatus PaymentStatus { get; set; } =
            PaymentStatus.Pending;


        [StringLength(30)]
        public string? PaymentProvider { get; set; }

        [StringLength(100)]
        public string? PaymentReference { get; set; }

        [StringLength(100)]
        public string? PaymentCaptureId { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } =
            new List<OrderItem>();
    }
}