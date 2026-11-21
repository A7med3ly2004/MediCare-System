# MediCare — User Acceptance Testing (UAT) Plan & Execution Script

**Application:** MediCare Clinic Management & Appointment Platform  
**Target Roles:** Anonymous, Patient, Doctor, Admin  
**Prerequisites:** Solution running locally (`dotnet run --project src/MediCare.Web`), database seeded with initial roles and specializations.

---

## Instructions for Testers
1. Follow each scenario sequentially from Step 1 to the end.
2. Verify that the **Expected Result** is met.
3. Mark the **Result** column as **PASS**, **FAIL**, or **BLOCKED**.
4. If a step fails, record the step number, screenshot, and console/network logs in the test notes.

---

## 1. Scenario 1: Patient Registration & Email Verification (UC-01, FR-01 to FR-04)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **1.1** | Anonymous | Navigate to `http://localhost:5000/Account/RegisterPatient`. | Patient registration form is displayed with Full Name, Email, Password, Confirm Password, Date of Birth, Gender, Blood Group, Phone, and Address. | [ ] | |
| **1.2** | Anonymous | Submit the form with missing required fields (e.g. empty Email). | Client-side and server-side validation error displays next to the failing field. Form is not submitted. | [ ] | |
| **1.3** | Anonymous | Fill in valid patient details and click **Register**. | Account is created, user is redirected to `/Account/VerifyOtp?email=...`, and a 6-digit OTP email is dispatched. | [ ] | |
| **1.4** | Patient | Enter an incorrect 6-digit code 5 times. | After the 5th failed attempt, error indicates the code is locked due to security limits. | [ ] | |
| **1.5** | Patient | Click **Resend Code** before cooldown expires. | Toast/alert indicates cooldown is active and shows remaining seconds. | [ ] | |
| **1.6** | Patient | After cooldown, click **Resend Code**, then enter the newly received 6-digit code and submit. | Code verifies successfully. Account is activated (`IsEmailVerified = true`), user is signed in, and redirected to `/Patient`. | [ ] | |

---

## 2. Scenario 2: Doctor Directory Search & Filtering (UC-03, FR-07 to FR-10)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **2.1** | Patient | Navigate to `/Doctors` from the main navigation bar. | Directory page loads showing search filters (Specialization dropdown, Location text input, Availability Date picker) and paginated doctor cards. | [ ] | |
| **2.2** | Patient | Select a specialization (e.g., "Cardiology") and click **Search**. | List filters to show only verified, active doctors in Cardiology with photo, fee, location, and room number. | [ ] | |
| **2.3** | Patient | Select a future date when a doctor is on vacation. | Doctor does not appear in the available results for that date. | [ ] | |
| **2.4** | Patient | Click **Clear filters**. | All active and verified doctors are displayed again. | [ ] | |
| **2.5** | Patient | Click **View Profile** on a doctor card. | Full profile page (`/Doctors/Details/{id}`) loads showing doctor information, room, fee, bio, and time slot booking calendar/slots. | [ ] | |

---

## 3. Scenario 3: Appointment Booking & Lead-Time Rules (UC-04, UC-05, FR-12 to FR-17, BR-01 to BR-04)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **3.1** | Patient | On the doctor profile, pick a date and select a free time slot (e.g., 10:00 AM - 10:30 AM). Fill in optional notes and click **Book Appointment**. | Booking is completed in < 2 minutes (KPI-02). Status is **Pending**. Notification emails are sent to both patient and doctor. | [ ] | |
| **3.2** | Patient | Navigate to `/Patient/Appointments`. | The newly booked appointment appears in the **Upcoming** list with a yellow **Pending** badge. | [ ] | |
| **3.3** | Patient | Try to book the exact same slot again in another browser tab. | System rejects the booking with message "This time slot is no longer available" (Zero double booking). | [ ] | |
| **3.4** | Patient | Click **Reschedule** on the pending appointment (date > 2 hours in the future). Select a new free slot. | Appointment moves to the new slot and remains/returns to **Pending**. Confirmation email sent. | [ ] | |
| **3.5** | Patient | Click **Cancel** on an appointment scheduled less than 2 hours from now (or simulate lead time limit). | System refuses cancellation with message "Appointments cannot be cancelled within 2 hours of start time." | [ ] | |
| **3.6** | Patient | Click **Cancel** on an appointment scheduled >= 2 hours from now, provide a reason, and confirm. | Status transitions to **Cancelled**. The slot is freed up and immediately reappears as bookable on the doctor's calendar. | [ ] | |

---

## 4. Scenario 4: Doctor Schedule, Vacations & Conflict Prevention (UC-06, FR-11, BR-06)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **4.1** | Doctor | Log in as Doctor and navigate to `/Doctor/Schedule`. | Weekly schedule page loads showing working windows per weekday (Monday–Sunday) and vacation days list. | [ ] | |
| **4.2** | Doctor | Add a second non-overlapping working window on Tuesday (e.g. 14:00 to 18:00) and save. | Schedule saves successfully. Slots are generated for both morning and afternoon windows. | [ ] | |
| **4.3** | Doctor | Attempt to save an overlapping window (e.g., 09:00–12:00 and 11:00–14:00 on the same day). | Validation error prevents saving: overlapping windows on the same day are rejected. | [ ] | |
| **4.4** | Doctor | Attempt to add a vacation day on a date that already has a **Pending** or **Confirmed** appointment. | System rejects the vacation request (BR-06) and displays the table of conflicting appointments with patient name, date, and time. | [ ] | |

