namespace MediCare.Services.Interfaces;

public enum OtpStatus
{
    Success,
    WrongCode,
    Expired,
    Locked,
    CooldownActive,
    AlreadyVerified,
    UserNotFound
}

public class OtpSendResult
{
    public OtpStatus Status { get; init; }
    public int SecondsRemaining { get; init; }

    public static OtpSendResult Success() => new() { Status = OtpStatus.Success };
    public static OtpSendResult CooldownActive(int seconds) => new() { Status = OtpStatus.CooldownActive, SecondsRemaining = seconds };
    public static OtpSendResult AlreadyVerified() => new() { Status = OtpStatus.AlreadyVerified };
    public static OtpSendResult UserNotFound() => new() { Status = OtpStatus.UserNotFound };
}

public class OtpVerifyResult
{
    public OtpStatus Status { get; init; }
    public int AttemptsLeft { get; init; }

    public static OtpVerifyResult Success() => new() { Status = OtpStatus.Success };
    public static OtpVerifyResult WrongCode(int attemptsLeft) => new() { Status = OtpStatus.WrongCode, AttemptsLeft = attemptsLeft };
    public static OtpVerifyResult Expired() => new() { Status = OtpStatus.Expired };
    public static OtpVerifyResult Locked() => new() { Status = OtpStatus.Locked };
    public static OtpVerifyResult AlreadyVerified() => new() { Status = OtpStatus.AlreadyVerified };
    public static OtpVerifyResult UserNotFound() => new() { Status = OtpStatus.UserNotFound };
}

/// <summary>
/// Generates, validates, and resends email OTPs (FR-04).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a 6-digit OTP, stores its HMAC-SHA256 hash, and emails it to the user.
    /// Inactivates any previous codes for the user. Enforces resend cooldown.
    /// </summary>
    Task<OtpSendResult> SendOtpAsync(string userId);

    /// <summary>
    /// Convenience overload to send OTP by user's email address.
    /// </summary>
    Task<OtpSendResult> SendOtpByEmailAsync(string email);

    /// <summary>
    /// Verifies the plain 6-digit code against the stored hash in constant time.
    /// On success sets IsEmailVerified = true, EmailConfirmed = true, and marks code used.
    /// </summary>
    Task<OtpVerifyResult> VerifyOtpAsync(string userId, string plainCode);

    /// <summary>
    /// Convenience overload to verify OTP by user's email address.
    /// </summary>
    Task<OtpVerifyResult> VerifyOtpByEmailAsync(string email, string plainCode);
}
