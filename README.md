# MediCare — Clinic Management & Appointment Platform

MediCare is an enterprise-grade, web-based clinic management and appointment scheduling platform built with **ASP.NET Core MVC (.NET 8 LTS)**, **Entity Framework Core**, **ASP.NET Identity**, and **Microsoft SQL Server / Azure SQL** in an **N-Tier Clean Architecture**.

---

## 🌟 Key Features by Role

### 👤 Patient Portal
- **Secure Registration & OTP Verification:** Cryptographically secure 6-digit OTP email verification with 10-minute expiry, 5-attempt rate-limit lockout, and 60-second resend cooldown.
- **Doctor Directory & Smart Search:** Filter active verified doctors by medical specialization, clinic location, consultation fee, and real-time weekday/vacation availability.
- **Fast 2-Minute Booking (KPI-02):** Interactive slot booking with zero double-booking guarantee enforced by a filtered unique database index.
- **Appointment Self-Service:** View upcoming and past visits, reschedule appointments back to Pending status, or cancel with a minimum 2-hour lead time (BR-04).
- **Electronic Health Records (EHR):** Access past clinical visits, vital signs, diagnosis notes, and download branded prescription PDFs generated via QuestPDF.

### 🩺 Doctor Workspace
- **Practice & Profile Management:** Edit consultation fees, clinic room numbers, specializations, and profile photos.
- **Weekly Schedule & Vacation Planner:** Define recurring weekly working windows and vacation dates with automated conflict detection preventing modifications if active appointments exist (BR-06).
- **Doctor Dashboard:** View today’s agenda, manage pending booking requests with optimistic concurrency tokens (RowVersion), and review patient medical histories.
- **Clinical Visit Recording:** Document vitals (blood pressure, heart rate), symptoms, diagnosis, and issue multi-line electronic prescriptions; saving automatically marks the appointment as Completed in a single atomic transaction (NFR-15).

### 🛡️ Administrator Operations
- **Executive Analytics Dashboard:** Interactive Chart.js visualizations for 12-month appointment volumes, doctor utilization, popular specialties, and patient registration trends.
- **Daily Operations Report:** Filterable daily summary of new registrations, bookings, confirmations, and cancellations with zero-fill support.
- **Staff & Patient Directory Governance:** Manage doctor and patient accounts, toggle account suspension, or execute soft deletes with active booking validation.
- **Specialization Management:** Add, update, and safely delete unused clinic specializations.
- **Universal Appointment Oversight:** Confirm, reject, or cancel any appointment clinic-wide.
- **Exporting & Auditing:** Export operational reports to formatted **Excel (`.xlsx`)** via ClosedXML and **PDF** via QuestPDF; inspect email notification delivery logs.

---

## 🏗️ Technology Stack & Architecture

