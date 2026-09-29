import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Phone, Mail, MapPin, Droplets, User, Calendar, Shield, CalendarClock, Stethoscope, ClipboardList, Paperclip, FileText, Plus } from 'lucide-react'
import Button from '../../../components/Button'
import Badge, { BloodGroupBadge } from '../../../components/Badge'
import { patientApi } from '../services/patientApi'
import { appointmentApi } from '../../appointments/services/appointmentApi'
import { medicalRecordApi } from '../../medical-records/services/medicalRecordApi'
import { useAuth } from '../../auth/AuthContext'
import MedicalRecordDetailModal from '../../medical-records/components/MedicalRecordDetailModal'
import MedicalRecordFormModal from '../../medical-records/components/MedicalRecordFormModal'
import { calculateAge, formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import './PatientDetailPage.css'

export default function PatientDetailPage() {
  const { id } = useParams()
  const { user } = useAuth()
  const navigate = useNavigate()
  const [patient, setPatient] = useState(null)
  const [appointments, setAppointments] = useState([])
  const [medicalRecords, setMedicalRecords] = useState([])
  const [doctors, setDoctors] = useState([])
  const [selectedRecord, setSelectedRecord] = useState(null)
  const [isAddRecordOpen, setIsAddRecordOpen] = useState(false)
  const [savingRecord, setSavingRecord] = useState(false)
  const [appointmentError, setAppointmentError] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    let active = true

    const loadPatient = async () => {
      setLoading(true)
      setError(null)
      setAppointmentError(null)
      setAppointments([])
      setMedicalRecords([])
      try {
        const patientResult = await patientApi.getById(id)
        if (active) {
          setPatient(patientResult)
          try {
            const appointmentResult = await patientApi.getAppointmentHistory(id)
            if (active) setAppointments(appointmentResult)
          } catch {
            if (active) setAppointmentError('Appointment history is temporarily unavailable.')
          }

          try {
            const recordsResult = await medicalRecordApi.getByPatientId(id)
            if (active) setMedicalRecords(recordsResult || [])
          } catch (err) {
            console.error('Failed to load patient medical records:', err)
          }
        }
      } catch (err) {
        if (active) setError(err.response?.status === 404 ? 'Patient record not found.' : 'Unable to load this patient record.')
      } finally {
        if (active) setLoading(false)
      }
    }

    loadPatient()
    return () => { active = false }
  }, [id])

  const handleSaveRecord = async (payload) => {
    try {
      setSavingRecord(true)
      await medicalRecordApi.createMedicalRecord(payload)
      const updated = await medicalRecordApi.getPatientMedicalRecords(patient.patientId)
      setMedicalRecords(Array.isArray(updated) ? updated : (updated?.data || []))
      setIsAddRecordOpen(false)
    } catch (err) {
      console.error('Failed to create medical record:', err)
      alert(err.response?.data?.message || 'Failed to save medical record.')
    } finally {
      setSavingRecord(false)
    }
  }

  if (loading) {
    return (
      <div className="page-wrapper patient-detail__state">
        <div className="skeleton" style={{ height: '160px', borderRadius: '14px' }} />
        <div className="skeleton" style={{ height: '260px', borderRadius: '14px' }} />
      </div>
    )
  }

  if (error || !patient) {
    return (
      <div className="page-wrapper patient-detail__state">
        <p className="text-muted">{error || 'Patient record not found.'}</p>
        <Button variant="secondary" onClick={() => navigate('/patients')}>Back</Button>
      </div>
    )
  }

  const [c1, c2] = nameToGradient(patient.firstName)
  const initials = getInitials(patient.firstName, patient.lastName)

  return (
    <div className="page-wrapper">
      <Button
        variant="ghost"
        icon={ArrowLeft}
        onClick={() => navigate('/patients')}
        id="back-to-patients"
        style={{ marginBottom: '24px' }}
      >
        Back to Patients
      </Button>

      <div className="patient-detail">
        <div className="patient-detail__hero glass-card">
          <div
            className="patient-detail__avatar"
            style={{ background: `linear-gradient(135deg, ${c1}, ${c2})` }}
          >
            {initials}
          </div>
          <div className="patient-detail__hero-info">
            <span className="patient-detail__eyebrow">Patient record</span>
            <h1 className="patient-detail__name">{patient.firstName} {patient.lastName}</h1>
            <p className="patient-detail__sub">
              {patient.gender} · {calculateAge(patient.dateOfBirth)} years old · {patient.nic}
            </p>
          </div>
          <BloodGroupBadge group={patient.bloodGroup} />
        </div>

        <div className="patient-detail__sections">
          <InfoCard title="Contact Information" icon={Phone}>
            <InfoRow icon={Phone} label="Phone" value={patient.phoneNumber} />
            <InfoRow icon={Mail} label="Email" value={patient.email} />
            <InfoRow icon={MapPin} label="Address" value={patient.address} />
          </InfoCard>

          <InfoCard title="Medical Information" icon={Droplets}>
            <InfoRow icon={Droplets} label="Blood Group" value={<BloodGroupBadge group={patient.bloodGroup} />} />
            <InfoRow icon={Calendar} label="Date of Birth" value={formatDate(patient.dateOfBirth)} />
            <InfoRow icon={User} label="Gender" value={patient.gender} />
          </InfoCard>

          <InfoCard title="Emergency Contact" icon={Shield}>
            <InfoRow icon={User} label="Name" value={patient.emergencyContactName} />
            <InfoRow icon={Phone} label="Phone" value={patient.emergencyContactPhone} />
          </InfoCard>

          <InfoCard title="Record Info" icon={Calendar}>
            <InfoRow icon={Calendar} label="Registered" value={formatDate(patient.createdAt)} />
            <InfoRow icon={Calendar} label="Last Updated" value={formatDate(patient.updatedAt)} />
          </InfoCard>
        </div>

        {/* Medical Records & Clinical History Section */}
        <section className="patient-history glass-card" style={{ marginBottom: '24px' }}>
          <div className="patient-history__header">
            <div className="patient-history__heading">
              <div className="info-card__icon"><ClipboardList size={16} /></div>
              <div>
                <h3 className="info-card__title">Clinical Medical Records</h3>
                <p className="patient-history__sub">Diagnoses, prescriptions, and lab reports for this patient</p>
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span className="patient-history__count">{medicalRecords.length} {medicalRecords.length === 1 ? 'record' : 'records'}</span>
              <Button size="sm" variant="primary" icon={Plus} onClick={() => setIsAddRecordOpen(true)}>
                Add Record
              </Button>
              <Button size="sm" variant="secondary" onClick={() => navigate('/medical-records')}>
                All Records
              </Button>
            </div>
          </div>

          {medicalRecords.length === 0 ? (
            <p className="patient-history__empty">No medical records have been recorded for this patient.</p>
          ) : (
            <div className="patient-history__list">
              {medicalRecords.map((rec) => (
                <article
                  className="patient-history__item"
                  key={rec.medicalRecordId}
                  style={{ cursor: 'pointer' }}
                  onClick={() => setSelectedRecord(rec)}
                >
                  <div className="patient-history__date">
                    <strong>{formatDate(rec.recordDate)}</strong>
                    <Badge variant={rec.recordType === 'Consultation' ? 'primary' : 'info'}>{rec.recordType}</Badge>
                  </div>
                  <div className="patient-history__visit">
                    <div className="patient-history__doctor">
                      <Stethoscope size={15} />
                      <strong>{rec.doctorName || 'Attending Clinician'}</strong>
                    </div>
                    <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{rec.diagnosis}</span>
                    <small>{rec.treatmentPlan}</small>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    {rec.attachments?.length > 0 && (
                      <span className="mr-attachment-badge">
                        <Paperclip size={12} /> {rec.attachments.length}
                      </span>
                    )}
                    <span className={`patient-history__status patient-history__status--${rec.status.toLowerCase()}`}>
                      {rec.status}
                    </span>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        {/* Appointment History Section */}
        <section className="patient-history glass-card">
          <div className="patient-history__header">
            <div className="patient-history__heading">
              <div className="info-card__icon"><CalendarClock size={16} /></div>
              <div>
                <h3 className="info-card__title">Appointment History</h3>
                <p className="patient-history__sub">Recent scheduled visits for this patient</p>
              </div>
            </div>
            <span className="patient-history__count">{appointments.length} {appointments.length === 1 ? 'visit' : 'visits'}</span>
          </div>
          {appointments.length === 0 ? (
            <p className="patient-history__empty">{appointmentError || 'No appointments have been recorded for this patient.'}</p>
          ) : (
            <div className="patient-history__list">
              {appointments.map(appointment => (
                <article className="patient-history__item" key={appointment.appointmentId}>
                  <div className="patient-history__date">
                    <strong>{formatDate(appointment.scheduledAt)}</strong>
                    <span>{new Date(appointment.scheduledAt).toLocaleTimeString('en-LK', { hour: '2-digit', minute: '2-digit' })}</span>
                  </div>
                  <div className="patient-history__visit">
                    <div className="patient-history__doctor"><Stethoscope size={15} /><strong>{appointment.doctorName}</strong></div>
                    <span>{appointment.specialty} · {appointment.appointmentType}</span>
                    <small>{appointment.reason}</small>
                  </div>
                  <span className={`patient-history__status patient-history__status--${appointment.status.toLowerCase()}`}>
                    {appointment.status}
                  </span>
                </article>
              ))}
            </div>
          )}
        </section>
      </div>

      {selectedRecord && (
        <MedicalRecordDetailModal
          record={selectedRecord}
          onClose={() => setSelectedRecord(null)}
          canEdit={false}
        />
      )}

      {isAddRecordOpen && (
        <MedicalRecordFormModal
          open={isAddRecordOpen}
          isOpen={isAddRecordOpen}
          onClose={() => setIsAddRecordOpen(false)}
          onSubmit={handleSaveRecord}
          patients={[patient]}
          doctors={doctors}
          currentDoctorId={user?.role === 'Doctor' ? user.doctorId : null}
          loading={savingRecord}
        />
      )}
    </div>
  )
}

function InfoCard({ title, icon: Icon, children }) {
  return (
    <div className="info-card glass-card">
      <div className="info-card__header">
        <div className="info-card__icon"><Icon size={16} /></div>
        <h3 className="info-card__title">{title}</h3>
      </div>
      <div className="info-card__body">{children}</div>
    </div>
  )
}

function InfoRow({ icon: Icon, label, value }) {
  return (
    <div className="info-row">
      <span className="info-row__label">
        <Icon size={13} />
        {label}
      </span>
      <span className="info-row__value">{value || '—'}</span>
    </div>
  )
}

