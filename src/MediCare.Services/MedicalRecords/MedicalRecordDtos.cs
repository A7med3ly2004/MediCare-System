using MediCare.Data.Entities;
using MediCare.Services.Appointments;

namespace MediCare.Services.MedicalRecords;

public class PrescriptionItemDto
{
    public int Id { get; set; }
    public string Medication { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
}

public class SaveMedicalRecordDto
{
    public int AppointmentId { get; set; }
    public string DoctorUserId { get; set; } = string.Empty;
    public string? BloodPressure { get; set; }
    public int? HeartRate { get; set; }
    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentNotes { get; set; }
    public List<PrescriptionItemDto> Prescriptions { get; set; } = new();
}

public class MedicalRecordDetailDto
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string FormattedTime => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";

    public int DoctorId { get; set; }
    public string DoctorUserId { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorSpecialization { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }

    public int PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? PatientEmail { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int Age => DateTime.Today.Year - DateOfBirth.Year - (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }

    public string? BloodPressure { get; set; }
    public int? HeartRate { get; set; }
    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentNotes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int? PrescriptionId { get; set; }
    public DateTime? PrescriptionIssuedAt { get; set; }
    public List<PrescriptionItemDto> Prescriptions { get; set; } = new();
}

public class PatientMedicalProfileDto
{
    public int PatientId { get; set; }
    public string PatientUserId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int Age => DateTime.Today.Year - DateOfBirth.Year - (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }

    public AppointmentListItemDto? NextAppointment { get; set; }
    public List<MedicalRecordDetailDto> MedicalRecords { get; set; } = new();
}

public class UpdatePatientProfileDto
{
    public string PatientUserId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? BloodGroup { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalHistory { get; set; }
}
