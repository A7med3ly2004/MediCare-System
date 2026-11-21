# MediCare — Requirements Traceability Matrix (SPEC.md)

This document provides complete traceability for all **35 Functional Requirements (FR-01 to FR-35)** and **19 Non-Functional Requirements (NFR-01 to NFR-19)** specified in [SPEC.md](file:///d:/My%20partion/Full%20stack%20.Net%20Web%20Developer/Final%20Project/project%20implementation/MediCare/docs/SPEC.md).

---

## 1. Functional Requirements (FR-01 to FR-35)

| Req ID | Requirement Description | Priority | Status | Implementation File / Page | Test Coverage |
| :---: | :--- | :---: | :---: | :--- | :--- |
| **FR-01** | Patient Registration with DOB, Blood Group, Gender | Must | **Done** | `AccountController.RegisterPatient`<br>`Views/Account/RegisterPatient.cshtml` | `AccountControllerTests` |
| **FR-02** | Doctor Registration with Specialization, Room, Fee, Photo | Must | **Done** | `AccountController.RegisterDoctor`<br>`Views/Account/RegisterDoctor.cshtml` | `AccountControllerTests` |
| **FR-03** | Mandatory Privacy Notice Acceptance checkbox | Must | **Done** | `RegisterPatientViewModel`<br>`RegisterDoctorViewModel` | `AccountControllerTests` |
| **FR-04** | Email Verification via 6-digit OTP (10 min expiry, 5 attempts, cooldown) | Must | **Done** | `OtpService.cs`<br>`AccountController.VerifyOtp` | `OtpServiceTests`<br>`EmailNotificationTests` |
| **FR-05** | Secure Login & Logout with Lockout protection | Must | **Done** | `AccountController.Login`<br>`AccountController.Logout` | `AccountServiceTests`<br>`AccessControlTests` |
| **FR-06** | Remember Me on device & Password Change | Should | **Done** | `LoginViewModel.RememberMe`<br>`AccountController.cs` | `AccessControlTests` |
| **FR-07** | Doctor Directory search & filter (Specialization, Location, Date, Fee) | Must | **Done** | `DoctorService.SearchDoctorsAsync`<br>`Views/Doctors/Index.cshtml` | `DoctorServiceTests` |
| **FR-08** | Doctor Details profile page with available slot calendar | Must | **Done** | `DoctorsController.Details`<br>`Views/Doctors/Details.cshtml` | `DoctorServiceTests` |
| **FR-09** | Doctor edits own profile (fee, room, location, photo) | Must | **Done** | `DoctorService.UpdateProfileAsync`<br>`Views/Doctor/Profile.cshtml` | `DoctorServiceTests` |
| **FR-10** | Doctor listed only if verified, active, not deleted, and profile complete | Must | **Done** | `DoctorService.GetSearchQuery` | `DoctorServiceTests` |
| **FR-11** | Doctor weekly working hours and vacation days management | Must | **Done** | `ScheduleService.cs`<br>`Views/Doctor/Schedule.cshtml` | `ScheduleServiceTests` |
| **FR-12** | Patient books appointment in free slot | Must | **Done** | `AppointmentService.CreateAsync`<br>`Views/Doctors/Details.cshtml` | `AppointmentServiceTests`<br>`ConcurrencyTests` |
| **FR-13** | Patient views appointment list (upcoming & past) | Must | **Done** | `PatientController.Appointments`<br>`Views/Patient/Appointments.cshtml` | `AppointmentServiceTests` |
| **FR-14** | Doctor confirms or rejects pending appointment requests | Must | **Done** | `AppointmentService.ConfirmAsync`<br>`AppointmentService.RejectAsync` | `AppointmentServiceTests` |
| **FR-15** | Patient reschedules appointment (lead time >= 2h, returns to Pending) | Must | **Done** | `AppointmentService.RescheduleAsync`<br>`Views/Patient/Appointments.cshtml` | `AppointmentServiceTests` |
| **FR-16** | Patient/Admin cancels appointment (lead time >= 2h, frees slot) | Must | **Done** | `AppointmentService.CancelAsync`<br>`Views/Patient/Appointments.cshtml` | `AppointmentServiceTests` |
| **FR-17** | Double-booking prevention via filtered unique database index | Must | **Done** | `IX_Appointments_DoctorId_AppointmentDate_StartTime`<br>`AppointmentConfiguration.cs` | `ConcurrencyTests` (20 parallel threads) |
| **FR-18** | Doctor dashboard with today's appointments and pending requests | Must | **Done** | `DoctorController.Index`<br>`Views/Doctor/Index.cshtml` | `DoctorServiceTests` |
| **FR-19** | Concurrency protection on appointment decisions via RowVersion token | Must | **Done** | `Appointment.RowVersion`<br>`AppointmentService.ConfirmAsync` | `AppointmentServiceTests` |
| **FR-20** | Doctor records visit; marks appointment Completed in single transaction | Must | **Done** | `MedicalRecordService.SaveVisitAsync`<br>`Views/Doctor/RecordVisit.cshtml` | `MedicalRecordServiceTests` |
| **FR-21** | Doctor edits existing medical record; preserves Completed status | Must | **Done** | `MedicalRecordService.SaveVisitAsync` | `MedicalRecordServiceTests` |
| **FR-22** | Prescription lines (Medication, Dosage, Frequency) | Must | **Done** | `Prescription.cs`<br>`PrescriptionItem.cs` | `MedicalRecordServiceTests` |
| **FR-23** | Patient views personal medical records and prescriptions | Must | **Done** | `PatientController.Health`<br>`Views/Patient/MedicalHistory.cshtml` | `MedicalRecordServiceTests`<br>`AccessControlTests` |
| **FR-24** | Print/Download Prescription PDF (branded clinic Rx sheet) | Should | **Done** | `PrescriptionPdfGenerator.cs`<br>`PatientController.DownloadPrescriptionPdf` | `PrescriptionPdfGeneratorTests` |
| **FR-25** | Doctor views patient medical history and known allergies for their appointments | Must | **Done** | `MedicalRecordService.GetPatientClinicalContextAsync` | `MedicalRecordServiceTests` |
| **FR-26** | Automated booking confirmation email sent to patient and doctor | Must | **Done** | `NotificationService.SendBookingReceived...`<br>`NotificationFactory.cs` | `EmailNotificationTests` |
| **FR-27** | Automated confirmation or rejection email sent upon doctor decision | Must | **Done** | `NotificationService.SendAppointmentConfirmed...`<br>`NotificationService.SendAppointmentRejected...` | `EmailNotificationTests` |
| **FR-28** | Automated reschedule or cancellation email sent to both parties | Must | **Done** | `NotificationService.SendAppointmentRescheduled...`<br>`NotificationService.SendAppointmentCancelled...` | `EmailNotificationTests` |
| **FR-29** | Private Admin area restricted to Admin role on server side | Must | **Done** | `AdminController.cs`<br>`[Authorize(Roles = "Admin")]` | `AccessControlTests` |
| **FR-30** | Admin Daily Report for selected date (registrations, bookings, cancels) | Must | **Done** | `ReportService.GetDailyReportAsync`<br>`Views/Admin/Index.cshtml` | `ReportServiceTests` |
| **FR-31** | Admin analytics dashboard with Chart.js charts (12-month trends, popular doctors/specialties) | Must | **Done** | `ReportService.GetDashboardAnalyticsAsync`<br>`Views/Admin/Index.cshtml` | `ReportServiceTests` |
| **FR-32** | Admin manages doctors and patients (edit, suspend/unsuspend, soft-delete with conflict check) | Must | **Done** | `AdminController.Doctors`<br>`AdminController.Patients` | `AdminManagementTests` |
| **FR-33** | Admin manages medical specializations (add, update, delete unused only) | Must | **Done** | `AdminController.Specializations`<br>`Views/Admin/Specializations.cshtml` | `AdminManagementTests` |
| **FR-34** | Admin oversight: view, confirm, reject, or cancel any clinic appointment | Must | **Done** | `AdminController.Appointments`<br>`Views/Admin/Appointments.cshtml` | `AdminManagementTests` |
| **FR-35** | Export operational reports to formatted PDF and Excel (`.xlsx`) | Should | **Done** | `DailyReportPdfGenerator.cs`<br>`ReportExcelExporter.cs`<br>`AdminController.Export...` | `ReportExportTests` |

---

## 2. Non-Functional Requirements (NFR-01 to NFR-19)

| Req ID | Category | Requirement Description | Status | Implementation & Validation |
| :---: | :--- | :--- | :---: | :--- |
| **NFR-01** | Performance | Page load under 2s for 95% of requests | **Done** | Indexed SQL Server queries, server-rendered lightweight Razor markup, and bundled CSS/JS. |
| **NFR-02** | Performance | Admin queries < 5s with 10k sample records | **Done** | Database GROUP BY aggregations, pagination via `Skip/Take`, and filtered indexes. |
| **NFR-03** | Performance | OTP email delivery within 1 minute | **Done** | Asynchronous email transport through SendGrid HTTP API and optimized HTML templates. |
| **NFR-04** | Security | Identity password hashing with PBKDF2 | **Done** | ASP.NET Core Identity `PasswordHasher<ApplicationUser>` with unique cryptographic salts. |
| **NFR-05** | Security | HTTPS-only and HSTS enforcement | **Done** | `app.UseHsts()` with 365 days preload, `UseHttpsRedirection()`, and `CookieSecurePolicy.Always`. |
| **NFR-06** | Security | Server-side authorization and data ownership | **Done** | Server-side user ID comparison on all medical records and appointments (`AccessControlTests`). |
| **NFR-07** | Security | Attack mitigation: Anti-forgery, parameterized queries, encoding | **Done** | Global `AutoValidateAntiforgeryTokenAttribute`, EF Core parameterized SQL, and Razor HTML encoding. |
| **NFR-08** | Security | Zero secrets committed to source repository (KPI-07) | **Done** | Secrets read from `dotnet user-secrets` locally and Azure App Settings in production. |
| **NFR-09** | Privacy | Mandatory Privacy Notice consent | **Done** | Required checkbox validation during patient and doctor registration. |
| **NFR-10** | Usability | Responsive on phone, tablet, and desktop | **Done** | Bootstrap 5 grid, responsive sidebar drawer, and fluid typography. |
| **NFR-11** | Usability | Unassisted appointment booking under 2 minutes (KPI-02) | **Done** | Streamlined 2-step booking workflow from doctor profile (`UAT-PLAN.md` Scenario 3). |
| **NFR-12** | Usability | Lighthouse accessibility score 90+; Dark mode | **Done** | WCAG AA color contrast, label associations, keyboard accessibility, and CSS Dark Mode toggle. |
| **NFR-13** | Usability | Clear validation messages adjacent to failing inputs | **Done** | ASP.NET validation tags (`span asp-validation-for="..."`) rendered next to each field. |
| **NFR-14** | Reliability | 99% uptime availability monitoring | **Done** | Azure Application Insights Standard Availability ping tests configured in `docs/AZURE-SETUP.md`. |
| **NFR-15** | Reliability | Atomic database transactions via Unit of Work | **Done** | Booking, cancelling, and visit recording wrapped in single Unit of Work transaction commits. |
| **NFR-16** | Reliability | Friendly error page with logging (404, 403, 500) | **Done** | Custom branded `Home/Error.cshtml` with status codes and unhandled exception logging. |
| **NFR-17** | Maintainability | Clean N-Tier architecture (Web -> Services -> Data) | **Done** | Strict one-way layer dependencies, Repository, Unit of Work, Factory, and Dependency Injection. |
| **NFR-18** | Maintainability | GitHub feature branches, PR reviews, README | **Done** | Trunk-based Git workflow, PR template, CI/CD workflows, and comprehensive `README.md`. |
| **NFR-19** | Compatibility | Modern browser support (Chrome, Edge, Firefox) | **Done** | Standard HTML5, CSS custom properties, and cross-browser Bootstrap 5 components. |

---

## 3. Should-Have Requirements Status Summary

All Should-Have items specified in SPEC.md were evaluated and implemented:

1. **PDF Export (FR-35, FR-24):** **IMPLEMENTED** — Branded prescription sheets and daily operational reports generated via QuestPDF.
2. **Excel Export (FR-35):** **IMPLEMENTED** — Formatted multi-column `.xlsx` appointment reports generated via ClosedXML.
3. **Dark / Light Mode Toggle (NFR-12):** **IMPLEMENTED** — Built with CSS variables (`--mc-canvas`, `--mc-surface`, etc.), navbar toggle button, and persistent browser storage.
4. **Appointment Reminders (FR-28 Should):** **IMPLEMENTED** — Notification factory supports `CreateReminderEmail`; email dispatch logged in `Notifications` table.
