using System.Security.Claims;
using System.Text.Encodings.Web;
using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediCare.Tests;

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> SentEmails { get; } = new();

    public Task<bool> SendAsync(string to, string subject, string htmlBody)
    {
        lock (SentEmails)
        {
            SentEmails.Add((to, subject, htmlBody));
        }
        return Task.FromResult(true);
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = roleValues.ToString();
        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var userValues)
            ? userValues.ToString()
            : $"test-{role.ToLowerInvariant()}-1";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"{role} User"),
            new(ClaimTypes.Email, $"{role.ToLowerInvariant()}@medicare.test"),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=MediCareIntegrationTestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public FakeEmailSender FakeEmailSender { get; } = new();

    private static readonly object DbInitLock = new();
    private static bool _dbInitialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace DbContext with real SQL Server LocalDB
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(TestConnectionString);
                options.EnableSensitiveDataLogging();
            });

            // Replace IEmailSender with FakeEmailSender
            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor != null)
            {
                services.Remove(emailDescriptor);
            }
            services.AddSingleton(FakeEmailSender);
            services.AddSingleton<IEmailSender>(FakeEmailSender);

            // Add Test Authentication Scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
        });
    }

    public void EnsureDatabaseSeeded()
    {
        lock (DbInitLock)
        {
            if (_dbInitialized) return;

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var passwordHasher = new PasswordHasher<ApplicationUser>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            // 1. Roles
            var roles = new[] { "Admin", "Doctor", "Patient" };
            foreach (var r in roles)
            {
                if (!db.Roles.Any(x => x.Name == r))
                {
                    db.Roles.Add(new IdentityRole { Id = Guid.NewGuid().ToString(), Name = r, NormalizedName = r.ToUpperInvariant() });
                }
            }
            db.SaveChanges();

            // 2. Specialization
            var spec = new Specialization { Name = "Cardiology" };
            db.Specializations.Add(spec);
            db.SaveChanges();

            // 3. Admin User
            var adminUser = new ApplicationUser
            {
                Id = "test-admin-1",
                UserName = "admin@medicare.test",
                NormalizedUserName = "ADMIN@MEDICARE.TEST",
                Email = "admin@medicare.test",
                NormalizedEmail = "ADMIN@MEDICARE.TEST",
                FullName = "System Administrator",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Password#12345");
            db.Users.Add(adminUser);

            // 4. Doctor 1
            var doc1User = new ApplicationUser
            {
                Id = "test-doc-1",
                UserName = "doctor@medicare.test",
                NormalizedUserName = "DOCTOR@MEDICARE.TEST",
                Email = "doctor@medicare.test",
                NormalizedEmail = "DOCTOR@MEDICARE.TEST",
                FullName = "Dr. Samira Zaki",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            doc1User.PasswordHash = passwordHasher.HashPassword(doc1User, "Password#12345");
            db.Users.Add(doc1User);

            var doctor1 = new Doctor
            {
                UserId = doc1User.Id,
                SpecializationId = spec.Id,
                Location = "Main Clinic",
                RoomNumber = "101",
                ConsultationFee = 400m
            };
            db.Doctors.Add(doctor1);
            db.SaveChanges();

            // Doctor 1 schedules: Mon through Sun, 08:00 to 18:00 (30m slots)
            for (int day = 0; day <= 6; day++)
            {
                db.DoctorSchedules.Add(new DoctorSchedule
                {
                    DoctorId = doctor1.Id,
                    DayOfWeek = day,
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    SlotMinutes = 30
                });
            }

            // 5. Doctor 2
            var doc2User = new ApplicationUser
            {
                Id = "test-doc-2",
                UserName = "doctor2@medicare.test",
                NormalizedUserName = "DOCTOR2@MEDICARE.TEST",
                Email = "doctor2@medicare.test",
                NormalizedEmail = "DOCTOR2@MEDICARE.TEST",
                FullName = "Dr. Hany Nabil",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            doc2User.PasswordHash = passwordHasher.HashPassword(doc2User, "Password#12345");
            db.Users.Add(doc2User);

            var doctor2 = new Doctor
            {
                UserId = doc2User.Id,
                SpecializationId = spec.Id,
                Location = "Branch Clinic",
                RoomNumber = "202",
                ConsultationFee = 350m
            };
            db.Doctors.Add(doctor2);
            db.SaveChanges();

            // 6. Patient 1
            var pat1User = new ApplicationUser
            {
                Id = "test-pat-1",
                UserName = "patient@medicare.test",
                NormalizedUserName = "PATIENT@MEDICARE.TEST",
                Email = "patient@medicare.test",
                NormalizedEmail = "PATIENT@MEDICARE.TEST",
                FullName = "Tarek Youssef",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            pat1User.PasswordHash = passwordHasher.HashPassword(pat1User, "Password#12345");
            db.Users.Add(pat1User);

            var patient1 = new Patient
            {
                UserId = pat1User.Id,
                PatientCode = "P-9001",
                DateOfBirth = new DateOnly(1992, 4, 10),
                BloodGroup = "A+"
            };
            db.Patients.Add(patient1);

            // 7. Patient 2
            var pat2User = new ApplicationUser
            {
                Id = "test-pat-2",
                UserName = "patient2@medicare.test",
                NormalizedUserName = "PATIENT2@MEDICARE.TEST",
                Email = "patient2@medicare.test",
                NormalizedEmail = "PATIENT2@MEDICARE.TEST",
                FullName = "Nouran Ezzat",
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };
            pat2User.PasswordHash = passwordHasher.HashPassword(pat2User, "Password#12345");
            db.Users.Add(pat2User);

            var patient2 = new Patient
            {
                UserId = pat2User.Id,
                PatientCode = "P-9002",
                DateOfBirth = new DateOnly(1995, 8, 22),
                BloodGroup = "O+"
            };
            db.Patients.Add(patient2);
            db.SaveChanges();

            // 8. Account state variants for login testing
            var unverifiedUser = new ApplicationUser
            {
                Id = "test-unverified-1",
                UserName = "unverified@medicare.test",
                NormalizedUserName = "UNVERIFIED@MEDICARE.TEST",
                Email = "unverified@medicare.test",
                NormalizedEmail = "UNVERIFIED@MEDICARE.TEST",
                FullName = "Unverified User",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow
            };
            unverifiedUser.PasswordHash = passwordHasher.HashPassword(unverifiedUser, "Password#12345");
            db.Users.Add(unverifiedUser);

            var suspendedUser = new ApplicationUser
            {
                Id = "test-suspended-1",
                UserName = "suspended@medicare.test",
                NormalizedUserName = "SUSPENDED@MEDICARE.TEST",
                Email = "suspended@medicare.test",
                NormalizedEmail = "SUSPENDED@MEDICARE.TEST",
                FullName = "Suspended User",
                IsEmailVerified = true,
                IsSuspended = true,
                CreatedAt = DateTime.UtcNow
            };
            suspendedUser.PasswordHash = passwordHasher.HashPassword(suspendedUser, "Password#12345");
            db.Users.Add(suspendedUser);

            var deletedUser = new ApplicationUser
            {
                Id = "test-deleted-1",
                UserName = "deleted@medicare.test",
                NormalizedUserName = "DELETED@MEDICARE.TEST",
                Email = "deleted@medicare.test",
                NormalizedEmail = "DELETED@MEDICARE.TEST",
                FullName = "Deleted User",
                IsEmailVerified = true,
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow
            };
            deletedUser.PasswordHash = passwordHasher.HashPassword(deletedUser, "Password#12345");
            db.Users.Add(deletedUser);

            db.SaveChanges();

            // Assign Roles in AspNetUserRoles
            var adminRole = db.Roles.First(r => r.Name == "Admin");
            var doctorRole = db.Roles.First(r => r.Name == "Doctor");
            var patientRole = db.Roles.First(r => r.Name == "Patient");

            db.UserRoles.Add(new IdentityUserRole<string> { UserId = adminUser.Id, RoleId = adminRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = doc1User.Id, RoleId = doctorRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = doc2User.Id, RoleId = doctorRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = pat1User.Id, RoleId = patientRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = pat2User.Id, RoleId = patientRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = unverifiedUser.Id, RoleId = patientRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = suspendedUser.Id, RoleId = patientRole.Id });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = deletedUser.Id, RoleId = patientRole.Id });

            db.SaveChanges();
            _dbInitialized = true;
        }
    }

    public HttpClient CreateClientForRole(string role, string? userId = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        if (!string.Equals(role, "Anonymous", StringComparison.OrdinalIgnoreCase))
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
            if (!string.IsNullOrEmpty(userId))
            {
                client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
            }
        }

        return client;
    }
}
