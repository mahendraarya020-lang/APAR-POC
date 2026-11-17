using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RescueChecklist.Web.Data;
using RescueChecklist.Web.Models;

const string expectedHost = "psql-apar-database.postgres.database.azure.com";
const string expectedDatabase = "rescue_checklist";
const string expectedConfirmation = "SEED_APAR_POC";

if (Environment.GetEnvironmentVariable("APAR_SEED_CONFIRM") != expectedConfirmation)
{
    Console.Error.WriteLine("Refusing to seed. Set APAR_SEED_CONFIRM=SEED_APAR_POC explicitly.");
    return 2;
}

var rawConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
var demoPassword = Environment.GetEnvironmentVariable("RESCUE_DEMO_PASSWORD");
if (string.IsNullOrWhiteSpace(rawConnection) || string.IsNullOrWhiteSpace(demoPassword))
{
    Console.Error.WriteLine("Required environment variables are missing: ConnectionStrings__DefaultConnection and RESCUE_DEMO_PASSWORD.");
    return 2;
}

var connection = new NpgsqlConnectionStringBuilder(rawConnection);
if (!string.Equals(connection.Host, expectedHost, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(connection.Database, expectedDatabase, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Refusing to seed: connection host or database does not match APAR POC target.");
    return 2;
}
if (demoPassword.Length < 10)
{
    Console.Error.WriteLine("Demo password must contain at least 10 characters.");
    return 2;
}

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(rawConnection));
services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 10;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
try
{
    await DemoSeed.RunAsync(scope.ServiceProvider);
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    foreach (var username in new[] { "admin", "pic", "rescue", "management" })
    {
        if (await users.FindByNameAsync(username) is null)
            throw new InvalidOperationException($"Expected seeded account missing: {username}");
        Console.WriteLine($"Verified demo user: {username}");
    }
    Console.WriteLine("APAR POC seed completed successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Seeder failed: {ex.GetType().Name}: {ex.Message}");
    return 1;
}
