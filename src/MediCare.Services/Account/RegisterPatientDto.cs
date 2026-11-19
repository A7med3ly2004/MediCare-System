namespace MediCare.Services.Account;

public class RegisterPatientDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }
    public bool AcceptedPrivacyNotice { get; set; }
}
