using Darbak.Models;
using Microsoft.AspNetCore.Identity;

namespace Darbak.Data.Seed
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            using var scope = serviceProvider.CreateScope();

            var roleManager =
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roles = { "Admin", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminEmail = configuration["AdminAccount:Email"];
            var adminPassword = configuration["AdminAccount:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) ||
                string.IsNullOrWhiteSpace(adminPassword))
            {
                return;
            }

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "Darbak Admin"
                };

                var result =
                    await userManager.CreateAsync(admin, adminPassword);

                if (!result.Succeeded)
                {
                    throw new Exception(
                        string.Join(", ",
                            result.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}