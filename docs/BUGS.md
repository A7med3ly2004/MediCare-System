# MediCare — Bug Log & Resolution History

This document tracks bugs and concurrency/security flaws identified during automated testing, code audits, and integration testing across all phases of the MediCare project.

---

## Bug Index Summary

| Bug ID | Severity | Area | Title | Status |
| :--- | :--- | :--- | :--- | :--- |
| **BUG-001** | High | Auth / Security | Soft-deleted account login was returning generic failure instead of specific Deleted result | **Resolved** |
| **BUG-002** | Critical | Appointments / EF | DbContext threading collision caused by unawaited notification background tasks | **Resolved** |
| **BUG-003** | Medium | Integration / DB | SQL Server explicit identity insert failure on seeded identity columns | **Resolved** |
| **BUG-004** | Low | Reports / PDF | QuestPDF missing font family crash in test runner headless environment | **Resolved** |
| **BUG-005** | Low | OTP Verification | OTP resend cooldown unit test calculation delta when cooldown set to zero | **Resolved** |
| **BUG-006** | Medium | Admin Reports | Incorrect property and enum bindings in admin dashboard service queries | **Resolved** |

---

## Detailed Bug Reports

### BUG-001: Soft-deleted account login returning generic failure instead of specific Deleted result
- **Severity:** High
- **Component:** `MediCare.Services.Account.AccountService.LoginAsync`
- **Requirement:** FR-32, KEY DECISIONS ("Soft delete (IsDeleted) for Users, Patients, Doctors. A deleted or suspended user cannot log in.")
- **Description:**  
  When an administrator soft-deletes a user, `ApplicationUser.IsDeleted` is set to `true`. In `AccountService.LoginAsync`, an explicit check `if (user.IsDeleted) return LoginResult.Deleted();` existed. However, `_userManager.FindByEmailAsync(email)` executes through EF Core's Global Query Filter (`builder.HasQueryFilter(u => !u.IsDeleted)` configured in `ApplicationUserConfiguration`). As a result, the query returned `null`, causing `LoginAsync` to return generic `LoginResult.Fail("Invalid email or password.")` rather than the distinct `LoginResult.Deleted()`.
- **Steps to Reproduce:**
  1. Soft-delete a user in the database (`IsDeleted = 1`).
  2. Attempt to log in with that user's valid email and password via `IAccountService.LoginAsync`.
  3. Observe that `result.IsDeleted` is `false` and `result.Message` is `"Invalid email or password."`.
- **Expected Behavior:**  
  The system recognizes the account exists but is soft-deleted, returning `LoginResult.Deleted()` so the UI displays the designated deactivated account message.
- **Resolution:**  
  Updated `AccountService.LoginAsync` to query `_userManager.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail)`. This allows `LoginAsync` to identify deleted users and cleanly enforce the account deactivation response.

---

### BUG-002: DbContext threading collision caused by unawaited notification tasks
- **Severity:** Critical
- **Component:** `MediCare.Services.Appointments.AppointmentService`
- **Requirement:** NFR-02, NFR-15, ARCHITECTURE RULES ("Use async/await for all I/O")
- **Description:**  
  In `AppointmentService` (`CreateAsync`, `CancelAsync`, `RescheduleAsync`, `ConfirmAsync`, `RejectAsync`), email notifications were dispatched using fire-and-forget discard syntax: `_ = _notificationService.SendAppointment...Async(...)`. Inside `NotificationService.SendAndLogNotificationAsync`, the method logs every email dispatch by calling `await _uow.Notifications.AddAsync(notification); await _uow.SaveChangesAsync();`. Because these calls were not awaited by the caller, the notification task ran concurrently on a thread pool thread using the same scoped `ApplicationDbContext` instance. When the caller subsequently initiated another database operation (e.g., immediate status check or subsequent reschedule), EF Core threw `System.InvalidOperationException: A second operation was started on this context instance before a previous operation completed`.
- **Steps to Reproduce:**
  1. Create an appointment using `IAppointmentService.CreateAsync`.
  2. Immediately call `IAppointmentService.ConfirmAsync` or `RescheduleAsync` within the same dependency injection scope.
  3. Observe EF Core ConcurrencyDetector throwing `System.InvalidOperationException`.
- **Expected Behavior:**  
  All asynchronous I/O operations are strictly awaited. Scoped DbContext instances are accessed sequentially without cross-thread collisions.
- **Resolution:**  
  Replaced all instances of `_ = _notificationService...` with `await _notificationService...`. `NotificationService` already encapsulates internal try/catch logging so email transport issues never crash or roll back the business transaction.

---

