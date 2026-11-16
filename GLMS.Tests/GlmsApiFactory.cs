using GLMS.Web.Data;
using GLMS.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GLMS.Tests;

public sealed class GlmsApiFactory : WebApplicationFactory<ClientsController>
{
    private readonly string _databaseName = $"GlmsApiTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(local);Database=GlmsTests;Trusted_Connection=True;");
        builder.UseSetting("Jwt:SigningKey", "integration-test-only-signing-key-must-be-at-least-32-bytes");
        builder.UseSetting("Jwt:Issuer", "GLMS.Api.Tests");
        builder.UseSetting("Jwt:Audience", "GLMS.Tests");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<GlmsDbContext>>();
            services.RemoveAll<DbContextOptions<GlmsDbContext>>();
            services.RemoveAll<GlmsDbContext>();
            services.AddDbContext<GlmsDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
