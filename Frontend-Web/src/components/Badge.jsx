import { BLOOD_GROUP_COLORS } from '../lib/utils'
import './Badge.css'

const VARIANT_STYLES = {
  primary:  'badge--primary',
  success:  'badge--success',
  warning:  'badge--warning',
  danger:   'badge--danger',
  info:     'badge--info',
  default:  'badge--default',
  accent:   'badge--accent',
}

export default function Badge({ children, variant = 'default', dot = false }) {
  return (
    <span className={`badge ${VARIANT_STYLES[variant] || 'badge--default'}`}>
      {dot && <span className="badge__dot" />}
      {children}
    </span>
  )
}

export function BloodGroupBadge({ group }) {
  const color = BLOOD_GROUP_COLORS[group] || '#94a3b8'
  return (
    <span
      className="badge badge--blood"
      style={{
        '--blood-color': color,
        background: `${color}20`,
        borderColor: `${color}40`,
        color: color,
      }}
    >
      {group || '—'}
    </span>
  )
}

export function StatusBadge({ active = true }) {
  return (
    <Badge variant={active ? 'success' : 'default'} dot>
      {active ? 'Active' : 'Inactive'}
    </Badge>
  )
}
