using System.ComponentModel.DataAnnotations;
using MediCare.Services.MedicalRecords;

namespace MediCare.Web.ViewModels.MedicalRecords;

public class PrescriptionItemViewModel
{
    public int Id { get; set; }

    [StringLength(200, ErrorMessage = "Medication cannot exceed 200 characters.")]
    public string Medication { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Dosage cannot exceed 100 characters.")]
    public string Dosage { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Frequency cannot exceed 100 characters.")]
    public string Frequency { get; set; } = string.Empty;
}

public class RecordVisitViewModel
{
    [Required]
    public int AppointmentId { get; set; }

    public int? MedicalRecordId { get; set; }
    public bool IsEdit => MedicalRecordId.HasValue && MedicalRecordId.Value > 0;

    // Read-only Clinical Context
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorSpecialization { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string FormattedTime => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";

    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public int PatientAge => DateTime.Today.Year - DateOfBirth.Year - (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }

    // Editable Clinical Fields
    [RegularExpression(@"^\d{2,3}\/\d{2,3}(\s*(mmHg|mm Hg))?$", ErrorMessage = "Blood pressure must follow format like '120/80' or '120/80 mmHg'.")]
    [StringLength(20, ErrorMessage = "Blood pressure cannot exceed 20 characters.")]
    [Display(Name = "Blood pressure (e.g. 120/80)")]
    public string? BloodPressure { get; set; }

    [Range(20, 300, ErrorMessage = "Heart rate must be between 20 and 300 bpm.")]
    [Display(Name = "Heart rate (bpm)")]
    public int? HeartRate { get; set; }

    [StringLength(2000, ErrorMessage = "Symptoms cannot exceed 2000 characters.")]
    [Display(Name = "Presenting symptoms & complaint")]
    public string? Symptoms { get; set; }

    [Required(ErrorMessage = "Diagnosis is required.")]
    [StringLength(2000, ErrorMessage = "Diagnosis cannot exceed 2000 characters.")]
    [Display(Name = "Clinical diagnosis *")]
    public string Diagnosis { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Treatment notes cannot exceed 4000 characters.")]
    [Display(Name = "Treatment plan & clinical notes")]
    public string? TreatmentNotes { get; set; }

    // Prescription lines
    public List<PrescriptionItemViewModel> Items { get; set; } = new();
}

public class PatientProfileViewModel
{
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public int Age => DateTime.Today.Year - DateOfBirth.Year - (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone number cannot exceed 30 characters.")]
    [Display(Name = "Mobile phone number")]
    public string? PhoneNumber { get; set; }

    [RegularExpression(@"^(A\+|A-|B\+|B-|AB\+|AB-|O\+|O-)?$", ErrorMessage = "Blood group must be one of: A+, A-, B+, B-, AB+, AB-, O+, O-.")]
    [Display(Name = "Blood group")]
    public string? BloodGroup { get; set; }

    [StringLength(1000, ErrorMessage = "Allergies cannot exceed 1000 characters.")]
    [Display(Name = "Known drug or substance allergies")]
    public string? Allergies { get; set; }

    [StringLength(2000, ErrorMessage = "Medical history cannot exceed 2000 characters.")]
    [Display(Name = "Chronic conditions & relevant medical history")]
    public string? MedicalHistory { get; set; }
}

public class PatientHistoryPageViewModel
{
    public List<MedicalRecordDetailDto> Records { get; set; } = new();
}
