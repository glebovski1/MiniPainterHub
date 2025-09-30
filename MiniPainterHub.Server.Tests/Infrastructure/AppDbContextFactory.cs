using System;
using Microsoft.EntityFrameworkCore;
using MiniPainterHub.Server.Data;

namespace MiniPainterHub.Server.Tests.Infrastructure;

internal static class AppDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
