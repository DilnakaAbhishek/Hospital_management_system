import { DoorOpen, Calendar, Clock, Users, Banknote, Edit3, XCircle, Trash2, Lock, Sparkles } from 'lucide-react'
import Button from '../../../components/Button'
import { formatDate } from '../../../lib/utils'
import './DoctorScheduleCard.css'

export default function DoctorScheduleCard({ schedule, onEdit, onCancel, onDelete }) {
  const isPast = new Date(schedule.endAt).getTime() <= Date.now()
  const canEdit = schedule.isActive && !isPast
  const canModifyOrDelete = !schedule.hasPatientBookings

  const formatTimeRange = (startStr, endStr) => {
    const start = new Date(startStr).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    const end = new Date(endStr).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    return `${start} – ${end}`
  }

  const feeFormatted = `LKR ${Number(schedule.consultationFee).toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`

  const capacityPercent = Math.min(100, Math.round((schedule.bookedCount / schedule.capacity) * 100))

  return (
    <article className={`schedule-card-item ${!schedule.isActive ? 'schedule-card-item--cancelled' : ''}`}>
      <div
        className="schedule-card-item__accent"
        style={{
          background: schedule.isActive
            ? 'linear-gradient(180deg, #0ea5e9, #6366f1)'
            : 'linear-gradient(180deg, #94a3b8, #64748b)',
        }}
      />

      <header className="schedule-card-item__header">
        <div
          className="schedule-card-item__avatar"
          style={{
            background: schedule.isActive
              ? 'linear-gradient(135deg, #0284c7, #4f46e5)'
              : 'linear-gradient(135deg, #64748b, #475569)',
          }}
        >
          <DoorOpen size={22} />
        </div>

        <div className="schedule-card-item__info">
          <div className="flex items-center gap-2">
            <h4 className="schedule-card-item__name">Room {schedule.roomNumber}</h4>
          </div>
          <span className="schedule-card-item__meta">
            {schedule.roomName} • {schedule.floor}
          </span>
        </div>

        <div className="schedule-card-item__status-wrap">
          <span className={`schedule-status-badge ${schedule.isActive ? 'status-active' : 'status-cancelled'}`}>
            <span className="badge-dot" />
            {schedule.isActive ? (isPast ? 'Completed' : 'Active') : 'Cancelled'}
          </span>
        </div>
      </header>

      {/* Date & Time Highlight Box */}
      <div className="schedule-card-item__time-box">
        <div className="time-box-item">
          <Calendar size={15} className="text-primary" />
          <span>{formatDate(schedule.startAt)}</span>
        </div>
        <div className="time-box-divider" />
        <div className="time-box-item">
          <Clock size={15} className="text-primary" />
          <span>{formatTimeRange(schedule.startAt, schedule.endAt)}</span>
        </div>
      </div>

      {/* Capacity & Fee Cards */}
      <div className="schedule-card-item__metrics">
        <div className="schedule-metric-card">
          <div className="metric-header">
            <span className="metric-label">
              <Users size={13} className="inline mr-1" />
              Capacity
            </span>
            <span className="metric-value">{schedule.bookedCount} / {schedule.capacity}</span>
          </div>
          <div className="capacity-bar-track">
            <div
              className={`capacity-bar-fill ${capacityPercent >= 100 ? 'full' : ''}`}
              style={{ width: `${capacityPercent}%` }}
            />
          </div>
        </div>

        <div className="schedule-metric-card">
          <span className="metric-label">
            <Banknote size={13} className="inline mr-1" />
            Consultation Fee
          </span>
          <span className="metric-value metric-value--fee">{feeFormatted}</span>
        </div>
      </div>

      {schedule.hasPatientBookings && (
        <div className="schedule-card-item__locked-box">
          <Lock size={14} />
          <span>Locked: {schedule.bookedCount} patient{schedule.bookedCount === 1 ? '' : 's'} registered</span>
        </div>
      )}

      <footer className="schedule-card-item__footer">
        <div className="schedule-card-item__actions">
          {canEdit && (
            <Button
              variant="secondary"
              size="sm"
              icon={Edit3}
              onClick={() => onEdit(schedule)}
              id={`edit-schedule-${schedule.doctorTimeSlotId}`}
            >
              Edit
            </Button>
          )}

          {canModifyOrDelete && schedule.isActive && (
            <Button
              variant="outline"
              size="sm"
              icon={XCircle}
              onClick={() => onCancel(schedule)}
              id={`cancel-schedule-${schedule.doctorTimeSlotId}`}
            >
              Cancel
            </Button>
          )}

          {canModifyOrDelete && (
            <Button
              variant="danger"
              size="sm"
              icon={Trash2}
              onClick={() => onDelete(schedule)}
              id={`delete-schedule-${schedule.doctorTimeSlotId}`}
            >
              Delete
            </Button>
          )}
        </div>
      </footer>
    </article>
  )
}

function Detail({ icon: Icon, label, value }) {
  return (
    <div className="schedule-card-item__detail" title={value || undefined}>
      <Icon size={15} aria-hidden="true" />
      <div className="schedule-card-item__detail-content">
        <span className="schedule-card-item__detail-label">{label}</span>
        <span className="schedule-card-item__detail-value">{value || '—'}</span>
      </div>
    </div>
  )
}
