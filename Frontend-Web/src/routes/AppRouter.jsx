import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import DashboardLayout from '../layouts/DashboardLayout'
import DashboardPage from '../features/dashboard/pages/DashboardPage'
import PatientListPage from '../features/patients/pages/PatientListPage'
import PatientDetailPage from '../features/patients/pages/PatientDetailPage'
import DoctorsPage from '../features/doctors/pages/DoctorsPage'
import AppointmentsPage from '../features/appointments/pages/AppointmentsPage'
import LoginPage from '../features/auth/pages/LoginPage'
import RegisterPage from '../features/auth/pages/RegisterPage'
import ProtectedRoute from '../features/auth/ProtectedRoute'
import DoctorDashboardPage from '../features/doctors/pages/DoctorDashboardPage'
import DoctorProfilePage from '../features/doctors/pages/DoctorProfilePage'
import TriageReviewPage from '../features/triage/pages/TriageReviewPage'
import RoomsPage from '../features/rooms/pages/RoomsPage'
import DoctorSchedulesPage from '../features/appointments/pages/DoctorSchedulesPage'
import MedicalRecordsPage from '../features/medical-records/pages/MedicalRecordsPage'

export default function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/doctor/register" element={<RegisterPage />} />
        <Route path="/register" element={<Navigate to="/doctor/register" replace />} />

        <Route element={<ProtectedRoute roles={['Admin']} />}>
          <Route element={<DashboardLayout />}>
            <Route index element={<Navigate to="/dashboard" replace />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route path="/patients" element={<PatientListPage />} />
            <Route path="/patients/:id" element={<PatientDetailPage />} />
            <Route path="/doctors" element={<DoctorsPage />} />
            <Route path="/appointments" element={<AppointmentsPage />} />
            <Route path="/medical-records" element={<MedicalRecordsPage />} />
            <Route path="/triage/review" element={<TriageReviewPage />} />
            <Route path="/admin/rooms" element={<RoomsPage />} />
          </Route>
        </Route>

        <Route element={<ProtectedRoute roles={['Doctor']} />}>
          <Route element={<DashboardLayout />}>
            <Route path="/doctor/dashboard" element={<DoctorDashboardPage />} />
            <Route path="/doctor/medical-records" element={<MedicalRecordsPage />} />
            <Route path="/doctor/profile" element={<DoctorProfilePage />} />
            <Route path="/doctor/triage-review" element={<TriageReviewPage />} />
            <Route path="/doctor/schedules" element={<DoctorSchedulesPage />} />
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
