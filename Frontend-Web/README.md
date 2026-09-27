# MediCore Web Portal (React 18 + Vite)

> **Platform**: React 18, Vite, React Router 7, Vanilla CSS  
> **Target Audience**: Hospital Administrators, Clinicians, and Medical Staff  
> **Course**: SE3090 — Software Engineering Frameworks

---

## 1. Overview

The MediCore Web Portal is a modern, responsive web application for hospital management and clinical oversight. It features role-based access for Administrators and Doctors, real-time triage notifications, and a dedicated **SafeTriage Clinical Review Queue**.

---

## 2. Key Modules & Features

- **Admin & Doctor Dashboards**: Real-time hospital metrics, patient volume statistics, room occupancy, and upcoming consultation schedules.
- **SafeTriage Clinical Review Queue (`/doctor/triage-review`, `/triage/review`)**:
  - Live review queue for patient assessments flagged by the AI triage system (red flags, urgent symptoms, or patient review requests).
  - Inspect accumulated patient symptoms, extracted clinical facts, and SafeTriage recommendation.
  - One-click **"Approve SafeTriage suggestion"** to validate AI guidance.
  - **"Provide my own suggestion"** modal to override patient guidance with custom clinician advice.
  - Expandable technical details showing audit event traces, requirement states, and triage levels.
- **Patient Management (`/patients`)**: Comprehensive patient EHR directory, search, filter, patient profile, and vital signs monitoring.
- **Doctor Directory & Room Schedules (`/doctors`, `/doctor/schedules`)**: Specialist directory, room allocations, and weekly schedule management.
- **Appointment Center (`/appointments`)**: Schedule overview, session bookings, status updates, and cancellations.
- **Medical Records (`/medical-records`, `/doctor/medical-records`)**: Digital EHR consultations, diagnoses, prescriptions, and Cloudflare R2 lab attachment viewer.
- **Real-Time Notification Bell**: Polling and visual notification badge for incoming clinical reviews and appointment alerts.
- **Dark/Light Theme**: Seamless theme switcher persisted in local storage.

---

## 3. Getting Started

### Prerequisites
- [Node.js](https://nodejs.org/) (v18.0 or later)
- npm (v9.0 or later)

### Installation
From the `Frontend-Web/` directory:

```bash
cd Frontend-Web
npm install
```

### Running the Development Server
```bash
npm run dev
```

The portal runs locally at [http://localhost:5173](http://localhost:5173).

---

## 4. Running Tests

Unit and integration tests are powered by **Vitest** and **React Testing Library**:

```bash
npm test
```

To run specifically the SafeTriage Review Queue test suite:

```bash
npx vitest run src/features/triage/pages/TriageReviewPage.test.jsx
```

**Status: 4 / 4 Tests Passed.**
