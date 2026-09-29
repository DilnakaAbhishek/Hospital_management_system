import { useState, useEffect } from 'react'
import Modal from '../../../components/Modal'
import Button from '../../../components/Button'
import { Save } from 'lucide-react'
import './PatientForm.css'

const BLOOD_GROUPS = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-']
const GENDERS = ['Male', 'Female', 'Other']

const EMPTY = {
  firstName: '', lastName: '', dateOfBirth: '', gender: 'Male',
  nic: '', phoneNumber: '', email: '', address: '',
  bloodGroup: 'B+', emergencyContactName: '', emergencyContactPhone: '',
}

export default function PatientForm({ open, onClose, onSubmit, patient, loading, error }) {
  const [form, setForm] = useState(EMPTY)
  const [validationError, setValidationError] = useState(null)

  useEffect(() => {
    if (patient) {
      setForm({
        firstName: patient.firstName || '',
        lastName: patient.lastName || '',
        dateOfBirth: patient.dateOfBirth?.split('T')[0] || '',
        gender: patient.gender || 'Male',
        nic: patient.nic || '',
        phoneNumber: patient.phoneNumber || '',
        email: patient.email || '',
        address: patient.address || '',
        bloodGroup: patient.bloodGroup || 'B+',
        emergencyContactName: patient.emergencyContactName || '',
        emergencyContactPhone: patient.emergencyContactPhone || '',
      })
    } else {
      setForm(EMPTY)
    }
  }, [patient, open])

  const handleChange = (e) => {
    const { name, value } = e.target
    setForm(prev => ({ ...prev, [name]: value }))
    setValidationError(null)
  }

  const handleSubmit = (e) => {
    e.preventDefault()
    const message = validatePatientForm(form)
    if (message) {
      setValidationError(message)
      return
    }
    onSubmit(form)
  }

  const isEdit = Boolean(patient)

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={isEdit ? 'Edit Patient' : 'Add New Patient'}
      subtitle={isEdit ? `Editing record for ${patient?.firstName} ${patient?.lastName}` : 'Fill in the details to register a new patient'}
      size="lg"
      id="patient-form-modal"
    >
      <form onSubmit={handleSubmit} className="patient-form" id="patient-form">
        {(validationError || error) && (
          <div className="patient-form__error" role="alert">
            {validationError || error}
          </div>
        )}
        <div className="form-section">
          <h5 className="form-section__title">Personal Information</h5>
          <div className="form-grid form-grid--2">
            <Field label="First Name" name="firstName" value={form.firstName} onChange={handleChange} required />
            <Field label="Last Name" name="lastName" value={form.lastName} onChange={handleChange} required />
            <Field label="Date of Birth" name="dateOfBirth" type="date" value={form.dateOfBirth} onChange={handleChange} required />
            <SelectField label="Gender" name="gender" value={form.gender} onChange={handleChange} options={GENDERS} />
            <Field label="NIC Number" name="nic" value={form.nic} onChange={handleChange} placeholder="e.g. 980123456V" required />
            <SelectField label="Blood Group" name="bloodGroup" value={form.bloodGroup} onChange={handleChange} options={BLOOD_GROUPS} />
          </div>
        </div>

        <div className="form-section">
          <h5 className="form-section__title">Contact Information</h5>
          <div className="form-grid form-grid--2">
            <Field label="Phone Number" name="phoneNumber" value={form.phoneNumber} onChange={handleChange} placeholder="+94 77 123 4567" required />
            <Field label="Email Address" name="email" type="email" value={form.email} onChange={handleChange} placeholder="patient@email.com" required />
            <div className="form-grid__span-2">
              <Field label="Address" name="address" value={form.address} onChange={handleChange} placeholder="Street, City" required />
            </div>
          </div>
        </div>

        <div className="form-section">
          <h5 className="form-section__title">Emergency Contact</h5>
          <div className="form-grid form-grid--2">
            <Field label="Contact Name" name="emergencyContactName" value={form.emergencyContactName} onChange={handleChange} required />
            <Field label="Contact Phone" name="emergencyContactPhone" value={form.emergencyContactPhone} onChange={handleChange} placeholder="+94 71 123 4567" required />
          </div>
        </div>

        <div className="form-actions">
          <Button variant="secondary" type="button" onClick={onClose} id="patient-form-cancel">
            Cancel
          </Button>
          <Button variant="primary" type="submit" icon={Save} loading={loading} id="patient-form-submit">
            {isEdit ? 'Save Changes' : 'Register Patient'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}

function validatePatientForm(form) {
  const requiredFields = [
    ['firstName', 'First name'], ['lastName', 'Last name'], ['dateOfBirth', 'Date of birth'],
    ['nic', 'NIC number'], ['phoneNumber', 'Phone number'], ['email', 'Email address'],
    ['address', 'Address'], ['emergencyContactName', 'Emergency contact name'],
    ['emergencyContactPhone', 'Emergency contact phone'],
  ]

  const missing = requiredFields.find(([field]) => !form[field]?.trim())
  if (missing) return `${missing[1]} is required.`
  if (new Date(form.dateOfBirth) > new Date()) return 'Date of birth cannot be in the future.'
  if (!/^\S+@\S+\.\S+$/.test(form.email)) return 'Enter a valid email address.'
  if (form.nic.trim().length < 6) return 'Enter a valid NIC number.'
  if (form.phoneNumber.replace(/\D/g, '').length < 9) return 'Enter a valid phone number.'
  if (form.emergencyContactPhone.replace(/\D/g, '').length < 9) return 'Enter a valid emergency contact phone number.'
  return null
}

function Field({ label, name, value, onChange, type = 'text', placeholder, required }) {
  return (
    <div className="field">
      <label className="field__label" htmlFor={`field-${name}`}>
        {label} {required && <span className="field__required">*</span>}
      </label>
      <input
        id={`field-${name}`}
        className="field__input"
        type={type}
        name={name}
        value={value}
        onChange={onChange}
        placeholder={placeholder}
        required={required}
      />
    </div>
  )
}

function SelectField({ label, name, value, onChange, options }) {
  return (
    <div className="field">
      <label className="field__label" htmlFor={`field-${name}`}>{label}</label>
      <select
        id={`field-${name}`}
        className="field__input field__select"
        name={name}
        value={value}
        onChange={onChange}
      >
        {options.map(opt => (
          <option key={opt} value={opt}>{opt}</option>
        ))}
      </select>
    </div>
  )
}