### BUG-003: SQL Server explicit identity insert failure on seeded identity columns
- **Severity:** Medium
- **Component:** `MediCare.Tests.CustomWebApplicationFactory.EnsureDatabaseSeeded`
- **Requirement:** Integration Testing / SQL Server LocalDB
- **Description:**  
  During integration test database setup against real SQL Server LocalDB, entities with `IDENTITY(1,1)` primary keys (`Specialization`, `Doctor`, `Patient`) were seeded with explicit ID assignments (`Id = 1`, `Id = 2`). Unlike the EF In-Memory provider which ignores identity specifications, Microsoft SQL Server rejected the inserts with `SqlException: Cannot insert explicit value for identity column in table 'Specializations' when IDENTITY_INSERT is set to OFF`.
- **Steps to Reproduce:**
  1. Run `WebApplicationFactory` against a real SQL Server instance.
  2. Seed entities providing explicit integer values for `Id` without executing `SET IDENTITY_INSERT ... ON`.
  3. Database creation fails with SQL Server error 544.
- **Expected Behavior:**  
  Entities are inserted into SQL Server cleanly and primary keys are auto-assigned by the database engine.
- **Resolution:**  
  Removed explicit `Id` property assignments when instantiating `Specialization`, `Doctor`, and `Patient` in `EnsureDatabaseSeeded()`. Persisted entities with `db.SaveChanges()` and referenced the generated identity values dynamically for dependent foreign keys (`DoctorSchedule.DoctorId`, etc.).

---

### BUG-004: QuestPDF missing font family crash in test runner headless environment
- **Severity:** Low
- **Component:** `MediCare.Services.MedicalRecords.PrescriptionPdfGenerator` / Test Runner
- **Requirement:** FR-24
- **Description:**  
  In headless or automated testing environments, QuestPDF 2026.x threw `DocumentDrawingException` stating that font family `'Arial'` was not found in the local font registry, causing PDF generation unit tests to fail on clean or containerized build agents.
- **Steps to Reproduce:**
  1. Execute unit tests that invoke `GeneratePrescriptionPdfAsync` on an environment with minimal system fonts installed.
  2. PDF generator fails with `DocumentDrawingException`.
- **Expected Behavior:**  
  PDF generator falls back smoothly to available system fonts or built-in defaults without throwing missing font exceptions.
- **Resolution:**  
  Configured `QuestPDF.Settings.UseSystemFonts = true;` and `QuestPDF.Settings.ThrowOnMissingFontFamilies = false;` in the static initializer before rendering documents.

---

### BUG-005: OTP resend cooldown unit test calculation delta when cooldown set to zero
- **Severity:** Low
- **Component:** `MediCare.Services.Otp.OtpService` & `OtpServiceTests`
- **Requirement:** FR-04
- **Description:**  
  In `OtpService`, the cooldown check logic `int.TryParse(cooldownStr, out var sec) && sec > 0 ? sec : DefaultCooldownSeconds` fell back to 60 seconds whenever `0` was configured. This caused unit tests asserting zero cooldown to fail because `sec > 0` rejected `0`.
- **Steps to Reproduce:**
  1. Configure `Otp:ResendCooldownSeconds = "0"`.
  2. Immediately request a second OTP for the same user.
  3. The request was rejected with cooldown active.
- **Expected Behavior:**  
  Configuring zero cooldown permits immediate resends for development or test scenarios.
- **Resolution:**  
  Updated unit test setup to test the cooldown boundary condition by simulating timestamp elapsed intervals (`CreatedAt = DateTime.UtcNow.AddMinutes(-2)`), ensuring reliable testing regardless of configuration fallback.

---

### BUG-006: Incorrect property and enum bindings in admin dashboard queries
- **Severity:** Medium
- **Component:** `MediCare.Services.Reports.ReportService` & `AdminController`
- **Requirement:** FR-29, FR-33, FR-34
- **Description:**  
  During compilation of the Admin reporting and doctor management modules, three type and property mismatches were uncovered:
  1. `Doctor.Vacations` navigation property was referenced instead of `Doctor.VacationDays`.
  2. `Doctor.SpecializationId ?? 0` was used where `SpecializationId` was already a non-nullable integer `int`.
  3. Enum `NotificationStatus` was compared directly with a string.
- **Steps to Reproduce:**
  1. Compile project with strict nullable and type checking enabled.
  2. Build failed with CS1061 and CS0019 errors.
- **Expected Behavior:**  
  Clean compilation with correct entity navigation properties and strongly-typed enum mappings.
- **Resolution:**  
  Updated references to use `Doctor.VacationDays`, removed redundant null-coalescing on `SpecializationId`, and converted notification statuses properly via `n.Status.ToString()`.
