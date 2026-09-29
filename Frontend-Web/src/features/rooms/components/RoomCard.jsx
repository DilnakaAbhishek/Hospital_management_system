import { Building2, Layers, CheckCircle, CheckCircle2, Clock } from 'lucide-react'
import Button from '../../../components/Button'
import './RoomCard.css'

export default function RoomCard({ room, onConfirm }) {
  const isConfirmed = room.isConfirmed

  return (
    <article className="room-card-item">
      <div
        className="room-card-item__accent"
        style={{
          background: isConfirmed
            ? 'linear-gradient(180deg, #0ea5e9, #6366f1)'
            : 'linear-gradient(180deg, #f59e0b, #d97706)',
        }}
      />

      <header className="room-card-item__header">
        <div
          className="room-card-item__avatar"
          style={{
            background: isConfirmed
              ? 'linear-gradient(135deg, #0ea5e9, #6366f1)'
              : 'linear-gradient(135deg, #f59e0b, #d97706)',
          }}
        >
          <Building2 size={24} />
        </div>

        <div className="room-card-item__info">
          <h4 className="room-card-item__name">Room {room.roomNumber}</h4>
          <span className="room-card-item__meta">{room.roomName}</span>
        </div>
      </header>

      <div className="room-card-item__record">
        <Detail icon={Layers} label="Floor Level" value={room.floor || '—'} />
        <Detail
          icon={isConfirmed ? CheckCircle2 : Clock}
          label="Status"
          value={isConfirmed ? 'Confirmed' : 'Pending'}
        />
      </div>

      <footer className="room-card-item__footer">
        {isConfirmed ? (
          <div className="room-card-item__result confirmed">
            <CheckCircle2 size={16} />
            <span>Ready for Scheduling</span>
          </div>
        ) : (
          <div className="room-card-item__actions">
            <Button
              variant="primary"
              size="sm"
              icon={CheckCircle}
              onClick={() => onConfirm(room)}
              id={`confirm-room-${room.roomId}`}
            >
              Confirm Room
            </Button>
          </div>
        )}
      </footer>
    </article>
  )
}

function Detail({ icon: Icon, label, value }) {
  return (
    <div className="room-card-item__detail" title={value || undefined}>
      <Icon size={15} aria-hidden="true" />
      <div className="room-card-item__detail-content">
        <span className="room-card-item__detail-label">{label}</span>
        <span className="room-card-item__detail-value">{value || '—'}</span>
      </div>
    </div>
  )
}
