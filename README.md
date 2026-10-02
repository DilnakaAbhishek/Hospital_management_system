# Hospital Management System (MediCore)

> **Course**: BSc (Hons) in Information Technology – SE / AI  
> **Module**: SE3090 – Software Engineering Frameworks (Year 3, Semester 1, 2026)  
> **Assignment**: Assignment 1 – Integrated Full-Stack and Agentic AI Application Development

---

## 👥 Group Component Breakdown

1. **Member 1**: Patient Management with SafeTriage Agent
2. **Member 2**: Doctor Management with Planning Agent
3. **Member 3**: Appointment Management with Appointment Agent
4. **Member 4**: Medical Report Management with Medical Report Agent

---

## 📂 Repository Structure & Documentation

| Component | Directory | Description | Documentation |
|---|---|---|---|
| **Backend API** | `Backend/` | ASP.NET Core 8 Web API, PostgreSQL (Neon), Agentic AI | [Backend README](Backend/README.md) |
| **Web Portal** | `Frontend-Web/` | React 18 + Vite (Admin, Doctor, SafeTriage Review Queue) | [Web README](Frontend-Web/README.md) |
| **Mobile App** | `Frontend-Mobile/` | Flutter (Patient App, AI Health Assistant, Vitals Logger) | [Mobile README](Frontend-Mobile/README.md) |

---

## 🚀 Quick Start

### 1. Backend API (.NET 8)
```bash
dotnet run --project Backend/HospitalManagementSystem.Api --launch-profile http
```
- Swagger UI: `http://localhost:5000/swagger`

### 2. Web Portal (React 18)
```bash
cd Frontend-Web
npm install
npm run dev
```
- Web Portal: `http://localhost:5173`

### 3. Mobile App (Flutter)
```bash
cd Frontend-Mobile
flutter pub get
flutter run
```

---

## 🧪 Automated Testing

| Component | Framework | Command | Status |
|---|---|---|---|
| **Backend API** | xUnit (.NET 8) | `dotnet test Backend/HospitalManagementSystem.Api.Tests` | **274 / 274 Passed** |
| **Web Portal** | Vitest (React 18) | `npx vitest run src/features/triage/pages/TriageReviewPage.test.jsx` | **4 / 4 Passed** |
| **Mobile App (AI)** | Flutter Test | `flutter test test/hospital_assistant_test.dart` | **15 / 15 Passed** |
