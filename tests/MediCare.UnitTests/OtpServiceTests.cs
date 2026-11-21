using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Interfaces;
using MediCare.Services.Otp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediCare.UnitTests;

public class OtpServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private class TestNotificationService : INotificationService
    {
        public List<(string UserId, string ToEmail, string PlainOtp)> SentOtps { get; } = new();

        public Task<bool> SendOtpEmailAsync(string userId, string toEmail, string toName, string plainOtp)
        {
            SentOtps.Add((userId, toEmail, plainOtp));
            return Task.FromResult(true);
        }

        public Task<bool> SendBookingReceivedPatientAsync(string userId, string toEmail, string toName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room, decimal fee) => Task.FromResult(true);
        public Task<bool> SendBookingReceivedDoctorAsync(string userId, string toEmail, string doctorName, string patientName, string patientCode, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? notes) => Task.FromResult(true);
        public Task<bool> SendAppointmentConfirmedAsync(string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room) => Task.FromResult(true);
        public Task<bool> SendAppointmentRejectedAsync(string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? reason) => Task.FromResult(true);
        public Task<bool> SendAppointmentCancelledAsync(string userId, string toEmail, string recipientName, string otherPartyName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string cancelledBy) => Task.FromResult(true);
        public Task<bool> SendAppointmentRescheduledAsync(string userId, string toEmail, string recipientName, string otherPartyName, DateOnly newDate, TimeSpan newStartTime, TimeSpan newEndTime) => Task.FromResult(true);
        public Task<bool> SendReminderAsync(string userId, string toEmail, string recipientName, string doctorName, DateOnly date, TimeSpan startTime, TimeSpan endTime, string? room) => Task.FromResult(true);
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        var options = Options.Create(new IdentityOptions());
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var services = new ServiceCollection().BuildServiceProvider();

        return new UserManager<ApplicationUser>(
            store, options, passwordHasher, userValidators, passwordValidators,
            keyNormalizer, errors, services, NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private class TestConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _data;
        public TestConfiguration(Dictionary<string, string?>? data = null) => _data = data ?? new();
        public string? this[string key] { get => _data.TryGetValue(key, out var val) ? val : null; set => _data[key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    private static IConfiguration CreateConfig(int cooldownSeconds = 60)
    {
        return new TestConfiguration(new Dictionary<string, string?>
        {
            ["Otp:ResendCooldownSeconds"] = cooldownSeconds.ToString(),
            ["Otp:HashKey"] = "Test_Secret_HMAC_Key_For_Unit_Testing_12345"
        });
    }

    [Fact]
    public async Task SendOtpAsync_Generates6Digits_StoresHash_AndSendsEmail()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var userManager = CreateUserManager(db);
        var notif = new TestNotificationService();
        var config = CreateConfig();
        var service = new OtpService(uow, userManager, notif, config, NullLogger<OtpService>.Instance);

        var user = new ApplicationUser { Id = "u1", Email = "u1@test.com", UserName = "u1@test.com", FullName = "User One", IsEmailVerified = false };
        await userManager.CreateAsync(user);

        var result = await service.SendOtpAsync(user.Id);

        Assert.Equal(OtpStatus.Success, result.Status);
        Assert.Single(notif.SentOtps);

        var plainOtp = notif.SentOtps[0].PlainOtp;
        Assert.Equal(6, plainOtp.Length);
        Assert.True(int.TryParse(plainOtp, out _));

        var savedOtp = await db.OtpCodes.FirstOrDefaultAsync(o => o.UserId == user.Id);
        Assert.NotNull(savedOtp);
        // Verify code is stored as a hash and NOT as plaintext
        Assert.NotEqual(plainOtp, savedOtp.CodeHash);
        Assert.Equal(64, savedOtp.CodeHash.Length); // HMAC-SHA256 hex string length is 64
        Assert.False(savedOtp.IsUsed);
        Assert.Equal(0, savedOtp.FailedAttempts);
        Assert.True(savedOtp.ExpiresAt > DateTime.UtcNow.AddMinutes(9));
    }

    [Fact]
    public async Task VerifyOtpAsync_CorrectCode_SetsIsEmailVerifiedAndMarksCodeUsed()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var userManager = CreateUserManager(db);
        var notif = new TestNotificationService();
        var config = CreateConfig();
        var service = new OtpService(uow, userManager, notif, config, NullLogger<OtpService>.Instance);

        var user = new ApplicationUser { Id = "u2", Email = "u2@test.com", UserName = "u2@test.com", FullName = "User Two", IsEmailVerified = false };
        await userManager.CreateAsync(user);

        await service.SendOtpAsync(user.Id);
        var plainOtp = notif.SentOtps[0].PlainOtp;

        var verifyResult = await service.VerifyOtpAsync(user.Id, plainOtp);

        Assert.Equal(OtpStatus.Success, verifyResult.Status);

        var updatedUser = await userManager.FindByIdAsync(user.Id);
        Assert.True(updatedUser!.IsEmailVerified);
        Assert.True(updatedUser.EmailConfirmed);

        var savedOtp = await db.OtpCodes.FirstOrDefaultAsync(o => o.UserId == user.Id);
        Assert.NotNull(savedOtp);
        Assert.True(savedOtp.IsUsed);
    }

    [Fact]
    public async Task VerifyOtpAsync_ExpiredCode_ReturnsExpired()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var userManager = CreateUserManager(db);
        var notif = new TestNotificationService();
        var config = CreateConfig();
        var service = new OtpService(uow, userManager, notif, config, NullLogger<OtpService>.Instance);

        var user = new ApplicationUser { Id = "u3", Email = "u3@test.com", UserName = "u3@test.com", FullName = "User Three", IsEmailVerified = false };
        await userManager.CreateAsync(user);

        await service.SendOtpAsync(user.Id);
        var plainOtp = notif.SentOtps[0].PlainOtp;

        // Manually expire the code
        var savedOtp = await db.OtpCodes.FirstAsync(o => o.UserId == user.Id);
        savedOtp.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var verifyResult = await service.VerifyOtpAsync(user.Id, plainOtp);

        Assert.Equal(OtpStatus.Expired, verifyResult.Status);
    }

    [Fact]
    public async Task VerifyOtpAsync_5WrongAttempts_LocksOtp()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var userManager = CreateUserManager(db);
        var notif = new TestNotificationService();
        var config = CreateConfig();
        var service = new OtpService(uow, userManager, notif, config, NullLogger<OtpService>.Instance);

        var user = new ApplicationUser { Id = "u4", Email = "u4@test.com", UserName = "u4@test.com", FullName = "User Four", IsEmailVerified = false };
        await userManager.CreateAsync(user);

        await service.SendOtpAsync(user.Id);
        var correctOtp = notif.SentOtps[0].PlainOtp;
        var wrongOtp = correctOtp == "123456" ? "654321" : "123456";

        // Attempts 1 to 4: WrongCode with remaining attempts count
        for (int i = 1; i <= 4; i++)
        {
            var r = await service.VerifyOtpAsync(user.Id, wrongOtp);
            Assert.Equal(OtpStatus.WrongCode, r.Status);
            Assert.Equal(5 - i, r.AttemptsLeft);
        }

        // Attempt 5: Reaches MaxFailedAttempts (5) -> Locked
        var fifthResult = await service.VerifyOtpAsync(user.Id, wrongOtp);
        Assert.Equal(OtpStatus.Locked, fifthResult.Status);

        // Subsequent attempt, even with correct OTP, is locked
        var sixthResult = await service.VerifyOtpAsync(user.Id, correctOtp);
        Assert.Equal(OtpStatus.Locked, sixthResult.Status);
    }

    [Fact]
    public async Task SendOtpAsync_WithinCooldownPeriod_RefusesWithCooldownActive()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var userManager = CreateUserManager(db);
        var notif = new TestNotificationService();
        var config = CreateConfig(cooldownSeconds: 60);
        var service = new OtpService(uow, userManager, notif, config, NullLogger<OtpService>.Instance);

        var user = new ApplicationUser { Id = "u5", Email = "u5@test.com", UserName = "u5@test.com", FullName = "User Five", IsEmailVerified = false };
        await userManager.CreateAsync(user);

        // First send succeeds
        var first = await service.SendOtpAsync(user.Id);
        Assert.Equal(OtpStatus.Success, first.Status);

        // Immediate second send fails with CooldownActive
        var second = await service.SendOtpAsync(user.Id);
        Assert.Equal(OtpStatus.CooldownActive, second.Status);
        Assert.True(second.SecondsRemaining > 0 && second.SecondsRemaining <= 60);
    }

    [Fact]
    public async Task SendOtpAsync_NewCodeInvalidatesOlderUnusedCodes()
    {
        using var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var userManager = CreateUserManager(db);
        var notif = new TestNotificationService();
        var config = CreateConfig(cooldownSeconds: 0); // 0 cooldown for immediate re-send
        var service = new OtpService(uow, userManager, notif, config, NullLogger<OtpService>.Instance);

        var user = new ApplicationUser { Id = "u6", Email = "u6@test.com", UserName = "u6@test.com", FullName = "User Six", IsEmailVerified = false };
        await userManager.CreateAsync(user);

        // First code
        await service.SendOtpAsync(user.Id);
        var firstCode = notif.SentOtps[0].PlainOtp;

        // Move first OTP CreatedAt to past so cooldown is satisfied
        var firstRecord = await db.OtpCodes.FirstAsync(o => o.UserId == user.Id);
        firstRecord.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync();

        // Second code
        await service.SendOtpAsync(user.Id);
        var secondCode = notif.SentOtps[1].PlainOtp;

        // Trying the first code now must fail because old codes were invalidated
        var resultFirst = await service.VerifyOtpAsync(user.Id, firstCode);
        Assert.True(resultFirst.Status == OtpStatus.WrongCode || resultFirst.Status == OtpStatus.Expired);

        // Trying the second code succeeds
        var resultSecond = await service.VerifyOtpAsync(user.Id, secondCode);
        Assert.Equal(OtpStatus.Success, resultSecond.Status);
    }
}
