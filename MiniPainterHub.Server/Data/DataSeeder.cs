using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniPainterHub.Server.Identity;
using System;
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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Define roles
            var adminRole = "Admin";
            var userRole = "User";
            var moderatorRole = "Moderator";

            if (!await roleManager.RoleExistsAsync(adminRole))
                await roleManager.CreateAsync(new IdentityRole(adminRole));
            if (!await roleManager.RoleExistsAsync(userRole))
                await roleManager.CreateAsync(new IdentityRole(userRole));
            if (!await roleManager.RoleExistsAsync(moderatorRole))
                await roleManager.CreateAsync(new IdentityRole(moderatorRole));

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



            await SeedSystemDefaultsAsync(db);

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



        public static async Task SeedSystemDefaultsAsync(AppDbContext db)
        {
            var defaults = new (string Key, string Value)[]
            {
                ("SiteOnline", "true"),
                ("RegistrationEnabled", "true"),
                ("LoginEnabled", "true"),
                ("PostingEnabled", "true"),
                ("ImageUploadEnabled", "true"),
                ("RetentionDays", "30")
            };

            foreach (var (key, value) in defaults)
            {
                if (!db.AppSettings.Any(x => x.Key == key))
                {
                    db.AppSettings.Add(new Entities.AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
                }
            }

            if (!db.FeedPolicies.Any())
            {
                db.FeedPolicies.Add(new Entities.FeedPolicy
                {
                    Name = "Default",
                    WRecency = 1.0,
                    WLikes = 1.5,
                    WComments = 1.2,
                    WReportsPenalty = 1.0,
                    HalfLifeHours = 24,
                    DiversityByAuthor = true,
                    MaxPerAuthorPerPage = 2,
                    ExcludeNSFW = false,
                    IsActive = true,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();
        }

        public static async Task SeedAdminAsync(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SeedSystemDefaultsAsync(db);
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
    }
}
