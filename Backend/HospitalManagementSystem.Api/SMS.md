# Appointment SMS

The existing controller/service/repository architecture is retained. `AppointmentService` and
`DoctorScheduleService` call a shared `AppointmentSmsNotifier`, which depends on `ISmsService`.
No database migration or frontend change is required.

## Configuration

The default is Mock, even if `Sms:Provider` is absent. Local `appsettings.json` and the committed
`appsettings.example.json` contain:

```json
"Sms": { "Provider": "Mock" },
"Infobip": { "BaseUrl": "", "Sender": "ServiceSMS" }
```

Mock logs the normalized recipient and message through ILogger and never creates an HTTP client.
Use test patient profiles while developing because Mock logs contain phone numbers and appointment details.

For a real demo, set `Sms:Provider` to `Infobip`, set `Infobip:BaseUrl` to your account's HTTPS API
origin (for example `https://YOUR_ACCOUNT.api.infobip.com`), and configure `Infobip:Sender`.
Restart the API after changing providers. Unknown provider names are rejected at startup.

The project already has a UserSecretsId. From the repository root, set the key locally:

```powershell
dotnet user-secrets set "Infobip:ApiKey" "YOUR_REAL_API_KEY" --project Backend/HospitalManagementSystem.Api
```

Do not paste the real key into source files or shared terminals/transcripts. To avoid putting it in
PowerShell command history, use a masked prompt and the user-secrets JSON stdin interface instead:

```powershell
$smsSecureKey = Read-Host "Infobip API key" -AsSecureString
$smsPlainKey = [System.Net.NetworkCredential]::new('', $smsSecureKey).Password
@{ 'Infobip:ApiKey' = $smsPlainKey } | ConvertTo-Json -Compress | dotnet user-secrets set --project Backend/HospitalManagementSystem.Api
Remove-Variable smsSecureKey, smsPlainKey
```

ASP.NET loads User Secrets in Development. In deployed environments inject `Infobip__ApiKey`
through your deployment secret store; `Sms__Provider`, `Infobip__BaseUrl`, and `Infobip__Sender`
are the corresponding environment-variable overrides. User Secrets are local development storage,
not an encrypted production secret vault.

The implementation uses `POST /sms/3/messages`, `Authorization: App ...`, `sender`, `destinations`,
and `content.text`, following [Infobip's current SMS tutorial](https://www.infobip.com/docs/tutorials/send-your-first-sms-message-using-infobip-api).
It validates the single message response and accepts pending/delivered status groups (1/3).
Submission acceptance is not proof of handset delivery. Trial accounts require a verified recipient;
`ServiceSMS` is the documented trial sender.

## Flow

1. Existing validation and booking capacity checks run.
2. Save the appointment and commit the repository-owned transaction.
3. Load the saved appointment with its patient and doctor time slot.
4. Use current `Patient.PhoneNumber`; legacy appointments without PatientId can resolve the profile
   by their existing PatientEmail, matching the application's email-based patient lookup.
   The caller-supplied `Appointment.PatientPhone` snapshot is never used for sending.
5. Normalize Sri Lankan numbers such as `0771234567`, `+94 77 123 4567`, and `0094771234567`
   to `94771234567`. Missing profiles and invalid/non-Sri-Lankan numbers are skipped and logged.
6. Build the message from the saved doctor name, session start converted to Asia/Colombo, and queue number.
7. Await the selected provider. Infobip requests have a 10-second timeout and no automatic retries.
   Errors, invalid responses, rejected statuses, or missing Infobip configuration are logged;
   the successful appointment response and saved record remain intact.

Creation, cancellation, rescheduling, relevant admin appointment updates, admin slot changes/cancellation,
and doctor-owned schedule updates are connected. Repeated cancellation and same-slot rescheduling do not
send another SMS. Schedule updates notify confirmed bookings only. Existing rules preventing doctors
from cancelling/deleting booked schedules remain in place. Deactivating a slot through the existing
update route sends “session unavailable” without changing existing appointment-status semantics.

The assistant and proposal-confirmation flows own outer transactions. Notifications there are held
in the scoped notifier and flushed by transaction ID only after successful commit. Rolled-back
operations never flush. New transaction-owning callers must follow the same pattern and share the scoped notifier.

## Swagger / Postman test

1. Start the existing backend in Development with its database/JWT configuration, leaving Mock enabled:
   `dotnet run --project Backend/HospitalManagementSystem.Api`.
2. Open `/swagger`, log in using `/api/auth/login`, and enter the returned JWT in Authorize.
   In Postman use `Authorization: Bearer <token>`.
3. Ensure the authenticated patient's existing profile contains a valid phone number. Get a future
   available slot from `GET /api/appointment/slots?onlyAvailable=true`.
4. Send `POST /api/appointment` with the real slot ID:

   ```json
   {
     "doctorTimeSlotId": 123,
     "patientName": "Your test patient",
     "patientPhone": "0771234567",
     "appointmentType": "Consultation"
   }
   ```

   The patient token supplies the linked patient ID/email. Admin requests should also supply the real
   `patientId`. The phone above is a DTO example; the recipient comes from the saved patient profile.
5. Expect HTTP 201 and a `Mock SMS to ...` log with the real doctor and Sri Lanka appointment time.
   `GET /api/appointment/{id}` must return the saved appointment.
6. Reschedule using `POST /api/appointment/{id}/reschedule` with `{"doctorTimeSlotId":456}`;
   cancel using `POST /api/appointment/{id}/cancel` with `{"reason":"Test cancellation"}`.
   Verify the logs and updated appointment. For schedule updates, use the existing slot/schedule
   endpoints in Swagger and confirm only affected confirmed patients are notified.
7. For a real demo, configure Infobip as above and use your verified trial phone in the patient profile.
   Create a fresh appointment. Check the device/provider delivery report separately from acceptance logs.
8. To verify failure isolation, remove the Infobip key temporarily in a test environment and book again:
   expect the normal HTTP 201 and a configuration warning, with the appointment retained.

## Verification and limits

Run `dotnet build Backend/HospitalManagementSystem.Api/HospitalManagementSystem.Api.csproj` and
`dotnet test Backend/HospitalManagementSystem.Api.Tests/HospitalManagementSystem.Api.Tests.csproj`.
Tests use EF InMemory, both provider DI registrations, and stubbed HTTP responses. They do not contact Infobip.
Live PostgreSQL transaction behavior and real handset delivery need environment-specific testing.

There is no durable SMS outbox or retry queue: a process crash after commit can lose a notification.
Delivery receipts, retries, and scheduled reminders are left for later. The existing background completion
worker is unchanged; it does not send reminders.

## Files

Created in `Services`: `ISmsService.cs`, `MockSmsService.cs`, `InfobipSmsService.cs`, `SmsPhoneNumber.cs`,
`SmsServiceRegistration.cs`, `AppointmentSmsNotifier.cs`. Also created this guide and `../HospitalManagementSystem.Api.Tests/SmsTests.cs`.

Modified: `Services/AppointmentService.cs`, `Services/DoctorScheduleService.cs`, `Program.cs`,
`Controllers/AppointmentProposalConfirmationController.cs`, `AgenticAI/HospitalAssistant/HospitalAssistantService.cs`,
`appsettings.example.json`, and local ignored `appsettings.json`.
Existing constructor setup was updated in `AppointmentServiceTests.cs`, `RoomSchedulingServiceTests.cs`,
`AgenticAI/AppointmentSchedulingAgentTests.cs`, and `AgenticAI/HospitalAssistantTests.cs`.
Existing repository queries already support the required relationships, so no repository change was necessary.
