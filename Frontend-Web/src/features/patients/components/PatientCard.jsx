import { Phone, Mail, MapPin, Edit2, Trash2, Eye, CreditCard, CalendarDays } from 'lucide-react'
import { BloodGroupBadge } from '../../../components/Badge'
import Button from '../../../components/Button'
import { calculateAge, formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import './PatientCard.css'

export default function PatientCard({ patient, onEdit, onDelete, onView }) {
  const gradient = nameToGradient(patient.firstName)
  const fullName = `${patient.firstName} ${patient.lastName}`
  const age = calculateAge(patient.dateOfBirth)
  const initials = getInitials(patient.firstName, patient.lastName)

  return (
    <article className="patient-card">
      <div className="patient-card__accent" style={{ background: `linear-gradient(180deg, ${gradient[0]}, ${gradient[1]})` }} />

      <header className="patient-card__header">
        <div className="patient-card__avatar" style={{ background: `linear-gradient(135deg, ${gradient[0]}, ${gradient[1]})` }}>
          {initials}
        </div>
        <div className="patient-card__info">
          <h4 className="patient-card__name">{fullName}</h4>
          <span className="patient-card__meta">{patient.gender || 'Not specified'} <span aria-hidden="true">•</span> {age} years</span>
        </div>
        <div className="patient-card__blood">
          <span>Blood type</span>
          <BloodGroupBadge group={patient.bloodGroup} />
        </div>
      </header>

      <div className="patient-card__record">
        <Detail icon={CreditCard} label="Patient ID" value={patient.nic || `#${patient.patientId}`} />
        <Detail icon={CalendarDays} label="Registered" value={formatDate(patient.createdAt)} />
      </div>

      <div className="patient-card__contact">
        <Detail icon={Phone} label="Phone" value={patient.phoneNumber} />
        <Detail icon={Mail} label="Email" value={patient.email} />
        <Detail icon={MapPin} label="Address" value={patient.address} />
      </div>

      <footer className="patient-card__footer">
        <div className="patient-card__actions">
          <Button variant="ghost" size="sm" icon={Eye} onClick={() => onView?.(patient)} id={`view-patient-${patient.patientId}`}>View</Button>
          <Button variant="secondary" size="sm" icon={Edit2} onClick={() => onEdit?.(patient)} id={`edit-patient-${patient.patientId}`}>Edit</Button>
          <Button variant="danger" size="sm" icon={Trash2} onClick={() => onDelete?.(patient)} id={`delete-patient-${patient.patientId}`}>Delete</Button>
        </div>
      </footer>
    </article>
  )
}

function Detail({ icon: Icon, label, value }) {
  return (
    <div className="patient-card__detail" title={value || undefined}>
      <Icon size={15} aria-hidden="true" />
      <div className="patient-card__detail-content">
        <span className="patient-card__detail-label">{label}</span>
        <span className="patient-card__detail-value">{value || '—'}</span>
      </div>
    </div>
  )
}
