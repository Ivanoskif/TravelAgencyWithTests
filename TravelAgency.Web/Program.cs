using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Repository.Data;
using TravelAgency.Repository.Interface;
using TravelAgency.Repository.Implementation;
using TravelAgency.Service.Interface;
using TravelAgency.Service.Implementation;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
           ?? "Data Source=travel_agency.db";

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(conn));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(o =>
{
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IDestinationService, DestinationService>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IBookingService, BookingService>();

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient<IExternalCountryService, ExternalCountryService>(c =>
    c.BaseAddress = new Uri("https://restcountries.com/v3.1/"));
builder.Services.AddHttpClient<IPublicHolidayService, PublicHolidayService>(c =>
    c.BaseAddress = new Uri("https://date.nager.at/api/v3/"));
builder.Services.AddHttpClient<IWeatherService, WeatherService>();
//  https://api.open-meteo.com/v1/forecast
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(c =>
    c.BaseAddress = new Uri("https://api.frankfurter.app/"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromMinutes(30);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
    opt.Cookie.Name = ".TravelAgency.Session";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Packages}/{action=Index}/{id?}"
).WithStaticAssets();

app.MapRazorPages(); // Identity UI

await SeedData.EnsureSeedAsync(app);



app.Run();

static class SeedData
{
    public static async Task EnsureSeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        string[] roles = new[] { "Admin", "Agent", "Customer" };
        foreach (var r in roles)
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole<Guid>(r));

        var email = config["Seed:AdminEmail"] ?? "admin@travel.local";
        var pass = config["Seed:AdminPassword"] ?? "Admin!12345";

        var admin = await userMgr.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var create = await userMgr.CreateAsync(admin, pass);
            if (create.Succeeded)
                await userMgr.AddToRoleAsync(admin, "Admin");
        }
    }
}

public partial class Program { }
