# MediCore Backend API (.NET 8)

> **Platform**: ASP.NET Core 8 Web API, Entity Framework Core 8, PostgreSQL (Neon Serverless)  
> **Course**: SE3090 — Software Engineering Frameworks  
> **Architecture**: Multi-Agent Agentic AI Orchestration, Clean Architecture & Layered Services

---

## 1. Overview

The MediCore Backend is an enterprise-grade hospital management system API supporting four integrated modules:
1. **Patient Management with SafeTriage Agent** (Member 1)
2. **Doctor Management with Planning Agent** (Member 2)
3. **Appointment Management with Appointment Agent** (Member 3)
4. **Medical Report Management with Medical Report Agent** (Member 4)

---

## 2. Directory & Project Structure

```
Backend/
├── HospitalManagementSystem.Api/
│   ├── AgenticAI/                         # Autonomous multi-agent coordination layer
│   │   ├── PlanningCoordinator/           # Intent classification, planning, Gemini client, replanning
│   │   ├── SafeTriage/                    # Multi-agent triage pipeline (Extraction, Planning, Response)
│   │   ├── HospitalAssistant/             # Conversational state machine & patient preferences
│   │   ├── PatientCare/                   # Appointment proposal & safety validation agents
│   │   └── MedicalReports/                # EHR intelligence, lab summarization agent
│   ├── Controllers/                       # REST API endpoints (Auth, Patients, Doctors, Appointments, etc.)
│   ├── Data/                              # ApplicationDbContext, Neon PostgreSQL connection
│   ├── DTOs/                              # Strongly typed request/response transfer objects
│   ├── Models/                            # Database entity models
│   ├── Repositories/                      # Data access repositories
│   ├── Services/                          # Core business logic services
│   ├── appsettings.json                   # System configuration (JWT, Gemini, Database, Cloudflare R2)
│   └── Program.cs                         # Application entrypoint & DI container registration
│
└── HospitalManagementSystem.Api.Tests/    # Comprehensive xUnit test suite (274 tests)
    ├── AgenticAI/                         # Planning Coordinator & SafeTriage unit/integration tests
    ├── AppointmentServiceTests.cs         # Booking & scheduling logic tests
    ├── DoctorSearchServiceTests.cs        # Doctor filtering & search tests
    ├── PatientServiceTests.cs             # Patient management & verification tests
    └── TriageWorkflowServiceTests.cs      # SafeTriage clinical rules & review queue tests
```

---

## 3. Running Locally

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Neon PostgreSQL database instance (or local PostgreSQL 15+)

### Start the API Server
From the repository root:

```bash
dotnet run --project Backend/HospitalManagementSystem.Api --launch-profile http
```

Once running / deployed:
- **Deployment Status Page**: [http://localhost:5000/](http://localhost:5000/) (Displays deployment status badge, active environment, and quick links)
- **Swagger UI**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **Health Check**: [http://localhost:5000/health](http://localhost:5000/health)

---

## 4. Configuration

Key configuration sections in `Backend/HospitalManagementSystem.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-neon-host.aws.neon.tech;Port=5432;Database=neondb;Username=your_user;Password=your_password;SSL Mode=Require"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY",
    "Model": "gemini-3.5-flash-lite",
    "TimeoutSeconds": 75
  },
  "Jwt": {
    "Secret": "development-only-jwt-secret-change-before-production-2026",
    "Issuer": "MediCore.Api",
    "Audience": "MediCore.Client"
  },
  "CloudflareR2": {
    "BucketName": "hospital-medical-records"
  }
}
```

*Note: You can also pass `GEMINI_API_KEY` as an environment variable or via `dotnet user-secrets`.*

---

## 5. Agentic AI Architecture Highlights

- **Google Gemini Structured Output**: Strict JSON schema enforcement for planning, requirement extraction, and safe guidance.
- **Defense-in-Depth**: Immediate, zero-latency **Deterministic Rule-Based Fallback** if Gemini is unreachable, slow, or rate-limited.
- **Human-in-the-Loop (HITL) Boundary**: Red-flag symptoms or vital anomalies route into `PendingClinicalReview`, requiring doctor approval via the Web Review Queue before advice is delivered.
- **Zero-Assumption Booking**: Symptoms never trigger auto-booking; booking requires explicit patient slot confirmation.
- **Adversarial Defenses**: Pre-screen heuristics catch and neutralize prompt injection attempts.

---

## 6. Running Tests

To run the complete test suite:

```bash
dotnet test Backend/HospitalManagementSystem.Api.Tests/HospitalManagementSystem.Api.Tests.csproj
```

**Status: 274 Passed, 0 Failed, 0 Skipped (100% Pass Rate).**
