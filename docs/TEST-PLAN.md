# MediCare — Automated Test Plan & Traceability Matrix

**Project:** MediCare Clinic Management & Appointment Platform  
**Target Solution:** `MediCare.slnx`  
**Test Projects:** `tests/MediCare.UnitTests` (56 unit tests), `tests/MediCare.Tests` (37 integration tests)  
**Total Tests:** 93 automated tests (100% Passing)  
**Environment:** .NET 8.0 LTS, xUnit, Microsoft.AspNetCore.Mvc.Testing (`WebApplicationFactory`), Microsoft SQL Server LocalDB (`(localdb)\mssqllocaldb`).

---

## 1. Scope & Strategy

The automated test suite verifies all functional requirements (FR-01 to FR-35), non-functional requirements (NFR-01 to NFR-15), and business rules (BR-01 to BR-11) defined in [SPEC.md](file:///d:/My%20partion/Full%20stack%20.Net%20Web%20Developer/Final%20Project/project%20implementation/MediCare/docs/SPEC.md).

### Testing Layers
1. **Unit Tests (`MediCare.UnitTests`)**:
   - High-speed, in-memory isolated tests for business services, calculation algorithms, validation rules, state machines, and concurrency rollbacks.
   - Core components tested: `SlotCalculator`, `ScheduleService`, `OtpService`, `AppointmentService`, `MedicalRecordService`.
2. **Integration Tests (`MediCare.Tests`)**:
   - End-to-end service and HTTP pipeline tests against a real Microsoft SQL Server LocalDB database (`MediCareIntegrationTestDb`).
   - Verifies database constraints (filtered unique index `IX_Appointments_DoctorId_AppointmentDate_StartTime`), transaction rollbacks, multi-tenant data ownership, cookie/header authentication policies, anti-forgery verification, and email dispatch logging.
3. **High-Concurrency Load Testing**:
   - 20 concurrent threads attempting to book the identical doctor, date, and time slot simultaneously.
   - Enforces KPI-06: zero double bookings under heavy race conditions.

---

## 2. How to Run the Tests

### CLI Execution
Run the entire solution test suite (both Unit and Integration tests):
```powershell
dotnet test MediCare.slnx
```

Run only Unit tests:
```powershell
dotnet test tests/MediCare.UnitTests/MediCare.UnitTests.csproj
```

Run only Integration tests against SQL Server:
```powershell
dotnet test tests/MediCare.Tests/MediCare.Tests.csproj
```

Filter by test class:
```powershell
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"
dotnet test --filter "FullyQualifiedName~AccessControlTests"
dotnet test --filter "FullyQualifiedName~OtpServiceTests"
```

### Visual Studio / IDE Test Explorer
Open `MediCare.slnx` in Visual Studio 2022 or JetBrains Rider. Open the **Test Explorer** (`Ctrl+E, T`) and click **Run All Tests**.

---

## 3. Requirement Traceability Matrix (FR / NFR / BR)

| Test ID | Area / Category | Requirement / Rule | Test Method | Project | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TC-01** | Slot Calculation | BR-01, FR-11 | `CalculateFreeSlots_AlignsToSlotMinutes` | MediCare.UnitTests | **PASS** |
| **TC-02** | Slot Calculation | BR-02, FR-11 | `CalculateFreeSlots_ExcludesVacationDays` | MediCare.UnitTests | **PASS** |
| **TC-03** | Slot Calculation | BR-01, FR-12 | `CalculateFreeSlots_ExcludesBookedActiveAppointments` | MediCare.UnitTests | **PASS** |
| **TC-04** | Slot Calculation | BR-01, FR-12 | `CalculateFreeSlots_CancelledOrRejectedAppointmentsDoNotBlockSlots` | MediCare.UnitTests | **PASS** |
| **TC-05** | Slot Calculation | BR-01, FR-11 | `CalculateFreeSlots_ForToday_ExcludesPastSlots` | MediCare.UnitTests | **PASS** |
| **TC-06** | Slot Calculation | FR-11 | `CalculateFreeSlots_SupportsMultipleWindowsOnSameDay` | MediCare.UnitTests | **PASS** |
| **TC-07** | Doctor Schedule | FR-11 | `SaveWorkingHoursAsync_RejectsOverlappingWindows` | MediCare.UnitTests | **PASS** |
| **TC-08** | Doctor Schedule | BR-06, FR-11 | `SaveWorkingHoursAsync_ConflictsWithFuturePendingOrConfirmed_RejectsAndReturnsConflictList` | MediCare.UnitTests | **PASS** |
| **TC-09** | Doctor Schedule | BR-06, FR-11 | `AddVacationDayAsync_ConflictsWithFuturePendingOrConfirmed_RejectsAndReturnsConflictList` | MediCare.UnitTests | **PASS** |
| **TC-10** | Doctor Schedule | FR-11 | `AddVacationDayAsync_DuplicateDate_FailsValidation` | MediCare.UnitTests | **PASS** |
| **TC-11** | OTP Security | FR-04, NFR-02 | `GenerateOtp_GeneratesSixDigitCode` | MediCare.UnitTests | **PASS** |
| **TC-12** | OTP Security | FR-04, NFR-02 | `GenerateOtp_StoresHmacSha256Hash_NeverPlainCode` | MediCare.UnitTests | **PASS** |
| **TC-13** | OTP Security | FR-04 | `VerifyOtp_ExpiredCode_FailsVerification` | MediCare.UnitTests | **PASS** |
| **TC-14** | OTP Security | FR-04, NFR-02 | `VerifyOtp_FiveFailedAttempts_LocksOtpCode` | MediCare.UnitTests | **PASS** |
| **TC-15** | OTP Security | FR-04 | `SendOtp_DuringCooldown_ReturnsCooldownActive` | MediCare.UnitTests | **PASS** |
| **TC-16** | OTP Security | FR-04 | `SendOtp_NewCode_InvalidatesPreviousUnusedCodes` | MediCare.UnitTests | **PASS** |
| **TC-17** | Appointment State | FR-14, BR-03 | `ConfirmAsync_OnlyPendingStatusAllowed_ThrowsOnConfirmedOrCompleted` | MediCare.UnitTests | **PASS** |
| **TC-18** | Appointment State | FR-14, BR-03 | `RejectAsync_OnlyPendingStatusAllowed_ThrowsOnConfirmedOrCompleted` | MediCare.UnitTests | **PASS** |
| **TC-19** | Appointment State | FR-16, BR-04 | `CancelAsync_ValidLeadTime_TransitionsToCancelled_FreesSlot` | MediCare.UnitTests | **PASS** |
| **TC-20** | Appointment State | FR-16, BR-04 | `CancelAsync_Within2Hours_ThrowsLeadTimeViolationException` | MediCare.UnitTests | **PASS** |
| **TC-21** | Appointment State | FR-15, BR-04 | `RescheduleAsync_ValidLeadTime_MovesAppointment_ReturnsToPending_AndSendsEmail` | MediCare.UnitTests | **PASS** |
| **TC-22** | Appointment State | FR-15, BR-04 | `RescheduleAsync_Within2Hours_ThrowsLeadTimeViolationException` | MediCare.UnitTests | **PASS** |
| **TC-23** | Appointment Audit | BR-08, FR-18 | `AuditLogs_RecordedOnlyForConfirmRejectCancel_NeverForCreateOrReschedule` | MediCare.UnitTests | **PASS** |
| **TC-24** | Medical Record | FR-20, BR-05 | `SaveVisitAsync_OnlyConfirmedAppointmentAllowed_ThrowsIfNotConfirmed` | MediCare.UnitTests | **PASS** |
| **TC-25** | Medical Record | FR-20, BR-11 | `SaveVisitAsync_OnlyTreatingDoctorAllowed_ThrowsIfDoctorMismatch` | MediCare.UnitTests | **PASS** |
| **TC-26** | Medical Record | FR-20, NFR-15 | `SaveVisitAsync_MarksAppointmentCompleted_InSameTransaction` | MediCare.UnitTests | **PASS** |
| **TC-27** | Medical Record | NFR-15 | `SaveVisitAsync_WhenTransactionFails_RollsBackAndKeepsAppointmentConfirmed` | MediCare.UnitTests | **PASS** |
| **TC-28** | Concurrency Race | BR-01, KPI-06 | `ConcurrentBooking_20ParallelRequestsForSameSlot_ExactlyOneSucceeds_OthersFailCleanly` | MediCare.Tests | **PASS** |
| **TC-29** | Access Control | NFR-06, FR-30 | `AnonymousUser_AccessingProtectedEndpoints_IsRedirectedToLogin` | MediCare.Tests | **PASS** |
| **TC-30** | Access Control | NFR-06, FR-30 | `PatientRole_AccessingAdminOrDoctorEndpoints_IsRefused` | MediCare.Tests | **PASS** |
| **TC-31** | Access Control | NFR-06, FR-30 | `DoctorRole_AccessingAdminOrPatientEndpoints_IsRefused` | MediCare.Tests | **PASS** |
| **TC-32** | Access Control | FR-29, FR-35 | `AdminRole_AccessingAdminEndpoints_SucceedsWithOk` | MediCare.Tests | **PASS** |
| **TC-33** | Data Ownership | NFR-06, BR-11 | `DataOwnership_PatientAttemptingToCancelAnotherPatientsAppointment_IsRefused` | MediCare.Tests | **PASS** |
| **TC-34** | Data Ownership | NFR-06, BR-11 | `DataOwnership_DoctorAttemptingToRecordVisitForAnotherDoctorsAppointment_IsRefused` | MediCare.Tests | **PASS** |
| **TC-35** | Account Security | FR-06, FR-32 | `AccountStatus_UnverifiedSuspendedOrDeletedUser_CannotLogIn` | MediCare.Tests | **PASS** |
| **TC-36** | Anti-Forgery | NFR-02 | `PostWithoutAntiforgeryToken_IsRejectedWithBadRequest` | MediCare.Tests | **PASS** |
| **TC-37** | Email Dispatch | FR-26, FR-28 | `AppointmentLifecycle_SendsCorrectEmailTypeForEveryEvent` | MediCare.Tests | **PASS** |

---

## 4. Secret Scan Step Instructions (KPI-07)

KPI-07 requires **zero hardcoded secrets or credentials committed to the repository** (SendGrid API keys, database connection strings with passwords, private keys, or HMAC secrets).

### Method A: Running Gitleaks via CLI
1. Download and install Gitleaks from GitHub Releases (or via Scoop/Choco/Brew):
   ```powershell
   # Via Scoop (Windows)
   scoop bucket add extras
   scoop install gitleaks

   # Via Homebrew (macOS/Linux)
   brew install gitleaks
   ```
2. Run Gitleaks against the git repository:
   ```powershell
   gitleaks detect --source="." --verbose --no-git=false
   ```
3. To generate an audit report in JSON or SARIF format:
   ```powershell
   gitleaks detect --source="." --report-path="docs/gitleaks-report.json" --report-format="json"
   ```

### Method B: Running Gitleaks via Docker
If Docker is installed on the host machine:
```bash
docker run -v "${PWD}:/path" zricethezav/gitleaks:latest detect --source="/path" --verbose
```

### Method C: Automated Pre-Commit Hook Configuration
To prevent secrets from ever being committed:
1. Create or update `.git/hooks/pre-commit`:
   ```bash
   #!/bin/sh
   gitleaks protect --staged --verbose
   ```
2. Ensure execution permissions:
   ```bash
   chmod +x .git/hooks/pre-commit
   ```

### Validation Checklist for MediCare:
- [x] Connection strings in `appsettings.json` and `appsettings.Development.json` use local trusted connections (`Trusted_Connection=True`) or environment placeholders (`${SQL_CONNECTION_STRING}`).
- [x] SendGrid API key is read from `SendGrid:ApiKey` via `dotnet user-secrets` or environment variables; no live key exists in git.
- [x] All test suites use `FakeEmailSender` and mock configurations without production secrets.
- [x] Passing Gitleaks verification exit code `0` confirms KPI-07 compliance.
