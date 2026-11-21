## Description
<!-- Provide a brief description of the changes introduced by this pull request. -->

## Type of Change
- [ ] 🚀 New feature (non-breaking change adding functionality)
- [ ] 🐛 Bug fix (non-breaking change fixing an issue)
- [ ] 📝 Documentation update
- [ ] 🎨 UI/UX styling enhancement (matches `docs/UI-NOTES.md`)
- [ ] ⚡ Performance improvement
- [ ] 🧪 Tests (unit, integration, or concurrency test additions)
- [ ] 🔧 Build / CI/CD configuration

## Traceability & Requirements
<!-- Reference related Functional Requirement (FR), Non-Functional Requirement (NFR), or Business Rule (BR) IDs. -->
- **Requirements Covered:** (e.g. FR-12, BR-01, NFR-06)
- **Related Issue:** Closes #

## Architecture & Code Quality Checklist
- [ ] **Layering:** Dependencies flow strictly one way: `MediCare.Web` -> `MediCare.Services` -> `MediCare.Data`.
- [ ] **No Business Logic in Controllers:** Controllers remain thin; business rules reside in Services.
- [ ] **Async / Await:** All database and external I/O operations are strictly asynchronous and awaited.
- [ ] **Security & Authorization:** Server-side `[Authorize(Roles = "...")]` and ownership validation are enforced.
- [ ] **Anti-Forgery:** State-altering POST endpoints include anti-forgery protection.
- [ ] **Zero Secrets Committed:** No API keys, passwords, or live connection strings in code or git history (KPI-07).
- [ ] **Tests Pass:** `dotnet test MediCare.slnx` runs and passes with 0 failures.
- [ ] **UI Fidelity:** Screen layouts adhere to tokens and components documented in `docs/UI-NOTES.md`.

## Testing Verification
<!-- Describe the tests executed to verify these changes. -->
- [ ] Automated Unit Tests: `dotnet test tests/MediCare.UnitTests`
- [ ] Automated Integration Tests: `dotnet test tests/MediCare.Tests`
- [ ] Manual / Browser Verification
