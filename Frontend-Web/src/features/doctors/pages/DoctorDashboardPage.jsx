import { useState, useEffect, useMemo } from 'react'
import { useLocation } from 'react-router-dom'
import {
  Calendar,
  Clock,
  Users,
  CheckCircle2,
  CalendarCheck,
  Stethoscope,
  BarChart3,
  LineChart,
  Search,
  Loader2,
  MapPin,
  Sparkles,
  Phone,
  UserCheck,
  RefreshCw
} from 'lucide-react'
import {
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer
} from 'recharts'
import { useAuth } from '../../auth/AuthContext'
import { appointmentApi } from '../../appointments/services/appointmentApi'
import { formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import Badge from '../../../components/Badge'
import './DoctorDashboardPage.css'
import '../../dashboard/pages/DashboardPage.css'

const DAYS_OF_WEEK = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

const isSameDay = (d1, d2) => {
  if (!d1 || !d2) return false
  const date1 = new Date(d1)
  const date2 = new Date(d2)
  return (
    date1.getFullYear() === date2.getFullYear() &&
    date1.getMonth() === date2.getMonth() &&
    date1.getDate() === date2.getDate()
  )
}

const ChartTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null
  return (
    <div className="chart-tooltip">
      <p className="chart-tooltip__label">{label}</p>
      {payload.map((entry) => (
        <p key={entry.name} style={{ color: entry.color, fontSize: '0.82rem', margin: '2px 0' }}>
          {entry.name}: <strong>{entry.value} patients</strong>
        </p>
      ))}
    </div>
  )
}

