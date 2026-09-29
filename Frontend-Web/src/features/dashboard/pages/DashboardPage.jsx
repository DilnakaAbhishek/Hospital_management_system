import { useState, useEffect } from 'react'
import { Users, Stethoscope, Calendar, TrendingUp, ArrowUp, ArrowDown, Loader2 } from 'lucide-react'
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar
} from 'recharts'
import { dashboardApi } from '../services/dashboardApi'
import { BloodGroupBadge } from '../../../components/Badge'
import { formatDate, calculateAge, getInitials, nameToGradient } from '../../../lib/utils'
import './DashboardPage.css'

const CustomTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null
  return (
    <div className="chart-tooltip">
      <p className="chart-tooltip__label">{label}</p>
      {payload.map(entry => (
        <p key={entry.name} style={{ color: entry.color, fontSize: '0.82rem' }}>
          {entry.name}: <strong>{entry.value}</strong>
        </p>
      ))}
    </div>
  )
}

export default function DashboardPage() {
  const [data, setData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const fetchStats = () => {
    setLoading(true)
    setError(null)
    dashboardApi.getAdminStats()
      .then(res => {
        setData(res)
        setLoading(false)
      })
      .catch(err => {
        console.error('Failed to load dashboard stats:', err)
        const msg = err.response?.data?.message || err.message || 'Failed to load dashboard data. Please make sure backend is running.'
        setError(msg)
        setLoading(false)
      })
  }

  useEffect(() => {
    fetchStats()
  }, [])

  const statCards = [
    {
      id: 'stat-patients',
      label: data?.patientsStat?.label || 'Total Patients',
      value: data?.patientsStat?.value ?? '0',
      change: data?.patientsStat?.change ?? '0%',
      up: data?.patientsStat?.up ?? true,
      icon: Users,
      color: 'var(--clr-primary)',
      bg: 'rgba(14,165,233,0.1)',
    },
    {
      id: 'stat-doctors',
      label: data?.doctorsStat?.label || 'Active Doctors',
      value: data?.doctorsStat?.value ?? '0',
      change: data?.doctorsStat?.change ?? '0',
      up: data?.doctorsStat?.up ?? true,
      icon: Stethoscope,
      color: 'var(--clr-accent)',
      bg: 'rgba(99,102,241,0.1)',
    },
    {
      id: 'stat-appointments',
      label: data?.appointmentsStat?.label || "Today's Appointments",
      value: data?.appointmentsStat?.value ?? '0',
      change: data?.appointmentsStat?.change ?? '0%',
      up: data?.appointmentsStat?.up ?? true,
      icon: Calendar,
      color: 'var(--clr-warning)',
      bg: 'rgba(245,158,11,0.1)',
    },
    {
      id: 'stat-revenue',
      label: data?.revenueStat?.label || 'Monthly Revenue',
      value: data?.revenueStat?.value ?? 'Rs 0',
      change: data?.revenueStat?.change ?? '0%',
      up: data?.revenueStat?.up ?? true,
      icon: TrendingUp,
      color: 'var(--clr-success)',
      bg: 'rgba(16,185,129,0.1)',
    },
  ]

  const monthlyTrends = data?.monthlyTrends || []
  const recentPatients = data?.recentPatients || []

  if (loading) {
    return (
      <div className="page-wrapper" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '300px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', color: 'var(--text-muted)' }}>
          <Loader2 className="animate-spin" size={24} />
          <span>Loading dashboard analytics...</span>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="page-wrapper">
        <div className="glass-card" style={{ padding: '24px', textAlign: 'center' }}>
          <p style={{ color: 'var(--clr-danger)', marginBottom: '16px' }}>{error}</p>
          <button
            className="btn btn--primary"
            onClick={fetchStats}
            style={{ padding: '8px 20px', cursor: 'pointer' }}
          >
            Retry Loading Dashboard
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="page-wrapper">
      <div className="grid-cols-4 stagger-children animate-fade-in" style={{ marginBottom: '28px' }}>
        {statCards.map(stat => {
          const Icon = stat.icon
          return (
            <div key={stat.id} id={stat.id} className="stat-card glass-card">
              <div className="stat-card__top">
                <div className="stat-card__icon" style={{ background: stat.bg, color: stat.color }}>
                  <Icon size={20} />
                </div>
                <span className={`stat-card__change ${stat.up ? 'stat-card__change--up' : 'stat-card__change--down'}`}>
                  {stat.up ? <ArrowUp size={11} /> : <ArrowDown size={11} />}
                  {stat.change}
                </span>
              </div>
              <div className="stat-card__value">{stat.value}</div>
              <div className="stat-card__label">{stat.label}</div>
            </div>
          )
        })}
      </div>

      <div className="dashboard__charts animate-fade-in" style={{ animationDelay: '0.1s' }}>
        <div className="glass-card chart-card">
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">Patient Admissions & Appointments</h3>
              <p className="chart-card__sub">Monthly trend analytics</p>
            </div>
            <div className="chart-legend">
              <span className="chart-legend__dot" style={{ background: 'var(--clr-primary)' }} />
              <span>Patients</span>
              <span className="chart-legend__dot" style={{ background: 'var(--clr-accent)' }} />
              <span>Appointments</span>
            </div>
          </div>
          <ResponsiveContainer width="100%" height={220}>
            <AreaChart data={monthlyTrends} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
              <defs>
                <linearGradient id="colPrimary" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#0ea5e9" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#0ea5e9" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="colAccent" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#6366f1" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#6366f1" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.08)" />
              <XAxis dataKey="month" tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Area type="monotone" dataKey="patients" name="Patients" stroke="#0ea5e9" strokeWidth={2} fill="url(#colPrimary)" />
              <Area type="monotone" dataKey="appointments" name="Appointments" stroke="#6366f1" strokeWidth={2} fill="url(#colAccent)" />
            </AreaChart>
          </ResponsiveContainer>
        </div>

        <div className="glass-card chart-card">
          <div className="chart-card__header">
            <div>
              <h3 className="chart-card__title">Recent Activity Breakdown</h3>
              <p className="chart-card__sub">Monthly comparisons</p>
            </div>
          </div>
          <ResponsiveContainer width="100%" height={220}>
            <BarChart data={monthlyTrends.slice(-4)} margin={{ top: 5, right: 10, left: -20, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.08)" />
              <XAxis dataKey="month" tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fill: 'var(--text-muted)', fontSize: 11 }} axisLine={false} tickLine={false} />
              <Tooltip content={<CustomTooltip />} />
              <Bar dataKey="patients" name="Patients" fill="#0ea5e9" radius={[4,4,0,0]} opacity={0.85} />
              <Bar dataKey="appointments" name="Appointments" fill="#6366f1" radius={[4,4,0,0]} opacity={0.85} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="glass-card animate-fade-in" style={{ animationDelay: '0.2s', marginTop: '28px' }}>
        <div className="chart-card__header" style={{ padding: '20px 24px', borderBottom: '1px solid var(--border-default)' }}>
          <div>
            <h3 className="chart-card__title">Recent Patients</h3>
            <p className="chart-card__sub">Latest registrations</p>
          </div>
          <a href="/patients" className="dashboard__link" id="view-all-patients">View all →</a>
        </div>
        <div className="recent-patients">
          {recentPatients.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-muted)' }}>
              No recent patients registered yet.
            </div>
          ) : (
            recentPatients.map((p, i) => {
              const [c1, c2] = nameToGradient(p.firstName || 'P')
              return (
                <div key={p.patientId} className="recent-patient" style={{ animationDelay: `${i * 0.06}s` }}>
                  <div
                    className="recent-patient__avatar"
                    style={{ background: `linear-gradient(135deg, ${c1}, ${c2})` }}
                  >
                    {getInitials(p.firstName, p.lastName)}
                  </div>
                  <div className="recent-patient__info">
                    <div className="recent-patient__name">{p.firstName} {p.lastName}</div>
                    <div className="recent-patient__meta">{p.gender} · {calculateAge(p.dateOfBirth)} yrs</div>
                  </div>
                  <BloodGroupBadge group={p.bloodGroup} />
                  <div className="recent-patient__date">{formatDate(p.createdAt)}</div>
                </div>
              )
            })
          )}
        </div>
      </div>
    </div>
  )
}
