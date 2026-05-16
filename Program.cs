using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Repositories;
using KerzelPay.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor Pages (Razor Pages needed for the Identity UI scaffolded pages)
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// EF Core + SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository Pattern — register the generic repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<KerzelPay.Services.StripeService>();

// ASP.NET Core Identity with our custom ApplicationUser + Roles
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Stripe configuration — keys come from User Secrets (safe, never in Git)
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();   // <-- must come BEFORE UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();       // <-- needed for the Identity Razor pages (Login, Register, etc.)

// --- Seed roles, users, and currencies on startup ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await RoleSeeder.SeedRolesAsync(services);
    await UserSeeder.SeedUsersAsync(services);

    var context = services.GetRequiredService<ApplicationDbContext>();
    CurrencySeeder.SeedCurrencies(context);
}

app.Run();