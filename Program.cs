using KerzelPay.Data;
using KerzelPay.Models;
using KerzelPay.Repositories;
using KerzelPay.Seeders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// MVC + Razor Pages (Razor Pages needed for the Identity UI scaffolded pages)
builder.Services.AddControllersWithViews();
// ---- Swagger (API docs) ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Kerzel Pay API",
        Version = "v1",
        Description = "Public REST API for the Kerzel Pay money transfer platform.",
        Contact = new OpenApiContact
        {
            Name = "Anthony Kerbage & Yorgo Moukarzel",
            Email = "informationcsv@gmail.com"
        }
    });

    // JWT auth in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste your JWT token here. Format: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddRazorPages();

// EF Core + SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository Pattern � register the generic repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<KerzelPay.Services.StripeService>();
builder.Services.AddScoped<KerzelPay.Services.CurrencyService>();
builder.Services.AddScoped<KerzelPay.Services.TransferService>();
builder.Services.AddScoped<KerzelPay.Services.AgentCashService>();
// HttpClient for the live currency-rates API (Frankfurter / ECB)
builder.Services.AddHttpClient("Frankfurter", client =>
{
    client.BaseAddress = new Uri("https://api.frankfurter.app/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<KerzelPay.Services.RateRefreshService>();
builder.Services.AddScoped<KerzelPay.Services.IEmailService, KerzelPay.Services.SmtpEmailService>();

builder.Services.AddScoped<KerzelPay.Services.JwtService>();

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

// ---- JWT Authentication (for the REST API) ----
// Note: Identity already added cookie auth. We add JWT as an ADDITIONAL scheme.
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.SaveTokens = true;
    });
// Stripe configuration � keys come from User Secrets (safe, never in Git)
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Swagger only in development (production should disable)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kerzel Pay API V1");
        c.RoutePrefix = "swagger";  // available at /swagger
    });
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
    SettingsSeeder.SeedDefaults(context);

    // Seed demo agents
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    await AgentSeeder.SeedAgentsAsync(context, userManager);

    var rateRefreshService = services.GetRequiredService<KerzelPay.Services.RateRefreshService>();
    await rateRefreshService.RefreshRatesAsync();
}

app.Run();