- **Framework:** .NET 8.0 LTS (C# 12)
- **Web Layer:** ASP.NET Core MVC, Razor Views, Bootstrap 5, Bootstrap Icons, Chart.js
- **Persistence:** Entity Framework Core 8, Microsoft SQL Server / Azure SQL Database
- **Authentication & Security:** ASP.NET Core Identity, PBKDF2 Password Hashing, HMAC-SHA256 OTP Hashes, Anti-Forgery Tokens, HSTS & HTTPS-Only enforcement
- **Email Delivery:** SendGrid API (`IEmailSender` abstraction) with fallback development console logging
- **Document Generation:** QuestPDF 2026 (Prescription & Report PDFs), ClosedXML (Excel exports)
- **Automated Testing:** xUnit, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`), FakeEmailSender
- **DevOps & Cloud:** Azure App Service (Linux, .NET 8 LTS), Azure SQL Database, Application Insights, GitHub Actions CI/CD

### Solution Structure (N-Tier Architecture)

```
MediCare.slnx
├── src/
│   ├── MediCare.Web/          # UI layer: Controllers, ViewModels, Views, custom CSS design system
│   ├── MediCare.Services/     # Business logic, DTOs, Service interfaces, Factory patterns, QuestPDF/Excel exports
│   └── MediCare.Data/         # Persistence layer: DbContext, Entities, Configurations, Repositories, Unit of Work, Migrations, Seeders
├── tests/
│   ├── MediCare.UnitTests/    # 56 unit tests: SlotCalculator, ScheduleService, OtpService, AppointmentService, MedicalRecordService
│   └── MediCare.Tests/        # 37 integration tests: 20-thread concurrency race, role access control, data ownership, anti-forgery
└── docs/                      # Architectural specifications, UI notes, test plans, bug logs, UAT scripts, Azure setup guide
```

Dependencies flow strictly in one direction: **`MediCare.Web` ➔ `MediCare.Services` ➔ `MediCare.Data`**. Controllers contain zero business logic.

---

## 💻 System Requirements

- **.NET 8.0 SDK** or later ([Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0))
- **SQL Server 2019+** or **LocalDB** (included with Visual Studio)
- **Git**

---

## 🚀 Quickstart: Running Locally

### 1. Clone the Repository
```bash
git clone https://github.com/your-org/medicare.git
cd medicare
```

### 2. Configure Development Secrets
The application reads sensitive configuration from `dotnet user-secrets` in development to ensure zero secrets are committed to source control (KPI-07):

```bash
cd src/MediCare.Web

# Initialize user secrets for MediCare.Web
dotnet user-secrets init

# Configure Administrator credentials (used to seed initial Admin account)
dotnet user-secrets set "Admin:Email" "admin@medicare.local"
dotnet user-secrets set "Admin:Password" "Admin#Password123"

# Configure SendGrid credentials (or leave dummy for local console email sender)
dotnet user-secrets set "SendGrid:ApiKey" "SG.your_actual_sendgrid_key_here"
dotnet user-secrets set "SendGrid:FromEmail" "noreply@yourdomain.com"
dotnet user-secrets set "SendGrid:FromName" "MediCare Clinic"

cd ../..
```

### 3. Build & Run the Application
```bash
dotnet run --project src/MediCare.Web
```

On first startup in the `Development` environment:
1. EF Core automatically applies any pending database migrations to `(localdb)\mssqllocaldb;Database=MediCare`.
2. The idempotent database seeder populates roles, the Administrator account, 10 clinical specializations, 6 sample doctors with schedules, and 10 sample patients.

Open your browser at `https://localhost:5001` or `http://localhost:5000`.

---

## 🔑 Pre-Seeded Sample Accounts

All seeded sample accounts use standard development passwords:

| Role | Email | Password | Description |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@medicare.local` | `Admin#Password123` *(from user-secrets)* | Full clinical & operational administrator |
| **Doctor** | `samira.zaki@medicare.local` | `Doctor#Password123` | Cardiologist (Main Clinic, Room 101) |
| **Doctor** | `hany.nabil@medicare.local` | `Doctor#Password123` | Orthopedic Surgeon (Branch Clinic, Room 202) |
| **Doctor** | `mona.fawzy@medicare.local` | `Doctor#Password123` | Pediatrician (Main Clinic, Room 103) |
| **Doctor** | `tarek.salem@medicare.local` | `Doctor#Password123` | Dermatologist (Branch Clinic, Room 204) |
| **Doctor** | `rania.lotfy@medicare.local` | `Doctor#Password123` | Neurologist (Main Clinic, Room 105) |
| **Doctor** | `youssef.ghali@medicare.local` | `Doctor#Password123` | Dentist (Branch Clinic, Room 206) |
| **Patient** | `ahmed.hassan@medicare.local` | `Patient#Password123` | Verified patient (Code: `P-1001`, Blood: `A+`) |
| **Patient** | `nour.ibrahim@medicare.local` | `Patient#Password123` | Verified patient (Code: `P-1002`, Blood: `B+`) |
| **Patient** | `omar.farouk@medicare.local` | `Patient#Password123` | Verified patient (Code: `P-1003`, Blood: `O+`) |

*(Note: In Production, sample doctors and patients are never seeded; only roles and the Admin account are created).*

---

## 🧪 Running Automated Tests

MediCare includes **93 automated tests** covering unit calculations, security policies, concurrency races, and database integrity:

```bash
# Run all tests (Unit + Integration)
dotnet test MediCare.slnx

# Run Unit tests only (56 tests)
dotnet test tests/MediCare.UnitTests

# Run SQL Server Integration tests only (37 tests against LocalDB)
dotnet test tests/MediCare.Tests

# Run Concurrency race test (20 simultaneous booking requests)
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"
```

For detailed test matrices and requirement traceability, see [TEST-PLAN.md](file:///d:/My%20partion/Full%20stack%20.Net%20Web%20Developer/Final%20Project/project%20implementation/MediCare/docs/TEST-PLAN.md).

---

## 📧 SendGrid Configuration & Sender Verification

To send live transaction emails (OTP verification, appointment confirmations, reschedule notices, cancellations):

1. **Sign Up:** Create a free account at [sendgrid.com](https://sendgrid.com).
2. **Create API Key:** Navigate to **Settings** > **API Keys** > **Create API Key** with **Full Access** or restricted **Mail Send** permissions.
3. **Sender Verification:**
   - Go to **Settings** > **Sender Authentication**.
   - Complete **Single Sender Verification** (for quick setup) or **Domain Authentication** (recommended for production).
   - Use the exact verified email address for `SendGrid:FromEmail`.
4. **Configure Secrets:**
   ```bash
   dotnet user-secrets set "SendGrid:ApiKey" "SG.your-actual-api-key" --project src/MediCare.Web
   dotnet user-secrets set "SendGrid:FromEmail" "your-verified-email@example.com" --project src/MediCare.Web
   ```

---

## ☁️ Azure Deployment (CI/CD)

The application is fully prepared for continuous deployment to **Azure App Service** and **Azure SQL**:

1. **Infrastructure Provisioning:** Follow the step-by-step Azure CLI instructions in [docs/AZURE-SETUP.md](file:///d:/My%20partion/Full%20stack%20.Net%20Web%20Developer/Final%20Project/project%20implementation/MediCare/docs/AZURE-SETUP.md) to create your Resource Group, App Service Plan, Web App, Azure SQL Database, and Application Insights.
2. **GitHub Secrets:** Add your publish credentials to GitHub:
   - `AZURE_WEBAPP_NAME`: Your Azure Web App name.
   - `AZURE_WEBAPP_PUBLISH_PROFILE`: XML publish profile downloaded from Azure.
3. **CI/CD Pipelines:**
   - `.github/workflows/ci.yml`: Triggers on every push and pull request to run builds and test suites.
   - `.github/workflows/deploy.yml`: Triggers on push to `main` to build, test, generate idempotent SQL migration scripts (`migrations.sql`), publish, and deploy directly to Azure App Service.

---

## 🌿 Branching Strategy & Contribution Guidelines

MediCare adheres to standard trunk-based feature branching:
- **`main`:** Protected production branch. Direct commits are restricted; changes must land via Pull Requests with passing CI status and at least one code review.
- **`feature/<short-name>`:** Topic branches for new capabilities (e.g., `feature/azure-cicd`, `feature/doctor-search`).
- **`bugfix/<short-name>`:** Dedicated branches for defect resolutions.
- **Pull Request Template:** Located at `.github/pull_request_template.md` with requirement traceability and security checklists.

---

## 📄 License & Compliance

Developed as part of the MediCare Clinic Management System Project. Sample patient data is purely synthetic. Complies with HIPPA/GDPR simulated healthcare privacy guidelines.
