using MediCare.Data;
using MediCare.Data.Entities;
using MediCare.Data.UnitOfWork;
using MediCare.Services.MedicalRecords;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediCare.UnitTests;

public class MedicalRecordServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (ApplicationDbContext db, UnitOfWork uow, MedicalRecordService service, Doctor doctor, Patient patient, Appointment appointment) SetupEnvironment()
    {
        var db = CreateInMemoryDbContext();
        var uow = new UnitOfWork(db);
        var service = new MedicalRecordService(uow, db, NullLogger<MedicalRecordService>.Instance);

        var doctorUser = new ApplicationUser { Id = "doc-user", FullName = "Dr. Mariam Ahmed", Email = "mariam@test.com" };
        var patientUser = new ApplicationUser { Id = "patient-user", FullName = "Ahmed Tarek", Email = "ahmed@test.com" };

        var specialization = new Specialization { Id = 1, Name = "Cardiology" };

        var doctor = new Doctor
        {
            Id = 1,
            UserId = "doc-user",
            User = doctorUser,
            SpecializationId = 1,
            Specialization = specialization,
            Location = "Cairo Clinic",
            RoomNumber = "02",
            ConsultationFee = 500
        };

        var patient = new Patient
        {
            Id = 1,
            UserId = "patient-user",
            User = patientUser,
            PatientCode = "P-1042",
            DateOfBirth = new DateOnly(1988, 5, 14),
            BloodGroup = "O+",
            Allergies = "Penicillin",
            MedicalHistory = "Essential hypertension"
        };

        var appointment = new Appointment
        {
            Id = 1,
            DoctorId = doctor.Id,
            Doctor = doctor,
            PatientId = patient.Id,
            Patient = patient,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Status = AppointmentStatus.Confirmed
        };

        db.Users.AddRange(doctorUser, patientUser);
        db.Specializations.Add(specialization);
        db.Doctors.Add(doctor);
        db.Patients.Add(patient);
        db.Appointments.Add(appointment);
        db.SaveChanges();

        return (db, uow, service, doctor, patient, appointment);
    }

    [Fact]
    public async Task SaveVisitAsync_ConfirmedAppointment_CompletesAppointmentAndSavesRecordInOneTransaction()
    {
        var (db, _, service, doctor, patient, appointment) = SetupEnvironment();

        var dto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            BloodPressure = "130/85",
            HeartRate = 76,
            Symptoms = "Mild headache and dizziness",
            Diagnosis = "Primary hypertension, controlled",
            TreatmentNotes = "Continue current anti-hypertensive regimen and lifestyle modifications.",
            Prescriptions = new List<PrescriptionItemDto>
            {
                new() { Medication = "Amlodipine", Dosage = "5 mg", Frequency = "Once daily" },
                new() { Medication = "Concor", Dosage = "2.5 mg", Frequency = "Once daily morning" }
            }
        };

        var result = await service.SaveVisitAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("130/85 mmHg", result.BloodPressure);
        Assert.Equal(76, result.HeartRate);
        Assert.Equal("Primary hypertension, controlled", result.Diagnosis);
        Assert.Equal(2, result.Prescriptions.Count);

        // Verify Appointment status in DB transitioned to Completed (BR-05)
        var apptInDb = await db.Appointments.FindAsync(appointment.Id);
        Assert.NotNull(apptInDb);
        Assert.Equal(AppointmentStatus.Completed, apptInDb.Status);

        // Verify MedicalRecord in DB
        var recordInDb = await db.MedicalRecords
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(m => m.AppointmentId == appointment.Id);

        Assert.NotNull(recordInDb);
        Assert.NotNull(recordInDb.Prescription);
        Assert.Equal(2, recordInDb.Prescription.Items.Count);
    }

    [Fact]
    public async Task SaveVisitAsync_PendingOrCancelledAppointment_ThrowsInvalidVisitStateException()
    {
        var (db, _, service, doctor, _, appointment) = SetupEnvironment();

        appointment.Status = AppointmentStatus.Pending;
        await db.SaveChangesAsync();

        var dto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            Diagnosis = "Test"
        };

        await Assert.ThrowsAsync<InvalidVisitStateException>(() => service.SaveVisitAsync(dto));

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidVisitStateException>(() => service.SaveVisitAsync(dto));
    }

    [Fact]
    public async Task SaveVisitAsync_AnotherDoctorAppointment_ThrowsUnauthorizedAccessException()
    {
        var (_, _, service, _, _, appointment) = SetupEnvironment();

        var dto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = "other-doctor-user",
            Diagnosis = "Test"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveVisitAsync(dto));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(350)]
    public async Task SaveVisitAsync_InvalidHeartRate_ThrowsArgumentException(int invalidHr)
    {
        var (_, _, service, doctor, _, appointment) = SetupEnvironment();

        var dto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            HeartRate = invalidHr,
            Diagnosis = "Test"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveVisitAsync(dto));
    }

    [Theory]
    [InlineData("120")]
    [InlineData("high")]
    [InlineData("120/80/70")]
    public async Task SaveVisitAsync_InvalidBloodPressure_ThrowsArgumentException(string invalidBp)
    {
        var (_, _, service, doctor, _, appointment) = SetupEnvironment();

        var dto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            BloodPressure = invalidBp,
            Diagnosis = "Test"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveVisitAsync(dto));
    }

    [Fact]
    public async Task UpdateVisitAsync_AllowedForTreatingDoctor_UpdatesVitalsAndPrescriptions()
    {
        var (db, _, service, doctor, _, appointment) = SetupEnvironment();

        // Initial save
        var saveDto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            BloodPressure = "120/80",
            HeartRate = 70,
            Diagnosis = "Initial Diagnosis"
        };
        var initialRecord = await service.SaveVisitAsync(saveDto);

        // Update
        var updateDto = new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            BloodPressure = "118/78",
            HeartRate = 68,
            Diagnosis = "Updated Diagnosis",
            Prescriptions = new List<PrescriptionItemDto>
            {
                new() { Medication = "Lisinopril", Dosage = "10 mg", Frequency = "Once daily" }
            }
        };

        var updatedResult = await service.UpdateVisitAsync(initialRecord.Id, updateDto);

        Assert.Equal("118/78 mmHg", updatedResult.BloodPressure);
        Assert.Equal("Updated Diagnosis", updatedResult.Diagnosis);
        Assert.NotNull(updatedResult.UpdatedAt);
        Assert.Single(updatedResult.Prescriptions);

        var recordInDb = await db.MedicalRecords
            .Include(m => m.Prescription).ThenInclude(p => p!.Items)
            .FirstOrDefaultAsync(m => m.Id == initialRecord.Id);

        Assert.NotNull(recordInDb);
        Assert.Equal("118/78 mmHg", recordInDb.BloodPressure);
        Assert.Single(recordInDb.Prescription!.Items);
    }

    [Fact]
    public async Task GetRecordByIdAsync_AdminRole_ThrowsUnauthorizedAccessException()
    {
        var (_, _, service, doctor, _, appointment) = SetupEnvironment();

        var record = await service.SaveVisitAsync(new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            Diagnosis = "Confidential"
        });

        // Admin attempting to read clinical record (BR-11, NFR-06)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetRecordByIdAsync(record.Id, "admin-user-id", "Admin"));
    }

    [Fact]
    public async Task GetRecordByIdAsync_OtherPatient_ThrowsUnauthorizedAccessException()
    {
        var (_, _, service, doctor, _, appointment) = SetupEnvironment();

        var record = await service.SaveVisitAsync(new SaveMedicalRecordDto
        {
            AppointmentId = appointment.Id,
            DoctorUserId = doctor.UserId,
            Diagnosis = "Confidential"
        });

        // Other patient trying to access
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetRecordByIdAsync(record.Id, "unauthorized-patient-id", "Patient"));
    }

    [Fact]
    public async Task GetPatientMedicalProfileForDoctorAsync_DoctorWithNoAppointment_ThrowsUnauthorizedAccessException()
    {
        var (db, _, service, _, patient, _) = SetupEnvironment();

        // Create Doctor 2 who has no appointments with this patient
        var doc2User = new ApplicationUser { Id = "doc-2-user", FullName = "Dr. Other" };
        var doc2 = new Doctor { Id = 2, UserId = "doc-2-user", User = doc2User, Location = "Cairo", ConsultationFee = 300 };
        db.Users.Add(doc2User);
        db.Doctors.Add(doc2);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetPatientMedicalProfileForDoctorAsync(patient.Id, doc2.UserId));
    }

    [Fact]
    public async Task UpdatePatientProfileAsync_ValidBloodGroup_UpdatesPatientProfile()
    {
        var (db, _, service, _, patient, _) = SetupEnvironment();

        var updateDto = new UpdatePatientProfileDto
        {
            PatientUserId = patient.UserId,
            PhoneNumber = "+201099887766",
            BloodGroup = "AB+",
            Allergies = "Sulfa drugs",
            MedicalHistory = "Mild asthma"
        };

        var success = await service.UpdatePatientProfileAsync(updateDto);

        Assert.True(success);

        var patientInDb = await db.Patients.FindAsync(patient.Id);
        Assert.NotNull(patientInDb);
        Assert.Equal("+201099887766", patientInDb.PhoneNumber);
        Assert.Equal("AB+", patientInDb.BloodGroup);
        Assert.Equal("Sulfa drugs", patientInDb.Allergies);
        Assert.Equal("Mild asthma", patientInDb.MedicalHistory);
    }

    [Fact]
    public async Task UpdatePatientProfileAsync_InvalidBloodGroup_ThrowsArgumentException()
    {
        var (_, _, service, _, patient, _) = SetupEnvironment();

        var updateDto = new UpdatePatientProfileDto
        {
            PatientUserId = patient.UserId,
            BloodGroup = "XYZ"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdatePatientProfileAsync(updateDto));
    }
}
