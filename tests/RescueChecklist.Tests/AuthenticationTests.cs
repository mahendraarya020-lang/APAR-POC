using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace RescueChecklist.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task PrivatePageRedirectsAnonymousVisitorToLogin()
    {
        var prior = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=unused;Username=unused;Password=unused");
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Test"));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            var response = await client.GetAsync("/Equipment");
            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
        }
        finally { Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", prior); }
    }
}
