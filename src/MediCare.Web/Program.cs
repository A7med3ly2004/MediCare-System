using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.Extensions;
using MediCare.Data.Seed;
using MediCare.Services.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ───────────────────────────────────────────────────────────────

// Global antiforgery validation on all unsafe HTTP methods (POST, PUT, DELETE)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Register the Data layer (DbContext, Repositories, Unit of Work, Seeder)
builder.Services.AddDataLayer(builder.Configuration);

// Register the Services layer (AccountService, etc.)
builder.Services.AddServicesLayer(builder.Configuration);

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy: at least 8 chars, a digit, uppercase and lowercase
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout policy: 5 failed attempts locks out for 15 minutes
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User policy
    options.User.RequireUniqueEmail = true;

    // Email confirmation handled via OTP in application logic (Task 2B)
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Cookie configuration for Identity
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Antiforgery cookie configuration
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ─── App Pipeline ───────────────────────────────────────────────────────────

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Friendly error status code re-execute (404, 403, 500)
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─── Development: apply migrations and seed ─────────────────────────────────

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        // Apply any pending migrations automatically
        await db.Database.MigrateAsync();

        // Run the seeder (idempotent)
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database initialization.");
    }
}

app.Run();

public partial class Program { }