export default function DoctorDashboardPage() {
  const { user } = useAuth()
  const location = useLocation()
  const [appointments, setAppointments] = useState([])
  const [loading, setLoading] = useState(true)
  const [chartType, setChartType] = useState('bar') // 'area' | 'bar'
  const [timeFilter, setTimeFilter] = useState('all') // 'all' | 'today' | 'tomorrow' | 'week'
  const [searchQuery, setSearchQuery] = useState('')

  const fetchAppointments = async () => {
    setLoading(true)
    try {
      const res = await appointmentApi.getAll({ pageSize: 50, sortBy: 'startAt', sortDirection: 'asc' })
      const list = res?.data || (Array.isArray(res) ? res : [])
      console.log('Doctor dashboard loaded appointments:', list)
      setAppointments(list)
    } catch (err) {
      console.error('Failed to fetch doctor appointments:', err)
      setAppointments([])
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchAppointments()
  }, [])

  useEffect(() => {
    if (!loading && (location.hash === '#upcoming-appointments' || window.location.hash === '#upcoming-appointments')) {
      const timer = setTimeout(() => {
        const el = document.getElementById('upcoming-appointments')
        if (el) {
          el.scrollIntoView({ behavior: 'smooth', block: 'start' })
        }
      }, 80)
      return () => clearTimeout(timer)
    }
  }, [loading, location.hash, location.key])

  const now = new Date()
  const tomorrow = new Date(now)
  tomorrow.setDate(tomorrow.getDate() + 1)

  // Filter upcoming & scheduled appointments (status is not cancelled)
  const upcomingList = useMemo(() => {
    return appointments
      .filter((appt) => appt.status !== 'Cancelled')
      .sort((a, b) => new Date(a.startAt || 0) - new Date(b.startAt || 0))
  }, [appointments])

  // Today's appointments count (local date check)
  const todayCount = useMemo(() => {
    return appointments.filter((a) => {
      if (a.status === 'Cancelled') return false
      return isSameDay(a.startAt, now)
    }).length
  }, [appointments, now])

  // Completed count
  const completedCount = useMemo(() => {
    return appointments.filter((a) => a.status === 'Completed').length
  }, [appointments])

  // Build weekly consultation volume data from actual doctor appointments (Monday to Sunday)
  const weeklyVolumeData = useMemo(() => {
    const counts = { Mon: 0, Tue: 0, Wed: 0, Thu: 0, Fri: 0, Sat: 0, Sun: 0 }
    
    appointments.forEach((appt) => {
      if (appt.startAt && appt.status !== 'Cancelled') {
        const dateObj = new Date(appt.startAt)
        const dayIdx = dateObj.getDay() // 0 = Sun, 1 = Mon...
        const dayName = dayIdx === 0 ? 'Sun' : DAYS_OF_WEEK[dayIdx - 1]
        if (counts[dayName] !== undefined) {
          counts[dayName] += 1
        }
      }
    })

    return DAYS_OF_WEEK.map((day) => ({
      day,
      Consultations: counts[day] || 0
    }))
  }, [appointments])

  const totalWeeklyConsultations = useMemo(() => {
    return weeklyVolumeData.reduce((sum, item) => sum + item.Consultations, 0)
  }, [weeklyVolumeData])

  // Filtered upcoming appointments based on user selection and search
  const filteredAppointments = useMemo(() => {
    return upcomingList.filter((appt) => {
      // Time filter
      if (timeFilter === 'today') {
        if (!isSameDay(appt.startAt, now)) return false
      } else if (timeFilter === 'tomorrow') {
        if (!isSameDay(appt.startAt, tomorrow)) return false
      } else if (timeFilter === 'week') {
        if (appt.startAt) {
          const apptDate = new Date(appt.startAt)
          const inSevenDays = new Date(now)
          inSevenDays.setDate(inSevenDays.getDate() + 7)
          if (apptDate > inSevenDays) return false
        }
      }

      // Search query filter
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase()
        const name = (appt.patientName || '').toLowerCase()
        const reason = (appt.reason || '').toLowerCase()
        const type = (appt.appointmentType || '').toLowerCase()
        const phone = (appt.patientPhone || '').toLowerCase()
        return name.includes(q) || reason.includes(q) || type.includes(q) || phone.includes(q)
      }

      return true
    })
  }, [upcomingList, timeFilter, searchQuery, now, tomorrow])

  const formatAppointmentTime = (dateStr) => {
    if (!dateStr) return 'TBD'
    const date = new Date(dateStr)
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  }

  const formatAppointmentDate = (dateStr) => {
    if (!dateStr) return 'Scheduled'
    if (isSameDay(dateStr, now)) return 'Today'
    if (isSameDay(dateStr, tomorrow)) return 'Tomorrow'
    return formatDate(dateStr)
  }

  if (loading) {
    return (
      <div className="page-wrapper" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '350px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: 'var(--text-muted)' }}>
          <Loader2 className="animate-spin" size={24} />
          <span>Loading doctor dashboard...</span>
        </div>
      </div>
    )
  }

  return (
    <div className="page-wrapper doctor-dashboard animate-fade-in">
      {/* Welcome Banner */}
      <div className="doctor-welcome-banner">
        <div className="doctor-welcome__text">
          <h2>Welcome back, Dr. {user?.fullName || 'Doctor'}</h2>
          <p>Here is your real-time clinical schedule and patient consultation overview</p>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <button
            onClick={fetchAppointments}
            className="btn btn--outline"
            style={{ display: 'flex', alignItems: 'center', gap: '6px', padding: '6px 14px', fontSize: '0.82rem' }}
            title="Refresh dashboard"
          >
            <RefreshCw size={14} /> Refresh
          </button>
          <div className="doctor-welcome__badge">
            <Sparkles size={16} />
            <span>Active Practice Mode</span>
          </div>
        </div>
      </div>

      {/* Overview Stat Cards */}
      <div className="doctor-stats-grid stagger-children">
        <div className="stat-card glass-card">
          <div className="stat-card__top">
            <div className="stat-card__icon" style={{ background: 'rgba(14,165,233,0.12)', color: 'var(--clr-primary)' }}>
              <Clock size={20} />
            </div>
            <span className="stat-card__change stat-card__change--up">Today</span>
          </div>
          <div className="stat-card__value">{todayCount}</div>
          <div className="stat-card__label">Today's Appointments</div>
        </div>

        <div className="stat-card glass-card">
          <div className="stat-card__top">
            <div className="stat-card__icon" style={{ background: 'rgba(99,102,241,0.12)', color: 'var(--clr-accent)' }}>
              <CalendarCheck size={20} />
            </div>
            <span className="stat-card__change stat-card__change--up">Active</span>
          </div>
          <div className="stat-card__value">{upcomingList.length}</div>
          <div className="stat-card__label">Total Scheduled</div>
        </div>

        <div className="stat-card glass-card">
          <div className="stat-card__top">
            <div className="stat-card__icon" style={{ background: 'rgba(16,185,129,0.12)', color: 'var(--clr-success)' }}>
              <CheckCircle2 size={20} />
            </div>
            <span className="stat-card__change stat-card__change--up">Finished</span>
          </div>
          <div className="stat-card__value">{completedCount}</div>
          <div className="stat-card__label">Completed Consultations</div>
        </div>

        <div className="stat-card glass-card">
          <div className="stat-card__top">
            <div className="stat-card__icon" style={{ background: 'rgba(245,158,11,0.12)', color: 'var(--clr-warning)' }}>
              <Users size={20} />
            </div>
            <span className="stat-card__change stat-card__change--up">This Week</span>
          </div>
          <div className="stat-card__value">{totalWeeklyConsultations}</div>
          <div className="stat-card__label">Weekly Patient Volume</div>
        </div>
      </div>

      {/* Main Grid: Weekly Patient Volume Chart */}
      <div className="doctor-main-grid">
        <div className="glass-card chart-card doctor-chart-section">
          <div className="chart-card__header">
            <div>
              <div className="chart-card__title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Stethoscope size={18} style={{ color: 'var(--clr-primary)' }} />
                Weekly Patient Volume
              </div>
              <div className="chart-card__sub">
                Daily patient consultations comparison across the week ({totalWeeklyConsultations} total scheduled visits)
              </div>
            </div>

            <div className="chart-header-actions">
              <div className="chart-type-toggle">
                <button
                  className={`chart-type-btn ${chartType === 'area' ? 'chart-type-btn--active' : ''}`}
                  onClick={() => setChartType('area')}
                  title="Area Chart"
                >
                  <LineChart size={14} /> Area
                </button>
                <button
                  className={`chart-type-btn ${chartType === 'bar' ? 'chart-type-btn--active' : ''}`}
                  onClick={() => setChartType('bar')}
                  title="Bar Chart"
                >
                  <BarChart3 size={14} /> Bar
                </button>
              </div>
            </div>
          </div>

          <div style={{ width: '100%', height: 260 }}>
            <ResponsiveContainer>
              {chartType === 'area' ? (
                <AreaChart data={weeklyVolumeData} margin={{ top: 10, right: 20, left: -10, bottom: 0 }}>
                  <defs>
                    <linearGradient id="doctorConsultationGrad" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--clr-primary)" stopOpacity={0.4} />
                      <stop offset="95%" stopColor="var(--clr-primary)" stopOpacity={0.0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" vertical={false} />
                  <XAxis dataKey="day" stroke="var(--text-muted)" tick={{ fontSize: 12 }} axisLine={false} tickLine={false} />
                  <YAxis stroke="var(--text-muted)" tick={{ fontSize: 12 }} axisLine={false} tickLine={false} allowDecimals={false} />
                  <Tooltip content={<ChartTooltip />} />
                  <Area
                    type="monotone"
                    dataKey="Consultations"
                    stroke="var(--clr-primary)"
                    strokeWidth={3}
                    fillOpacity={1}
                    fill="url(#doctorConsultationGrad)"
                  />
                </AreaChart>
              ) : (
                <BarChart data={weeklyVolumeData} margin={{ top: 10, right: 20, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" vertical={false} />
                  <XAxis dataKey="day" stroke="var(--text-muted)" tick={{ fontSize: 12 }} axisLine={false} tickLine={false} />
                  <YAxis stroke="var(--text-muted)" tick={{ fontSize: 12 }} axisLine={false} tickLine={false} allowDecimals={false} />
                  <Tooltip content={<ChartTooltip />} />
                  <Bar
                    dataKey="Consultations"
                    fill="var(--clr-primary)"
                    radius={[6, 6, 0, 0]}
                    maxBarSize={44}
                  />
                </BarChart>
              )}
            </ResponsiveContainer>
          </div>
        </div>

        {/* Upcoming Appointments Section (View Only - No edit or cancel) */}
        <div className="upcoming-section" id="upcoming-appointments">
          <div className="upcoming-header">
            <div>
              <h3 style={{ fontSize: '1.2rem', fontWeight: '700', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Calendar size={20} style={{ color: 'var(--clr-accent)' }} />
                Upcoming Appointments
              </h3>
              <p style={{ fontSize: '0.86rem', color: 'var(--text-muted)' }}>
                Scheduled consultations assigned to your calendar ({filteredAppointments.length} showing)
              </p>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
              {/* Filter Tabs */}
              <div className="upcoming-filters">
                <button
                  className={`upcoming-filter-btn ${timeFilter === 'all' ? 'upcoming-filter-btn--active' : ''}`}
                  onClick={() => setTimeFilter('all')}
                >
                  All ({upcomingList.length})
                </button>
                <button
                  className={`upcoming-filter-btn ${timeFilter === 'today' ? 'upcoming-filter-btn--active' : ''}`}
                  onClick={() => setTimeFilter('today')}
                >
                  Today ({todayCount})
                </button>
                <button
                  className={`upcoming-filter-btn ${timeFilter === 'tomorrow' ? 'upcoming-filter-btn--active' : ''}`}
                  onClick={() => setTimeFilter('tomorrow')}
                >
                  Tomorrow
                </button>
                <button
                  className={`upcoming-filter-btn ${timeFilter === 'week' ? 'upcoming-filter-btn--active' : ''}`}
                  onClick={() => setTimeFilter('week')}
                >
                  Next 7 Days
                </button>
              </div>

              {/* Search Bar */}
              <div className="upcoming-search-wrapper">
                <Search size={16} className="upcoming-search-icon" />
                <input
                  type="text"
                  placeholder="Search patient, symptoms..."
                  className="upcoming-search-input"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* Cards Grid */}
          {filteredAppointments.length === 0 ? (
            <div className="appointments-empty-state">
              <div className="empty-state-icon">
                <UserCheck size={28} />
              </div>
              <h3>No Upcoming Appointments Found</h3>
              <p>
                {searchQuery || timeFilter !== 'all'
                  ? 'No appointments matched your search criteria.'
                  : 'You have no pending upcoming consultations scheduled at this time.'}
              </p>
            </div>
          ) : (
            <div className="appointments-card-list">
              {filteredAppointments.map((appt) => {
                const [c1, c2] = nameToGradient(appt.patientName || 'Patient')
                const dateLabel = formatAppointmentDate(appt.startAt)
                const timeLabel = formatAppointmentTime(appt.startAt)

                return (
                  <div key={appt.appointmentId || appt.id || Math.random()} className="glass-card appointment-view-card">
                    <div className="appointment-card__top">
                      <div className="appointment-patient-row">
                        <div
                          className="appointment-patient-avatar"
                          style={{ background: `linear-gradient(135deg, ${c1}, ${c2})` }}
                        >
                          {getInitials(
                            (appt.patientName || 'P').split(' ')[0],
                            (appt.patientName || '').split(' ')[1] || ''
                          )}
                        </div>
                        <div>
                          <div className="appointment-patient-name">{appt.patientName || 'Anonymous Patient'}</div>
                          <span className="appointment-patient-type">{appt.appointmentType || 'Consultation'}</span>
                        </div>
                      </div>

                      <Badge variant={appt.status === 'Confirmed' ? 'success' : 'primary'} dot>
                        {appt.status || 'Confirmed'}
                      </Badge>
                    </div>

                    <div className="appointment-card__meta-grid">
                      <div className="meta-item">
                        <Calendar size={14} className="meta-item__icon" />
                        <span><strong>{dateLabel}</strong></span>
                      </div>
                      <div className="meta-item">
                        <Clock size={14} className="meta-item__icon" />
                        <span>{timeLabel}</span>
                      </div>
                      {appt.patientPhone && (
                        <div className="meta-item">
                          <Phone size={14} className="meta-item__icon" />
                          <span>{appt.patientPhone}</span>
                        </div>
                      )}
                      {appt.roomName ? (
                        <div className="meta-item">
                          <MapPin size={14} className="meta-item__icon" />
                          <span>{appt.roomName}</span>
                        </div>
                      ) : appt.specialty ? (
                        <div className="meta-item">
                          <Stethoscope size={14} className="meta-item__icon" />
                          <span>{appt.specialty}</span>
                        </div>
                      ) : null}
                    </div>

                    {appt.reason && (
                      <div className="appointment-card__reason">
                        <strong>Reason: </strong>
                        <span>{appt.reason}</span>
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
