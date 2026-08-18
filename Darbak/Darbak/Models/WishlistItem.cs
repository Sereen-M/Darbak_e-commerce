using Microsoft.CodeAnalysis;

namespace Darbak.Models
{
    public class WishlistItem
    {
        public int Id { get; set; }

        public string UserId { get; set; } = null!;

        public ApplicationUser User { get; set; } = null!;

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
