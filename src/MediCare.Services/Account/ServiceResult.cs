namespace MediCare.Services.Account;

public class ServiceResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
    public static ServiceResult Ok() => new() { Succeeded = true };
    public static ServiceResult Fail(string error) => new() { Succeeded = false, Error = error };
}

public class LoginResult
{
    public bool Succeeded { get; init; }
    public bool IsLockedOut { get; init; }
    public bool IsUnverified { get; init; }
    public bool IsSuspended { get; init; }
    public bool IsDeleted { get; init; }
    public string? Role { get; init; }
    /// <summary>The user's string Id — used by the controller to call SignInManager.SignInAsync.</summary>
    public string? UserId { get; init; }
    public string? Error { get; init; }
    public static LoginResult Ok(string role, string userId) => new() { Succeeded = true, Role = role, UserId = userId };
    public static LoginResult Fail(string error) => new() { Error = error };
    public static LoginResult Locked() => new() { IsLockedOut = true, Error = "Account temporarily locked. Please try again later." };
    public static LoginResult Unverified() => new() { IsUnverified = true, Error = "Please verify your email address before logging in." };
    public static LoginResult Suspended() => new() { IsSuspended = true, Error = "Your account has been suspended. Please contact the clinic." };
    public static LoginResult Deleted() => new() { IsDeleted = true, Error = "Account not found." };
}
