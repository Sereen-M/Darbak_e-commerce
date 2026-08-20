using Darbak.Models.Enums;

namespace Darbak.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalProducts { get; set; }

        public int ActiveProducts { get; set; }

        public int TotalCategories { get; set; }

        public int TotalUsers { get; set; }

        public int TotalOrders { get; set; }

        public decimal TotalOrderValue { get; set; }

        public int PendingReviews { get; set; }

        public int PendingTestimonials { get; set; }

        public List<DashboardOrderViewModel> RecentOrders { get; set; }
            = new();

        public List<LowStockProductViewModel> LowStockProducts { get; set; }
            = new();

        public Dictionary<OrderStatus, int> OrdersByStatus { get; set; }
            = new();

        public Dictionary<PaymentStatus, int> OrdersByPaymentStatus { get; set; }
            = new();
    }

    public class DashboardOrderViewModel
    {
        public int Id { get; set; }

        public string UserName { get; set; } = null!;

        public DateTime OrderDate { get; set; }

        public decimal TotalAmount { get; set; }

        public OrderStatus Status { get; set; }

        public PaymentStatus PaymentStatus { get; set; }
    }

    public class LowStockProductViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public int StockQuantity { get; set; }

        public decimal Price { get; set; }
    }
}