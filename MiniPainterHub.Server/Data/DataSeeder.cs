using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

            const string adminRole = "Admin";
            const string userRole = "User";
            const string moderatorRole = "Moderator";

            await EnsureRoleExistsAsync(roleManager, adminRole);
            await EnsureRoleExistsAsync(roleManager, userRole);
            await EnsureRoleExistsAsync(roleManager, moderatorRole);

            await EnsureUserAsync(
                userManager,
                userName: "admin",
                email: "admin@local",
                password: "P@ssw0rd!",
                requiredRoles: new[] { adminRole, userRole });

            await EnsureUserAsync(
                userManager,
                userName: "user",
                email: "user@local",
                password: "User123!",
                requiredRoles: new[] { userRole });
        }

        public static async Task SeedAdminAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1) Hard off by default — you flip this on in Azure App Settings (or Key Vault-backed config)
            if (!cfg.GetValue<bool>("SeedAdmin:Enabled")) return;

            // 2) If any user is already in Admin, bail (prevents re-runs without extra tables)
            var adminRole = cfg["SeedAdmin:Role"] ?? "Admin";
            if (await roles.RoleExistsAsync(adminRole))
            {
                // quick check: if the role has at least one user, we're done
                var anyAdmin = (await users.GetUsersInRoleAsync(adminRole)).Any();
                if (anyAdmin) return;
            }

            // 3) Read email/password from configuration (never hard-code)
            var email = cfg["SeedAdmin:Email"];
            var pwd = cfg["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pwd))
                return; // missing config → skip safely

            // 4) Ensure role exists
            if (!await roles.RoleExistsAsync(adminRole))
                await roles.CreateAsync(new IdentityRole(adminRole));

            // 5) Ensure user exists
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };
                var create = await users.CreateAsync(user, pwd);
                if (!create.Succeeded)
                    throw new Exception("Admin seed failed: " + string.Join("; ", create.Errors.Select(e => e.Description)));
            }

            // 6) Ensure role assignment
            if (!await users.IsInRoleAsync(user, adminRole))
                await users.AddToRoleAsync(user, adminRole);
        }

        private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                return;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create role '{roleName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }
        }

        private static async Task EnsureUserAsync(
            UserManager<ApplicationUser> userManager,
            string userName,
            string email,
            string password,
            IReadOnlyCollection<string> requiredRoles)
        {
            var user = await userManager.FindByNameAsync(userName) ?? await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true
                };

                var create = await userManager.CreateAsync(user, password);
                if (!create.Succeeded)
                {
                    throw new Exception($"Seed user '{userName}' failed: {string.Join("; ", create.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                var requiresUpdate = false;
                if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    user.Email = email;
                    user.NormalizedEmail = userManager.NormalizeEmail(email);
                    requiresUpdate = true;
                }

                if (!string.Equals(user.UserName, userName, StringComparison.Ordinal))
                {
                    user.UserName = userName;
                    user.NormalizedUserName = userManager.NormalizeName(userName);
                    requiresUpdate = true;
                }

                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    requiresUpdate = true;
                }

                if (requiresUpdate)
                {
                    var update = await userManager.UpdateAsync(user);
                    if (!update.Succeeded)
                    {
                        throw new Exception($"Seed user '{userName}' update failed: {string.Join("; ", update.Errors.Select(e => e.Description))}");
                    }
                }

                var hasExpectedPassword = await userManager.CheckPasswordAsync(user, password);
                if (!hasExpectedPassword)
                {
                    var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                    var reset = await userManager.ResetPasswordAsync(user, resetToken, password);
                    if (!reset.Succeeded)
                    {
                        throw new Exception($"Seed user '{userName}' password reset failed: {string.Join("; ", reset.Errors.Select(e => e.Description))}");
                    }
                }
            }

            foreach (var role in requiredRoles)
            {
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    var addRole = await userManager.AddToRoleAsync(user, role);
                    if (!addRole.Succeeded)
                    {
                        throw new Exception($"Seed user '{userName}' role assignment failed for '{role}': {string.Join("; ", addRole.Errors.Select(e => e.Description))}");
                    }
                }
            }
        }
    }
}
