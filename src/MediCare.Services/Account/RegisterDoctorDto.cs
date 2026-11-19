namespace MediCare.Services.Account;

public class RegisterDoctorDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int SpecializationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public string? RoomNumber { get; set; }
    public string? ProfileImagePath { get; set; }  // set by controller after saving file
    public bool AcceptedPrivacyNotice { get; set; }
}
