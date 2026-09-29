import { Phone, Mail, CreditCard, CalendarDays, Stethoscope, FileText, Check, X, UserCheck, UserX, AlertCircle } from 'lucide-react'
import Button from '../../../components/Button'
import { formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import './DoctorCard.css'

export default function DoctorCard({ doctor, onReview, onApproveDeletion, onRejectDeletion }) {
  const gradient = nameToGradient(doctor.firstName || doctor.fullName)
  const fullName = `Dr. ${doctor.fullName || `${doctor.firstName} ${doctor.lastName}`}`
  const initials = getInitials(doctor.firstName || doctor.fullName, doctor.lastName || '')

  const statusClass = doctor.registrationStatus ? doctor.registrationStatus.toLowerCase() : 'pending'

  return (
    <article className="doctor-card-item">
      <div
        className="doctor-card-item__accent"
        style={{ background: `linear-gradient(180deg, ${gradient[0]}, ${gradient[1]})` }}
      />

      <header className="doctor-card-item__header">
        <div
          className="doctor-card-item__avatar"
          style={{ background: `linear-gradient(135deg, ${gradient[0]}, ${gradient[1]})` }}
        >
          {initials}
        </div>

        <div className="doctor-card-item__info">
          <h4 className="doctor-card-item__name" title={fullName}>{fullName}</h4>
          <span className="doctor-card-item__meta">{doctor.specialization || 'General Doctor'}</span>
        </div>

        <div className="doctor-card-item__status-wrap">
          <span className={`doctor-status-badge status-${statusClass}`}>
            {doctor.registrationStatus === 'DeletionPending' ? 'Deletion Requested' : doctor.registrationStatus}
          </span>
        </div>
      </header>

      <div className="doctor-card-item__record">
        <Detail icon={Stethoscope} label="Specialization" value={doctor.specialization} />
        <Detail icon={FileText} label="SLMC License" value={doctor.slmcLicenseNumber} />
      </div>

      <div className="doctor-card-item__contact">
        <Detail icon={CreditCard} label="NIC" value={doctor.nic} />
        <Detail icon={Phone} label="Phone" value={doctor.phoneNumber} />
        <Detail icon={Mail} label="Email" value={doctor.email} />
        <Detail icon={CalendarDays} label="Submitted" value={formatDate(doctor.createdAt)} />
      </div>

      {doctor.declineReason && doctor.registrationStatus !== 'DeletionPending' && (
        <div className="doctor-card-item__decline-box">
          <AlertCircle size={15} />
          <span><strong>Reason:</strong> {doctor.declineReason}</span>
        </div>
      )}

      {doctor.registrationStatus === 'DeletionPending' && doctor.declineReason && (
        <div className="doctor-card-item__decline-box">
          <AlertCircle size={15} />
          <span><strong>Deletion Reason:</strong> {doctor.declineReason}</span>
        </div>
      )}

      <footer className="doctor-card-item__footer">
        {doctor.registrationStatus === 'Pending' && (
          <div className="doctor-card-item__actions">
            <Button
              variant="primary"
              size="sm"
              icon={Check}
              onClick={() => onReview(doctor, true)}
              id={`approve-doctor-${doctor.doctorId}`}
            >
              Approve
            </Button>
            <Button
              variant="danger"
              size="sm"
              icon={X}
              onClick={() => onReview(doctor, false)}
              id={`decline-doctor-${doctor.doctorId}`}
            >
              Decline
            </Button>
          </div>
        )}

        {doctor.registrationStatus === 'DeletionPending' && (
          <div className="doctor-card-item__actions">
            <Button
              variant="danger"
              size="sm"
              icon={UserX}
              onClick={() => onApproveDeletion && onApproveDeletion(doctor)}
              id={`approve-deletion-doctor-${doctor.doctorId}`}
            >
              Approve Deletion
            </Button>
            <Button
              variant="secondary"
              size="sm"
              icon={Check}
              onClick={() => onRejectDeletion && onRejectDeletion(doctor)}
              id={`reject-deletion-doctor-${doctor.doctorId}`}
            >
              Cancel Deletion
            </Button>
          </div>
        )}

        {doctor.registrationStatus === 'Approved' && (
          <div className="doctor-card-item__result approved">
            <UserCheck size={16} />
            <span>Login Enabled</span>
          </div>
        )}

        {doctor.registrationStatus === 'Declined' && (
          <div className="doctor-card-item__result declined">
            <UserX size={16} />
            <span>Registration Declined</span>
          </div>
        )}
      </footer>
    </article>
  )
}

function Detail({ icon: Icon, label, value }) {
  return (
    <div className="doctor-card-item__detail" title={value || undefined}>
      <Icon size={15} aria-hidden="true" />
      <div className="doctor-card-item__detail-content">
        <span className="doctor-card-item__detail-label">{label}</span>
        <span className="doctor-card-item__detail-value">{value || '—'}</span>
      </div>
    </div>
  )
}
