using System.Security.Cryptography;
using System.Text;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediCare.Services.Otp;

public class OtpService : IOtpService
{
    private readonly IUnitOfWork _uow;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OtpService> _logger;

    private const int CodeValidityMinutes = 10;
    private const int MaxFailedAttempts = 5;
    private const int DefaultCooldownSeconds = 60;
    private const string FallbackHashKey = "MediCare_Default_Hmac_Secret_Key_For_Development_Only";

    public OtpService(
        IUnitOfWork uow,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<OtpService> logger)
    {
        _uow = uow;
        _userManager = userManager;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OtpSendResult> SendOtpByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return OtpSendResult.UserNotFound();

        return await SendOtpAsync(user.Id);
    }

    public async Task<OtpSendResult> SendOtpAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return OtpSendResult.UserNotFound();

        if (user.IsEmailVerified)
            return OtpSendResult.AlreadyVerified();

        // Check cooldown from the latest code's CreatedAt
        var latestCode = await _uow.OtpCodes.GetLatestForUserAsync(userId);
        if (latestCode is not null)
        {
            var cooldownStr = _configuration["Otp:ResendCooldownSeconds"];
            var cooldownSec = int.TryParse(cooldownStr, out var sec) && sec > 0 ? sec : DefaultCooldownSeconds;

            var elapsed = DateTime.UtcNow - latestCode.CreatedAt;
            if (elapsed.TotalSeconds < cooldownSec)
            {
                var remaining = (int)Math.Ceiling(cooldownSec - elapsed.TotalSeconds);
                return OtpSendResult.CooldownActive(remaining);
            }
        }

        // Invalidate all older unused codes for this user
        await _uow.OtpCodes.InvalidateAllForUserAsync(userId);

        // Generate cryptographically secure 6-digit code (including leading zeros)
        var plainCode = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        var codeHash = ComputeHash(plainCode);

        var otp = new OtpCode
        {
            UserId = userId,
            CodeHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(CodeValidityMinutes),
            FailedAttempts = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.OtpCodes.AddAsync(otp);
        await _uow.SaveChangesAsync();

        // Send OTP email (never logs plain code except development console sender)
        var recipientName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email! : user.FullName;
        await _notificationService.SendOtpEmailAsync(userId, user.Email!, recipientName, plainCode);

        _logger.LogInformation("Issued new OTP for user {UserId}", userId);
        return OtpSendResult.Success();
    }

    public async Task<OtpVerifyResult> VerifyOtpByEmailAsync(string email, string plainCode)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return OtpVerifyResult.UserNotFound();

        return await VerifyOtpAsync(user.Id, plainCode);
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(string userId, string plainCode)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return OtpVerifyResult.UserNotFound();

        if (user.IsEmailVerified)
            return OtpVerifyResult.AlreadyVerified();

        var latestCode = await _uow.OtpCodes.GetLatestForUserAsync(userId);
        if (latestCode is null)
            return OtpVerifyResult.WrongCode(0);

        if (latestCode.IsUsed)
            return OtpVerifyResult.Expired();

        if (latestCode.FailedAttempts >= MaxFailedAttempts)
            return OtpVerifyResult.Locked();

        if (latestCode.ExpiresAt <= DateTime.UtcNow)
            return OtpVerifyResult.Expired();

        // Compare hash in constant time
        var candidateHash = ComputeHash(plainCode.Trim());
        bool matches = ConstantTimeEquals(candidateHash, latestCode.CodeHash);

        if (matches)
        {
            latestCode.IsUsed = true;
            user.IsEmailVerified = true;
            user.EmailConfirmed = true;

            await _userManager.UpdateAsync(user);
            await _uow.SaveChangesAsync();

            _logger.LogInformation("User {UserId} successfully verified email OTP", userId);
            return OtpVerifyResult.Success();
        }

        // Wrong code: increment failed attempts
        latestCode.FailedAttempts++;
        await _uow.SaveChangesAsync();

        if (latestCode.FailedAttempts >= MaxFailedAttempts)
        {
            _logger.LogWarning("OTP for user {UserId} locked after reaching max failed attempts ({Attempts})",
                userId, latestCode.FailedAttempts);
            return OtpVerifyResult.Locked();
        }

        int attemptsLeft = MaxFailedAttempts - latestCode.FailedAttempts;
        _logger.LogWarning("Invalid OTP attempt for user {UserId}. {AttemptsLeft} attempts left.",
            userId, attemptsLeft);

        return OtpVerifyResult.WrongCode(attemptsLeft);
    }

    private string ComputeHash(string plainCode)
    {
        var secret = _configuration["Otp:HashKey"];
        if (string.IsNullOrWhiteSpace(secret))
            secret = FallbackHashKey;

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(plainCode);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);
        return Convert.ToHexString(hashBytes);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
