using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Server.Identity;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace MiniPainterHub.Server.Data
{
    /// <summary>
    /// Encapsulates database seeding logic for default users and roles.
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Define roles
            var adminRole = "Admin";
            var userRole = "User";

            if (!await roleManager.RoleExistsAsync(adminRole))
                await roleManager.CreateAsync(new IdentityRole(adminRole));
            if (!await roleManager.RoleExistsAsync(userRole))
                await roleManager.CreateAsync(new IdentityRole(userRole));

            // Seed admin
            const string adminEmail = "admin@local";
            const string adminPass = "P@ssw0rd!";
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(admin, adminPass);
                await userManager.AddToRolesAsync(admin, new[] { adminRole, userRole });
            }

            // Seed ordinary user
            const string userEmail = "user@local";
            const string userPass = "User123!";
            var user = await userManager.FindByEmailAsync(userEmail);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = "user",
                    Email = userEmail,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(user, userPass);
                await userManager.AddToRoleAsync(user, userRole);
            }
        }
    }
}
