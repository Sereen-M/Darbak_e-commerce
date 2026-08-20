namespace Darbak.ViewModels
{
    public class AdminUsersViewModel
    {
        public List<AdminUserListItemViewModel> Users { get; set; }
            = new();
    }

    public class AdminUserListItemViewModel
    {
        public string Id { get; set; } = null!;

        public string? FullName { get; set; }

        public string Email { get; set; } = null!;

        public List<string> Roles { get; set; }
            = new();
    }

    public class AdminUserDetailsViewModel
    {
        public string Id { get; set; } = null!;

        public string? FullName { get; set; }

        public string Email { get; set; } = null!;

        public string? UserName { get; set; }

        public List<string> Roles { get; set; }
            = new();

        public string SelectedRole { get; set; } = null!;
    }
}