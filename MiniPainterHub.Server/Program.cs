using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using MiniPainterHub.Server.Data;
using MiniPainterHub.Server.Identity;
using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using MiniPainterHub.Server.Services.Interfaces;
using MiniPainterHub.Server.Services;


namespace MiniPainterHub
{

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlOpts => sqlOpts
                      .MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name)
                      .MigrationsHistoryTable("__EFMigrationsHistory", schema: "dbo")
                )
            );

            builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                // other options...
            }).AddRoles<IdentityRole>()                  // if you need roles
             .AddEntityFrameworkStores<AppDbContext>()
             .AddSignInManager();

            // read JWT settings
            var jwt = builder.Configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwt["Key"]);

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
                        ValidateIssuerSigningKey = true,
                    };
                });

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "MiniPainterHub API", Version = "v1" });

                // 1️⃣ Define the Bearer auth scheme
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter ‘Bearer {token}’"
                });

                // 2️⃣ Require the scheme for all operations (or you can apply it per-controller)
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = new string[] { }
                });
            });

            builder.Services.AddScoped<IProfileService, ProfileService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();


            app.UseAuthentication();

            app.UseAuthorization();

            // 1. Serve the Blazor client’s static assets (js, wasm, dlls under _framework/)
            app.UseBlazorFrameworkFiles();

            // 2. Serve any other static files (wwwroot folder)
            app.UseStaticFiles();


            app.MapControllers();

            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}
