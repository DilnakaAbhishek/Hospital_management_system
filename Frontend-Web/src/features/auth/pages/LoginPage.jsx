import { Activity, Clock, Eye, EyeOff, Lock, Mail } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import Button from '../../../components/Button'
import './LoginPage.css'
import { login } from '../services/authApi'
import { useAuth } from '../AuthContext'

export default function LoginPage() {
  const navigate = useNavigate()
  const { signIn } = useAuth()
  const [showPass, setShowPass] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState(() => {
    const reason = sessionStorage.getItem('hms_logout_reason')
    if (reason === 'inactive') {
      sessionStorage.removeItem('hms_logout_reason')
      return 'You were signed out after 15 minutes of inactivity for clinical workstation security. Please sign in again.'
    }
    return ''
  })
  const [form, setForm] = useState({ email: '', password: '' })

  const handleChange = e => setForm(p => ({ ...p, [e.target.name]: e.target.value }))

  const handleSubmit = async e => {
    e.preventDefault()
    setError('')

    if (!form.email.trim() || !form.password.trim()) {
      setError('Please enter your email and password.')
      return
    }

    setLoading(true)
    try {
      const user = signIn(await login(form))
      navigate(user.role === 'Doctor' ? '/doctor/dashboard' : user.role === 'Admin' ? '/dashboard' : '/login')
    } catch (requestError) {
      setError(requestError.response?.data?.message ||
        'Cannot reach the hospital server. Start the API on http://localhost:5000 and try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-page__blob login-page__blob--1" />
      <div className="login-page__blob login-page__blob--2" />
      <div className="login-page__blob login-page__blob--3" />

      <div className="login-card glass-card animate-fade-in">
        <div className="login-card__brand">
          <div className="login-card__logo">
            <Activity size={26} strokeWidth={2.5} />
          </div>
          <h1 className="login-card__app-name">Medi<span>Core</span></h1>
        </div>

        <div className="login-card__header">
          <h2 className="login-card__title">Welcome back</h2>
          <p className="login-card__sub">Sign in to Hospital Management System</p>
        </div>

        {notice && (
          <div
            style={{
              margin: '0 0 16px 0',
              padding: '10px 14px',
              background: 'rgba(239, 68, 68, 0.08)',
              border: '1px solid var(--clr-danger, #ef4444)',
              borderRadius: '8px',
              color: 'var(--clr-danger, #ef4444)',
              fontSize: '0.86rem',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              fontWeight: 500,
            }}
          >
            <Clock size={16} />
            <span>{notice}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="login-form" id="login-form">
          <div className="login-field">
            <label className="login-field__label" htmlFor="login-email">Email Address</label>
            <div className="login-field__wrap">
              <Mail size={16} className="login-field__icon" />
              <input
                id="login-email"
                type="email"
                name="email"
                autoComplete="email"
                className="login-field__input"
                placeholder="admin@medicore.lk"
                value={form.email}
                onChange={handleChange}
                required
              />
            </div>
          </div>

          <div className="login-field">
            <label className="login-field__label" htmlFor="login-password">Password</label>
            <div className="login-field__wrap">
              <Lock size={16} className="login-field__icon" />
              <input
                id="login-password"
                type={showPass ? 'text' : 'password'}
                name="password"
                autoComplete="current-password"
                className="login-field__input"
                placeholder="Enter your password"
                value={form.password}
                onChange={handleChange}
                required
              />
              <button
                type="button"
                className="login-field__toggle"
                onClick={() => setShowPass(s => !s)}
                aria-label="Toggle password visibility"
                id="toggle-password"
              >
                {showPass ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>

          <div className="login-form__meta">
            <label className="login-form__remember" htmlFor="remember">
              <input type="checkbox" id="remember" />
              Remember me
            </label>
          </div>

          {error && (
            <p style={{ marginBottom: '10px', color: 'var(--clr-danger, #dc2626)', fontSize: '0.9rem' }}>
              {error}
            </p>
          )}

          <Button
            variant="primary"
            type="submit"
            fullWidth
            loading={loading}
            id="login-submit-btn"
          >
            Sign In
          </Button>

          <p style={{ textAlign: 'center', fontSize: '0.86rem', color: 'var(--text-secondary)', marginTop: '8px' }}>
            Are you a Doctor?{' '}
            <Link to="/doctor/register" style={{ color: 'var(--clr-primary)', fontWeight: 600, textDecoration: 'none' }}>
              Register Here
            </Link>
          </p>
        </form>

        <p className="login-card__footer">
          MediCore HMS &copy; 2026 · All rights reserved
        </p>
      </div>
    </div>
  )
}
