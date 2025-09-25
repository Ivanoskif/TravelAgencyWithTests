using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TravelAgency.Web;

namespace TravelAgency.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestingAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestingAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestingAuthHandler>(
                TestingAuthHandler.SchemeName, _ => { });
        });
    }
}
