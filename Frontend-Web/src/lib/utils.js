export function formatDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

export function calculateAge(dob) {
  if (!dob) return '—'
  const today = new Date()
  const birth = new Date(dob)
  let age = today.getFullYear() - birth.getFullYear()
  const m = today.getMonth() - birth.getMonth()
  if (m < 0 || (m === 0 && today.getDate() < birth.getDate())) age--
  return age
}

export function getInitials(firstName = '', lastName = '') {
  return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase()
}

export const BLOOD_GROUP_COLORS = {
  'A+': '#ef4444', 'A-': '#f87171',
  'B+': '#f59e0b', 'B-': '#fbbf24',
  'AB+': '#8b5cf6', 'AB-': '#a78bfa',
  'O+': '#10b981', 'O-': '#34d399',
}

export function truncate(str = '', max = 40) {
  return str.length > max ? str.slice(0, max) + '…' : str
}

export function debounce(fn, delay = 300) {
  let timer
  return (...args) => {
    clearTimeout(timer)
    timer = setTimeout(() => fn(...args), delay)
  }
}

export function nameToGradient(name = '') {
  const colors = [
    ['#0ea5e9', '#6366f1'],
    ['#10b981', '#0ea5e9'],
    ['#f59e0b', '#ef4444'],
    ['#8b5cf6', '#ec4899'],
    ['#06b6d4', '#10b981'],
  ]
  const idx = name.charCodeAt(0) % colors.length
  return colors[idx]
}