---

## 5. Scenario 5: Doctor Dashboard, Confirmation & Medical Records (UC-07, UC-08, FR-14, FR-20 to FR-24, BR-05, BR-08)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **5.1** | Doctor | Navigate to `/Doctor` (Doctor Dashboard). | Dashboard displays today's appointments, pending appointment requests counter, and quick action buttons. | [ ] | |
| **5.2** | Doctor | Click **Confirm** on a pending appointment request. | Status transitions to **Confirmed**. RowVersion concurrency token ensures decision wins. Email sent to patient with doctor's room number. AuditLog records the Confirm action. | [ ] | |
| **5.3** | Doctor | Click **Record Visit** on a Confirmed appointment. | Medical record form loads with patient's medical history, allergies, vitals fields (Blood Pressure, Heart Rate), Symptoms, Diagnosis, Treatment Notes, and dynamic Prescription lines. | [ ] | |
| **5.4** | Doctor | Enter Blood Pressure ("120/80"), Heart Rate ("72"), Symptoms, Diagnosis ("Acute bronchitis"), and add 2 medication lines (Medication, Dosage, Frequency). Click **Save Record**. | In ONE database transaction: Medical record and prescription items are saved, and the appointment status is updated to **Completed**. | [ ] | |
| **5.5** | Doctor | Attempt to save a record for an appointment that is still **Pending** or belongs to another doctor. | Refused on server side with `UnauthorizedAccessException` or invalid status error. | [ ] | |

---

## 6. Scenario 6: Patient Medical History & Prescription Download (UC-09, FR-23, FR-24, NFR-06)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **6.1** | Patient | Log in as Patient and navigate to `/Patient/MedicalHistory`. | List of past completed visits displays doctor name, date, diagnosis, and blood pressure/heart rate. | [ ] | |
| **6.2** | Patient | Click **View Details** on a medical record. | Detailed record view opens showing full diagnosis, clinical notes, and prescription table. | [ ] | |
| **6.3** | Patient | Click **Download Prescription (PDF)**. | Clean, branded PDF generated via QuestPDF downloads successfully. Document contains clinic logo, patient info, doctor name, and formatted Rx medication table. | [ ] | |
| **6.4** | Patient | Attempt to access another patient's medical record URL (`/Patient/MedicalRecord/999`). | Server refuses access with 403 Forbidden or Redirect to Access Denied (Data ownership enforced). | [ ] | |

---

## 7. Scenario 7: Admin Area, Daily Report, Management & Export (UC-10, UC-11, FR-29 to FR-35, BR-07)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **7.1** | Admin | Log in as Admin and navigate to `/Admin`. | Admin dashboard loads with Chart.js charts (Monthly Appointments, Popular Doctors, Popular Specializations) and Daily Report statistics widget. | [ ] | |
| **7.2** | Admin | Select a date on the Daily Report widget and click **Filter**. | Displays new patient registrations, new doctor registrations, appointments booked, confirmed, and cancelled for that day. Date with no data shows zeros. | [ ] | |
| **7.3** | Admin | Navigate to `/Admin/Doctors`. Search for a doctor by name or specialization. | Paginated doctor list displays. Admin can edit doctor details or toggle account suspension. | [ ] | |
| **7.4** | Admin | Attempt to soft-delete a doctor who has future Pending or Confirmed appointments. | System refuses deletion and recommends **Suspend** instead, explaining that active appointments exist. | [ ] | |
| **7.5** | Admin | Suspend a doctor. Then attempt to log in with that doctor's credentials in an incognito window. | Login is refused with explicit message indicating account has been suspended. | [ ] | |
| **7.6** | Admin | Navigate to `/Admin/Reports` and click **Export Appointments (Excel)**. | Clean `.xlsx` spreadsheet downloads with full appointment history, patient codes, doctor names, dates, and statuses. | [ ] | |
| **7.7** | Admin | Click **Export Daily Report (PDF)**. | Branded PDF report downloads containing summary KPI metrics and appointment breakdown for the selected period. | [ ] | |
| **7.8** | Admin | Navigate to `/Admin/Notifications`. | Audit log of all dispatched system emails (OTP, Booking Received, Confirmed, Rescheduled, Cancelled) displays with timestamps, recipient, subject, and Sent/Failed status. | [ ] | |

---

## 8. Scenario 8: Security & Cross-Role Isolation (NFR-02, NFR-06)

| Step # | Role | Action / Steps | Expected Result | Pass / Fail | Notes |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **8.1** | Anonymous | Attempt to access `/Admin`, `/Doctor`, or `/Patient/Appointments` without logging in. | Request is challenged and rejected: redirected to `/Account/Login` or returns HTTP 401. | [ ] | |
| **8.2** | Patient | Attempt to access `/Admin` or `/Doctor/Schedule`. | Request is refused with HTTP 403 Forbidden or redirected to Access Denied. | [ ] | |
| **8.3** | Doctor | Attempt to access `/Admin/Reports` or `/Patient`. | Request is refused with HTTP 403 Forbidden or redirected to Access Denied. | [ ] | |
| **8.4** | Malicious | Send a POST request to any mutation action (e.g., `/Admin/ToggleDoctorSuspend`) without an Anti-Forgery token. | Request is rejected with HTTP 400 Bad Request. | [ ] | |
