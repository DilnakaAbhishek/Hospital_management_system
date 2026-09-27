# MediCore Mobile App (Flutter)

> **Platform**: Flutter 3 (Dart 3.0+), Material 3  
> **Target Audience**: Hospital Patients  
> **Course**: SE3090 — Software Engineering Frameworks  
> **Focus Subsystem**: Patient Management with SafeTriage Agent (Member 1)

---

## 1. Overview

The **MediCore Mobile Application** is a patient-facing cross-platform mobile app built with Flutter. It connects patients with hospital services, conversational AI triage, appointment scheduling, and electronic health records (EHR).

---

## 2. Key Modules & Features

- **Conversational AI Health Assistant (`/assistant`)**:
  - Multi-turn safe symptom triage powered by Google Gemini and local deterministic safety rules.
  - Interactive clarification questions (up to 3 follow-ups) with quick single-select chips or text input.
  - SafeTriage doctor review banner showing pending approval status and delivery of clinician notes.
  - Non-emergency slot proposal cards with explicit patient confirmation before booking.
  - Emergency escalation banner with direct hospital contact action.
- **Vitals Logger (`/vitals`)**: Record and track blood pressure, pulse, SpO2, temperature, and blood glucose with normal range validation.
- **Patient Profile (`/profile`)**: Manage demographics, emergency contacts, medical history, and allergies.
- **Doctor Directory & Booking (`/doctors`)**: Search doctors by department and specialty; view consultation hours and fees.
- **Appointments Dashboard (`/appointments`)**: View upcoming and historical consultations; cancel with reason submission.
- **Medical Records (`/medical-records`)**: Access consultation summaries, prescriptions, and lab attachments.
- **Emergency Clinic Finder (`/clinic-finder`)**: Locate nearby 24/7 hospital clinics via device GPS.

---

## 3. Getting Started

### Prerequisites
- [Flutter SDK](https://docs.flutter.dev/get-started/install) (version 3.19+)
- Android Studio / Xcode

### Setup & Run
```bash
cd Frontend-Mobile
flutter pub get
flutter run
```

### API Endpoint Configuration
Defined in `lib/core/services/api_service.dart`:
- **Android Emulator**: `http://10.0.2.2:5000/api`
- **iOS Simulator / Desktop / Web**: `http://localhost:5000/api`

---

## 4. Running Tests

To run the Student 1 Agentic AI Assistant test suite:

```bash
flutter test test/hospital_assistant_test.dart
```

**Status: 15 / 15 Passed (100% Pass Rate).**
