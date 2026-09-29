import { Activity, ArrowLeft, CheckCircle, Eye, EyeOff } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import Button from '../../../components/Button'
import { registerDoctor } from '../services/authApi'
import './LoginPage.css'

const initialForm = { firstName: '', lastName: '', email: '', nic: '', specialization: '', slmcLicenseNumber: '', phoneNumber: '', password: '', confirmPassword: '' }
const nicPattern = /^(?:\d{9}[vVxX]|\d{12})$/
const phonePattern = /^(?:07\d{8}|\+947\d{8})$/

export default function RegisterPage() {
  const [form, setForm] = useState(initialForm)
  const [showPass, setShowPass] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [submitted, setSubmitted] = useState(false)

  const handleSubmit = async event => {
    event.preventDefault()
    setError('')
    if (!nicPattern.test(form.nic.trim())) return setError('Enter a valid Sri Lankan NIC: 9 digits plus V/X or 12 digits.')
    if (!phonePattern.test(form.phoneNumber.replace(/[ -]/g, ''))) return setError('Enter a valid Sri Lankan mobile number: 07XXXXXXXX or +947XXXXXXXX.')
    if (form.password.length < 8 || !/[A-Za-z]/.test(form.password) || !/\d/.test(form.password)) return setError('Password must be at least 8 characters with a letter and a number.')
    if (form.password !== form.confirmPassword) return setError('Passwords do not match.')
    setLoading(true)
    try { await registerDoctor(form); setSubmitted(true) }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to submit registration.') }
    finally { setLoading(false) }
  }

  const change = event => setForm(current => ({ ...current, [event.target.name]: event.target.value }))

  return (
    <div className="login-page doctor-registration-page">
      <div className="login-page__blob login-page__blob--1" />
      <div className="login-page__blob login-page__blob--2" />
      <div className="login-card doctor-registration-card glass-card animate-fade-in">
        <div className="login-card__brand"><div className="login-card__logo"><Activity size={26} /></div><h1 className="login-card__app-name">Medi<span>Core</span></h1></div>
        {submitted ? (
          <div className="registration-success">
            <CheckCircle size={54} />
            <h2>Request submitted</h2>
            <p>Your doctor registration is pending administrator approval. You can sign in only after it is approved.</p>
            <Link to="/login"><Button variant="primary" fullWidth>Return to Login</Button></Link>
          </div>
        ) : (
          <>
            <div className="login-card__header"><h2 className="login-card__title">Doctor Registration</h2><p className="login-card__sub">Submit your professional details for administrator review</p></div>
            <form onSubmit={handleSubmit} className="login-form">
              <div className="registration-grid">
                <Field label="First Name" name="firstName" value={form.firstName} onChange={change} />
                <Field label="Second Name" name="lastName" value={form.lastName} onChange={change} />
                <Field label="Email" name="email" type="email" value={form.email} onChange={change} />
                <Field label="Sri Lankan NIC" name="nic" placeholder="200012345678 or 901234567V" value={form.nic} onChange={change} />
                <Field label="Specialization" name="specialization" placeholder="Cardiology" value={form.specialization} onChange={change} />
                <Field label="SLMC License Number" name="slmcLicenseNumber" placeholder="SLMC-12345" value={form.slmcLicenseNumber} onChange={change} />
                <Field label="Sri Lankan Phone Number" name="phoneNumber" placeholder="0771234567" value={form.phoneNumber} onChange={change} />
                <div />
                <PasswordField label="Password" name="password" value={form.password} onChange={change} visible={showPass} toggle={() => setShowPass(value => !value)} />
                <PasswordField label="Confirm Password" name="confirmPassword" value={form.confirmPassword} onChange={change} visible={showPass} />
              </div>
              {error && <p className="form-error">{error}</p>}
              <Button variant="primary" type="submit" fullWidth loading={loading}>Submit Registration</Button>
              <Link className="registration-back" to="/login"><ArrowLeft size={15} /> Back to login</Link>
            </form>
          </>
        )}
      </div>
    </div>
  )
}

function Field({ label, ...props }) { return <label className="login-field"><span className="login-field__label">{label}</span><input className="login-field__input registration-input" required {...props} /></label> }
function PasswordField({ label, name, visible, toggle, ...props }) { return <label className="login-field"><span className="login-field__label">{label}</span><div className="login-field__wrap"><input className="login-field__input registration-input" type={visible ? 'text' : 'password'} autoComplete={name === 'confirmPassword' ? 'new-password' : 'new-password'} minLength="8" required name={name} {...props} />{toggle && <button type="button" className="login-field__toggle" onClick={toggle}>{visible ? <EyeOff size={15} /> : <Eye size={15} />}</button>}</div></label> }
