import { useState, useEffect, useRef } from 'react'
import {
  Plus,
  X,
  Paperclip,
  Save,
  User,
  Activity,
  FileText,
  Stethoscope,
  Pill,
  Calendar,
  UploadCloud,
  FileSpreadsheet,
  Image as ImageIcon,
  CheckCircle2,
  Trash2,
  Search,
  FlaskConical,
  ChevronDown,
  Info,
} from 'lucide-react'
import Modal from '../../../components/Modal'
import Button from '../../../components/Button'

const RECORD_TYPES = ['Consultation', 'LabReport', 'DischargeSummary', 'Prescription', 'GeneralNote']
const STATUSES = ['Finalized', 'Draft', 'Archived']

export default function MedicalRecordFormModal({
  open,
  isOpen,
  onClose,
  onSubmit,
  initialData = null,
  patients = [],
  doctors = [],
  currentDoctorId = null,
  loading = false,
}) {
  const isModalOpen = open ?? isOpen ?? false
  const fileInputRef = useRef(null)
  const patientDropdownRef = useRef(null)
  const doctorDropdownRef = useRef(null)

  const [formData, setFormData] = useState({
    patientId: '',
    doctorId: '',
    appointmentId: '',
    recordDate: new Date().toISOString().split('T')[0],
    recordType: 'Consultation',
    diagnosis: '',
    symptoms: '',
    treatmentPlan: '',
    prescriptionNotes: '',
    labNotes: '',
    followUpDate: '',
    status: 'Finalized',
    attachments: [],
  })

  const [patientSearch, setPatientSearch] = useState('')
  const [isPatientDropdownOpen, setIsPatientDropdownOpen] = useState(false)
  const [doctorSearch, setDoctorSearch] = useState('')
  const [isDoctorDropdownOpen, setIsDoctorDropdownOpen] = useState(false)
  const [validationError, setValidationError] = useState(null)
  const [isDragging, setIsDragging] = useState(false)

  const todayStr = new Date().toISOString().split('T')[0]

  const getTomorrowStr = (baseDateStr) => {
    try {
      const d = baseDateStr ? new Date(baseDateStr) : new Date()
      if (isNaN(d.getTime())) return ''
      d.setDate(d.getDate() + 1)
      return d.toISOString().split('T')[0]
    } catch {
      return ''
    }
  }

  const minFollowUpDate = formData.recordDate && formData.recordDate >= todayStr
    ? getTomorrowStr(formData.recordDate)
    : getTomorrowStr(todayStr)


  useEffect(() => {
    if (initialData) {
      setFormData({
        patientId: initialData.patientId || '',
        doctorId: initialData.doctorId || '',
        appointmentId: initialData.appointmentId || '',
        recordDate: initialData.recordDate ? initialData.recordDate.split('T')[0] : '',
        recordType: initialData.recordType || 'Consultation',
        diagnosis: initialData.diagnosis || '',
        symptoms: initialData.symptoms || '',
        treatmentPlan: initialData.treatmentPlan || '',
        prescriptionNotes: initialData.prescriptionNotes || '',
        labNotes: initialData.labNotes || '',
        followUpDate: initialData.followUpDate ? initialData.followUpDate.split('T')[0] : '',
        status: initialData.status || 'Finalized',
        attachments: initialData.attachments ? [...initialData.attachments] : [],
      })
      setPatientSearch(initialData.patientName || '')
      const foundDoctor = doctors.find((d) => String(d.doctorId) === String(initialData.doctorId))
      setDoctorSearch(
        initialData.doctorName ||
        (foundDoctor ? (foundDoctor.doctorName || foundDoctor.fullName) : '')
      )
    } else {
      const defaultDocId = currentDoctorId || ''
      const foundDoctor = defaultDocId
        ? doctors.find((d) => String(d.doctorId) === String(defaultDocId))
        : null
      setFormData({
        patientId: '',
        doctorId: defaultDocId,
        appointmentId: '',
        recordDate: new Date().toISOString().split('T')[0],
        recordType: 'Consultation',
        diagnosis: '',
        symptoms: '',
        treatmentPlan: '',
        prescriptionNotes: '',
        labNotes: '',
        followUpDate: '',
        status: 'Finalized',
        attachments: [],
      })
      setPatientSearch('')
      setDoctorSearch(foundDoctor ? (foundDoctor.doctorName || foundDoctor.fullName) : '')
    }
    setValidationError(null)
    setIsPatientDropdownOpen(false)
    setIsDoctorDropdownOpen(false)
  }, [initialData, isModalOpen, patients, doctors, currentDoctorId])

  // Sync doctor search display if doctors load asynchronously
  useEffect(() => {
    if (formData.doctorId && !doctorSearch && doctors.length > 0) {
      const doc = doctors.find((d) => String(d.doctorId) === String(formData.doctorId))
      if (doc) {
        setDoctorSearch(doc.doctorName || doc.fullName || `Doctor #${doc.doctorId}`)
      }
    }
  }, [formData.doctorId, doctorSearch, doctors])

  // Close dropdowns when clicked outside
  useEffect(() => {
    function handleClickOutside(event) {
      if (patientDropdownRef.current && !patientDropdownRef.current.contains(event.target)) {
        setIsPatientDropdownOpen(false)
      }
      if (doctorDropdownRef.current && !doctorDropdownRef.current.contains(event.target)) {
        setIsDoctorDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const selectedPatient = patients.find(
    (p) => String(p.patientId) === String(formData.patientId)
  )

  const filteredPatients = patients.filter((p) => {
    if (!patientSearch.trim()) return true
    const q = patientSearch.toLowerCase()
    const name = `${p.fullName || ''} ${p.firstName || ''} ${p.lastName || ''}`.toLowerCase()
    const nic = (p.nic || '').toLowerCase()
    const phone = (p.phoneNumber || '').toLowerCase()
    const email = (p.email || '').toLowerCase()
    const id = String(p.patientId)
    return name.includes(q) || nic.includes(q) || phone.includes(q) || email.includes(q) || id.includes(q)
  })

  const handleSelectPatient = (patient) => {
    setFormData((prev) => ({ ...prev, patientId: patient.patientId }))
    setPatientSearch(patient.fullName || `${patient.firstName} ${patient.lastName}`)
    setIsPatientDropdownOpen(false)
    setValidationError(null)
  }

  const filteredDoctors = doctors.filter((d) => {
    if (!doctorSearch.trim()) return true
    const q = doctorSearch.toLowerCase()
    const name = (d.doctorName || d.fullName || '').toLowerCase()
    const specialty = (d.specialty || d.specialization || '').toLowerCase()
    const id = String(d.doctorId)
    return name.includes(q) || specialty.includes(q) || id.includes(q)
  })

  const handleSelectDoctor = (doctor) => {
    setFormData((prev) => ({ ...prev, doctorId: doctor.doctorId }))
    setDoctorSearch(doctor.doctorName || doctor.fullName || `Dr. #${doctor.doctorId}`)
    setIsDoctorDropdownOpen(false)
  }

  const handleClearDoctor = () => {
    setFormData((prev) => ({ ...prev, doctorId: '' }))
    setDoctorSearch('')
    setIsDoctorDropdownOpen(false)
  }

  const handleFileChange = async (e) => {
    const files = e.target.files
    if (!files || files.length === 0) return
    await processSelectedFiles(files)
  }

  const processSelectedFiles = async (fileList) => {
    const allowedExtensions = ['.pdf', '.png', '.jpg', '.jpeg', '.dicom', '.doc', '.docx']
    const MAX_FILE_SIZE = 25 * 1024 * 1024
    const MAX_ATTACHMENTS = 10

    if (formData.attachments.length + fileList.length > MAX_ATTACHMENTS) {
      setValidationError(`You can attach a maximum of ${MAX_ATTACHMENTS} documents per medical record.`)
      return
    }

    for (let i = 0; i < fileList.length; i++) {
      const file = fileList[i]
      const fileExt = '.' + (file.name.split('.').pop() || '').toLowerCase()
      if (!allowedExtensions.includes(fileExt)) {
        setValidationError(`"${file.name}" has an unsupported format. Allowed formats: PDF, PNG, JPG, JPEG, DICOM, DOC, DOCX.`)
        return
      }
      if (file.size > MAX_FILE_SIZE) {
        setValidationError(`"${file.name}" exceeds the 25 MB file size limit. Please select a smaller document.`)
        return
      }
    }

    const newItems = []
    for (let i = 0; i < fileList.length; i++) {
      const file = fileList[i]
      const fileType = file.type || (file.name.endsWith('.pdf') ? 'application/pdf' : 'application/octet-stream')

      const fileDataUrl = await new Promise((resolve) => {
        const reader = new FileReader()
        reader.onload = () => resolve(reader.result)
        reader.onerror = () => resolve(`/uploads/medical-records/${file.name}`)
        reader.readAsDataURL(file)
      })

      // Cache file preview in sessionStorage for immediate viewing in-session
      try {
        if (typeof window !== 'undefined' && fileDataUrl && fileDataUrl.length < 5 * 1024 * 1024) {
          sessionStorage.setItem(`med_preview_${file.name}`, fileDataUrl)
        }
      } catch {
        // Ignore quota limits
      }

      newItems.push({
        rawFile: file,
        fileName: file.name,
        fileType: fileType,
        fileUrl: fileDataUrl,
        fileSize: file.size || 1024 * 500,
      })
    }

    setFormData((prev) => ({
      ...prev,
      attachments: [...prev.attachments, ...newItems],
    }))
    setValidationError(null)
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }

  const handleDrop = (e) => {
    e.preventDefault()
    setIsDragging(false)
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      processSelectedFiles(e.dataTransfer.files)
    }
  }

  const handleRemoveAttachment = (index) => {
    setFormData((prev) => ({
      ...prev,
      attachments: prev.attachments.filter((_, i) => i !== index),
    }))
  }

  const isConsultation = formData.recordType === 'Consultation'
  const isLabReport = formData.recordType === 'LabReport'
  const isPrescription = formData.recordType === 'Prescription'
  const isDischargeSummary = formData.recordType === 'DischargeSummary'
  const isGeneralNote = formData.recordType === 'GeneralNote'

  const handleSubmit = async (e) => {
    e.preventDefault()
    setValidationError(null)

    if (!formData.patientId) {
      setValidationError('Please select a patient.')
      return
    }

    // 1. Record Date validations (default to creation date, cannot be in future)
    if (!formData.recordDate) {
      setValidationError('Record date is required.')
      return
    }
    if (formData.recordDate > todayStr) {
      setValidationError('Record date cannot be in the future. It should be the creation date or past consultation date.')
      return
    }
    if (new Date(formData.recordDate).getFullYear() < 1950) {
      setValidationError('Record date cannot be earlier than 1950.')
      return
    }

    // 2. Follow-Up Date validations (must be strictly in the future and after record date)
    if (formData.followUpDate) {
      if (formData.followUpDate <= todayStr) {
        setValidationError('Follow-up date must be a future date (after today).')
        return
      }
      if (formData.recordDate && formData.followUpDate <= formData.recordDate) {
        setValidationError('Follow-up date must be strictly after the record date.')
        return
      }
      const followUpYear = new Date(formData.followUpDate).getFullYear()
      const currentYear = new Date().getFullYear()
      if (followUpYear > currentYear + 5) {
        setValidationError('Follow-up date cannot be more than 5 years in the future.')
        return
      }
    }

    // 3. Diagnosis / Topic validation (meaningful text, at least 3 chars)
    const cleanDiagnosis = formData.diagnosis.trim()
    if (!cleanDiagnosis) {
      if (isLabReport) setValidationError('Laboratory investigation / test name is required.')
      else if (isPrescription) setValidationError('Medical condition / clinical indication is required.')
      else if (isDischargeSummary) setValidationError('Final discharge diagnosis is required.')
      else if (isGeneralNote) setValidationError('Note subject / heading is required.')
      else setValidationError('Primary diagnosis is required.')
      return
    }
    if (cleanDiagnosis.length < 3) {
      setValidationError('Diagnosis / clinical title must be at least 3 characters long.')
      return
    }
    if (/^[0-9\W_]+$/.test(cleanDiagnosis)) {
      setValidationError('Diagnosis / title must contain meaningful clinical text, not only numbers or symbols.')
      return
    }

    // 4. Treatment Plan / Recommendations validation
    const cleanTreatmentPlan = formData.treatmentPlan.trim()
    if (!cleanTreatmentPlan) {
      if (isLabReport) setValidationError('Diagnostic impression & clinical interpretation is required.')
      else if (isPrescription) setValidationError('Patient administration instructions & directions are required.')
      else if (isDischargeSummary) setValidationError('Post-discharge instructions & care plan is required.')
      else if (isGeneralNote) setValidationError('Recommendations and plan are required.')
      else setValidationError('Treatment plan is required.')
      return
    }
    if (cleanTreatmentPlan.length < 3) {
      setValidationError('Treatment plan / recommendations must be at least 3 characters long.')
      return
    }

    // 5. Dynamic field validation per record type
    if (isLabReport) {
      const cleanLabNotes = formData.labNotes.trim()
      if (!cleanLabNotes) {
        setValidationError('Lab observations & test measurements are required.')
        return
      }
      if (cleanLabNotes.length < 3) {
        setValidationError('Lab observations must be at least 3 characters long.')
        return
      }
    } else if (isPrescription) {
      const cleanPrescriptionNotes = formData.prescriptionNotes.trim()
      if (!cleanPrescriptionNotes) {
        setValidationError('Prescription details & medication dosages are required.')
        return
      }
      if (cleanPrescriptionNotes.length < 3) {
        setValidationError('Prescription details must be at least 3 characters long.')
        return
      }
    } else {
      // Standard Consultation, DischargeSummary, GeneralNote
      const cleanSymptoms = formData.symptoms.trim()
      if (!cleanSymptoms) {
        if (isDischargeSummary) setValidationError('Admission reason & hospital stay summary is required.')
        else if (isGeneralNote) setValidationError('Clinical progress observations are required.')
        else setValidationError('Symptoms & clinical findings are required.')
        return
      }
      if (cleanSymptoms.length < 3) {
        setValidationError('Symptoms & observations must be at least 3 characters long.')
        return
      }
    }

    // Provide safe defaults for backend database non-null columns
    let finalSymptoms = formData.symptoms.trim()
    if (!finalSymptoms) {
      if (isLabReport) finalSymptoms = formData.labNotes.trim() || 'Laboratory diagnostic test findings'
      else if (isPrescription) finalSymptoms = `Prescription issued for ${cleanDiagnosis}`
      else finalSymptoms = 'Clinical documentation'
    }

    let finalTreatmentPlan = cleanTreatmentPlan
    if (!finalTreatmentPlan) {
      if (isPrescription) finalTreatmentPlan = formData.prescriptionNotes.trim() || 'Follow prescribed regimen'
      else finalTreatmentPlan = 'Follow clinical advice'
    }

    const payload = {
      ...formData,
      patientId: parseInt(formData.patientId, 10),
      doctorId: formData.doctorId ? parseInt(formData.doctorId, 10) : null,
      appointmentId: formData.appointmentId ? parseInt(formData.appointmentId, 10) : null,
      followUpDate: formData.followUpDate ? new Date(formData.followUpDate).toISOString() : null,
      recordDate: formData.recordDate
        ? (formData.recordDate.includes('T') ? formData.recordDate.split('T')[0] + 'T00:00:00.000Z' : `${formData.recordDate}T00:00:00.000Z`)
        : `${todayStr}T00:00:00.000Z`,
      diagnosis: cleanDiagnosis,
      symptoms: finalSymptoms,
      treatmentPlan: finalTreatmentPlan,
      prescriptionNotes: formData.prescriptionNotes?.trim() || null,
      labNotes: formData.labNotes?.trim() || null,
      attachments: formData.attachments.map((a) => {
        const isDataOrBlob =
          !a.fileUrl ||
          a.fileUrl.startsWith('data:') ||
          a.fileUrl.startsWith('blob:') ||
          a.fileUrl.length > 1000
        return {
          rawFile: a.rawFile,
          attachmentId: a.attachmentId,
          fileName: (a.fileName || 'attachment.pdf').slice(0, 250),
          fileType: (a.fileType || 'application/pdf').slice(0, 100),
          fileUrl: isDataOrBlob
            ? `/uploads/medical-records/${encodeURIComponent(a.fileName || 'attachment.pdf')}`
            : a.fileUrl,
          fileSize: Math.max(1, Math.min(a.fileSize || 1024 * 50, 50 * 1024 * 1024)),
        }
      }),
    }
    await onSubmit(payload)
  }

  const isEdit = Boolean(initialData)

  const formatFileSize = (bytes) => {
    if (!bytes) return '0 KB'
    const kb = bytes / 1024
    if (kb < 1024) return `${kb.toFixed(1)} KB`
    return `${(kb / 1024).toFixed(1)} MB`
  }

  return (
    <Modal
      open={isModalOpen}
      onClose={onClose}
      title={isEdit ? 'Edit Medical Record' : 'Create New Medical Record'}
      subtitle={
        isEdit
          ? `Updating Record #${initialData?.medicalRecordId} for ${initialData?.patientName}`
          : 'Record patient consultation, diagnostic report, prescription, or clinical note'
      }
      size="lg"
      id="medical-record-form-modal"
      closeOnOverlayClick={false}
      closeOnEscape={false}
    >
      <form onSubmit={handleSubmit} className="patient-form mr-form-styled" id="medical-record-form">
        {validationError && (
          <div className="patient-form__error" role="alert">
            {validationError}
          </div>
        )}

        {/* Section 1: Overview & Patient Details */}
        <div className="form-section">
          <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <User size={15} /> Patient & Record Information
          </h5>
          <div className="form-grid form-grid--2">
            {/* Searchable Patient Selector */}
            <div className="field" ref={patientDropdownRef} style={{ position: 'relative' }}>
              <label className="field__label">
                Patient <span className="field__required">*</span>
              </label>
              {isEdit ? (
                <input
                  type="text"
                  className="field__input"
                  value={initialData.patientName || `Patient #${initialData.patientId}`}
                  disabled
                />
              ) : (
                <div style={{ position: 'relative' }}>
                  <div style={{ display: 'flex', alignItems: 'center', position: 'relative' }}>
                    <Search
                      size={16}
                      style={{
                        position: 'absolute',
                        left: '12px',
                        color: 'var(--text-muted)',
                        pointerEvents: 'none',
                      }}
                    />
                    <input
                      type="text"
                      className="field__input"
                      style={{ paddingLeft: '36px', paddingRight: '32px' }}
                      placeholder="Search patient by name, NIC, phone, ID..."
                      value={patientSearch}
                      onChange={(e) => {
                        setPatientSearch(e.target.value)
                        setIsPatientDropdownOpen(true)
                      }}
                      onFocus={() => setIsPatientDropdownOpen(true)}
                      required={!formData.patientId}
                    />
                    <ChevronDown
                      size={16}
                      style={{
                        position: 'absolute',
                        right: '12px',
                        color: 'var(--text-muted)',
                        pointerEvents: 'none',
                        transform: isPatientDropdownOpen ? 'rotate(180deg)' : 'none',
                        transition: 'transform 0.2s ease',
                      }}
                    />
                  </div>

                  {/* Dropdown list */}
                  {isPatientDropdownOpen && (
                    <div
                      style={{
                        position: 'absolute',
                        top: '100%',
                        left: 0,
                        right: 0,
                        marginTop: '4px',
                        maxHeight: '220px',
                        overflowY: 'auto',
                        background: 'var(--bg-surface)',
                        border: '1px solid var(--border-default)',
                        borderRadius: '10px',
                        boxShadow: '0 10px 25px rgba(0,0,0,0.2)',
                        zIndex: 50,
                      }}
                    >
                      {filteredPatients.length > 0 ? (
                        filteredPatients.map((p) => {
                          const isSelected = String(p.patientId) === String(formData.patientId)
                          const pName = p.fullName || `${p.firstName} ${p.lastName}`
                          return (
                            <div
                              key={p.patientId}
                              data-testid={`patient-option-${p.patientId}`}
                              onClick={() => handleSelectPatient(p)}
                              style={{
                                padding: '10px 14px',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                background: isSelected ? 'rgba(14, 165, 233, 0.1)' : 'transparent',
                                borderBottom: '1px solid var(--border-default)',
                                transition: 'background 0.15s ease',
                              }}
                              onMouseEnter={(e) => {
                                if (!isSelected) e.currentTarget.style.background = 'var(--bg-base)'
                              }}
                              onMouseLeave={(e) => {
                                if (!isSelected) e.currentTarget.style.background = 'transparent'
                              }}
                            >
                              <div>
                                <div style={{ fontWeight: 600, fontSize: '0.88rem', color: 'var(--text-primary)' }}>
                                  {pName}
                                </div>
                                <div style={{ fontSize: '0.76rem', color: 'var(--text-muted)', marginTop: '2px' }}>
                                  NIC: {p.nic || 'N/A'} • Phone: {p.phoneNumber || 'N/A'} • ID: #{p.patientId}
                                </div>
                              </div>
                              {isSelected && <CheckCircle2 size={16} color="var(--clr-primary)" />}
                            </div>
                          )
                        })
                      ) : (
                        <div style={{ padding: '16px', textAlign: 'center', fontSize: '0.84rem', color: 'var(--text-muted)' }}>
                          No patients found matching "{patientSearch}"
                        </div>
                      )}
                    </div>
                  )}

                  {/* Hidden accessibility select */}
                  <select
                    style={{ display: 'none' }}
                    value={formData.patientId}
                    onChange={(e) => setFormData({ ...formData, patientId: e.target.value })}
                  >
                    <option value="">Select Patient</option>
                    {patients.map((p) => (
                      <option key={p.patientId} value={p.patientId}>
                        {p.fullName || `${p.firstName} ${p.lastName}`}
                      </option>
                    ))}
                  </select>
                </div>
              )}
            </div>

            {/* A doctor's identity is derived from their signed-in account. Only admins may assign a different clinician. */}
            {currentDoctorId ? (
              <div className="field">
                <label className="field__label">Authoring Doctor</label>
                <div
                  className="field__input"
                  style={{ display: 'flex', alignItems: 'center', gap: '8px', color: 'var(--text-muted)', cursor: 'not-allowed' }}
                  aria-label="Authoring doctor is assigned from the signed-in account"
                >
                  <Stethoscope size={16} color="var(--clr-primary)" />
                  {doctorSearch || 'Your signed-in doctor profile'}
                </div>
              </div>
            ) : (
            <div className="field" ref={doctorDropdownRef} style={{ position: 'relative' }}>
              <label className="field__label" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span>
                  Doctor / Clinician <span style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>(Optional)</span>
                </span>
                {formData.doctorId && (
                  <button
                    type="button"
                    onClick={handleClearDoctor}
                    style={{
                      border: 'none',
                      background: 'transparent',
                      color: 'var(--clr-primary)',
                      fontSize: '0.74rem',
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '2px',
                    }}
                    title="Clear assigned doctor"
                  >
                    <X size={12} /> Clear
                  </button>
                )}
              </label>
              <div style={{ position: 'relative' }}>
                <div style={{ display: 'flex', alignItems: 'center', position: 'relative' }}>
                  <Stethoscope
                    size={16}
                    style={{
                      position: 'absolute',
                      left: '12px',
                      color: 'var(--clr-primary)',
                      pointerEvents: 'none',
                    }}
                  />
                  <input
                    type="text"
                    className="field__input"
                    style={{ paddingLeft: '36px', paddingRight: '32px' }}
                    placeholder="Search doctor by name or specialty..."
                    value={doctorSearch}
                    onChange={(e) => {
                      setDoctorSearch(e.target.value)
                      setIsDoctorDropdownOpen(true)
                      if (!e.target.value.trim()) {
                        setFormData((prev) => ({ ...prev, doctorId: '' }))
                      }
                    }}
                    onFocus={() => setIsDoctorDropdownOpen(true)}
                  />
                  <ChevronDown
                    size={16}
                    style={{
                      position: 'absolute',
                      right: '12px',
                      color: 'var(--text-muted)',
                      pointerEvents: 'none',
                      transform: isDoctorDropdownOpen ? 'rotate(180deg)' : 'none',
                      transition: 'transform 0.2s ease',
                    }}
                  />
                </div>

                {/* Dropdown list */}
                {isDoctorDropdownOpen && (
                  <div
                    style={{
                      position: 'absolute',
                      top: '100%',
                      left: 0,
                      right: 0,
                      marginTop: '4px',
                      maxHeight: '220px',
                      overflowY: 'auto',
                      background: 'var(--bg-surface)',
                      border: '1px solid var(--border-default)',
                      borderRadius: '10px',
                      boxShadow: '0 10px 25px rgba(0,0,0,0.2)',
                      zIndex: 50,
                    }}
                  >
                    {/* Unassigned / General Hospital Record option */}
                    <div
                      data-testid="doctor-option-unassigned"
                      onClick={() => {
                        setFormData((prev) => ({ ...prev, doctorId: '' }))
                        setDoctorSearch('')
                        setIsDoctorDropdownOpen(false)
                      }}
                      style={{
                        padding: '10px 14px',
                        cursor: 'pointer',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        background: !formData.doctorId ? 'rgba(14, 165, 233, 0.08)' : 'transparent',
                        borderBottom: '1px solid var(--border-default)',
                      }}
                      onMouseEnter={(e) => {
                        if (formData.doctorId) e.currentTarget.style.background = 'var(--bg-base)'
                      }}
                      onMouseLeave={(e) => {
                        if (formData.doctorId) e.currentTarget.style.background = 'transparent'
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: 600, fontSize: '0.85rem', color: 'var(--text-muted)' }}>
                          Unassigned / General Hospital Record
                        </div>
                        <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>
                          Visible to hospital administrators and patient
                        </div>
                      </div>
                      {!formData.doctorId && <CheckCircle2 size={16} color="var(--clr-primary)" />}
                    </div>

                    {filteredDoctors.length > 0 ? (
                      filteredDoctors.map((d) => {
                        const isSelected = String(d.doctorId) === String(formData.doctorId)
                        const dName = d.doctorName || d.fullName || `Doctor #${d.doctorId}`
                        const dSpec = d.specialty || d.specialization || 'Specialist'
                        return (
                          <div
                            key={d.doctorId}
                            data-testid={`doctor-option-${d.doctorId}`}
                            onClick={() => handleSelectDoctor(d)}
                            style={{
                              padding: '10px 14px',
                              cursor: 'pointer',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'space-between',
                              background: isSelected ? 'rgba(14, 165, 233, 0.1)' : 'transparent',
                              borderBottom: '1px solid var(--border-default)',
                              transition: 'background 0.15s ease',
                            }}
                            onMouseEnter={(e) => {
                              if (!isSelected) e.currentTarget.style.background = 'var(--bg-base)'
                            }}
                            onMouseLeave={(e) => {
                              if (!isSelected) e.currentTarget.style.background = 'transparent'
                            }}
                          >
                            <div>
                              <div style={{ fontWeight: 600, fontSize: '0.88rem', color: 'var(--text-primary)' }}>
                                {dName}
                              </div>
                              <div style={{ fontSize: '0.76rem', color: 'var(--text-muted)', marginTop: '2px' }}>
                                Specialty: {dSpec} • ID: #{d.doctorId}
                              </div>
                            </div>
                            {isSelected && <CheckCircle2 size={16} color="var(--clr-primary)" />}
                          </div>
                        )
                      })
                    ) : (
                      <div style={{ padding: '16px', textAlign: 'center', fontSize: '0.84rem', color: 'var(--text-muted)' }}>
                        No doctors found matching "{doctorSearch}"
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>
            )}

            <div className="field">
              <label className="field__label">
                Record Type <span className="field__required">*</span>
              </label>
              <select
                className="field__input field__select"
                value={formData.recordType}
                onChange={(e) => setFormData({ ...formData, recordType: e.target.value })}
                required
              >
                {RECORD_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {type === 'Consultation' && 'Consultation (Full Visit)'}
                    {type === 'LabReport' && 'Lab Report / Test Result'}
                    {type === 'Prescription' && 'Prescription Order'}
                    {type === 'DischargeSummary' && 'Discharge Summary'}
                    {type === 'GeneralNote' && 'General Progress Note'}
                  </option>
                ))}
              </select>
            </div>

            <div className="field">
              <label className="field__label">
                Record Date <span className="field__required">*</span>
              </label>
              <input
                type="date"
                className="field__input"
                max={todayStr}
                value={formData.recordDate}
                onChange={(e) => setFormData({ ...formData, recordDate: e.target.value })}
                required
              />
              <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                Defaults to today's creation date. Cannot be in the future.
              </div>
            </div>

            <div className="field form-grid__span-2">
              <label className="field__label">
                Workflow Status <span className="field__required">*</span>
              </label>
              <select
                className="field__input field__select"
                value={formData.status}
                onChange={(e) => setFormData({ ...formData, status: e.target.value })}
                required
              >
                {STATUSES.map((st) => (
                  <option key={st} value={st}>
                    {st}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* Dynamic Section: Lab Report Focus */}
        {isLabReport && (
          <div className="form-section">
            <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <FlaskConical size={15} /> Laboratory Investigation & Metrics
            </h5>
            <div className="form-grid">
              <div className="field">
                <label className="field__label">
                  Investigation / Test Name <span className="field__required">*</span>
                </label>
                <input
                  type="text"
                  className="field__input"
                  placeholder="e.g. Full Blood Count (FBC), Fasting Blood Sugar, Lipid Profile, Chest X-Ray..."
                  value={formData.diagnosis}
                  onChange={(e) => setFormData({ ...formData, diagnosis: e.target.value })}
                  maxLength={500}
                  required
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Lab Observations, Quantitative Values & Measurements <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="e.g. Hemoglobin: 14.2 g/dL, WBC: 7.2 x10^3/uL, Platelets: 240,000/uL, Fasting Glucose: 98 mg/dL..."
                  rows={4}
                  value={formData.labNotes}
                  onChange={(e) => setFormData({ ...formData, labNotes: e.target.value })}
                  maxLength={4000}
                  required
                  style={{ fontFamily: 'var(--font-mono)', fontSize: '0.84rem', resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Diagnostic Impression & Clinical Recommendations <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="e.g. Hematological markers within normal biological reference intervals. No intervention required."
                  rows={3}
                  value={formData.treatmentPlan}
                  onChange={(e) => setFormData({ ...formData, treatmentPlan: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">Recommended Repeat Test Date (Optional)</label>
                <input
                  type="date"
                  className="field__input"
                  min={minFollowUpDate}
                  value={formData.followUpDate}
                  onChange={(e) => setFormData({ ...formData, followUpDate: e.target.value })}
                />
                <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                  Must be a future date after the record date.
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Dynamic Section: Prescription Focus */}
        {isPrescription && (
          <div className="form-section">
            <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Pill size={15} /> Prescription Details & Regimen
            </h5>
            <div className="form-grid">
              <div className="field">
                <label className="field__label">
                  Medical Indication / Condition <span className="field__required">*</span>
                </label>
                <input
                  type="text"
                  className="field__input"
                  placeholder="e.g. Acute Bacterial Sinusitis, Essential Hypertension, Pain Relief..."
                  value={formData.diagnosis}
                  onChange={(e) => setFormData({ ...formData, diagnosis: e.target.value })}
                  maxLength={500}
                  required
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Prescribed Medications, Dosages & Frequency <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="e.g. Amoxicillin/Clavulanate 625mg PO BD x 7 days&#10;Paracetamol 500mg PO TDS PRN for pain x 3 days..."
                  rows={4}
                  value={formData.prescriptionNotes}
                  onChange={(e) => setFormData({ ...formData, prescriptionNotes: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ fontFamily: 'var(--font-mono)', fontSize: '0.84rem', resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Administration Directions & Patient Guidance <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="e.g. Take immediately after meals with plenty of water. Complete the entire course. Avoid alcohol."
                  rows={3}
                  value={formData.treatmentPlan}
                  onChange={(e) => setFormData({ ...formData, treatmentPlan: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">Next Medication Review Date (Optional)</label>
                <input
                  type="date"
                  className="field__input"
                  min={minFollowUpDate}
                  value={formData.followUpDate}
                  onChange={(e) => setFormData({ ...formData, followUpDate: e.target.value })}
                />
                <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                  Must be a future date after the record date.
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Dynamic Section: Discharge Summary Focus */}
        {isDischargeSummary && (
          <div className="form-section">
            <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <FileText size={15} /> Inpatient Discharge Summary
            </h5>
            <div className="form-grid">
              <div className="field">
                <label className="field__label">
                  Final Discharge Diagnosis <span className="field__required">*</span>
                </label>
                <input
                  type="text"
                  className="field__input"
                  placeholder="e.g. Acute Appendicitis - Status Post Laparoscopic Appendectomy"
                  value={formData.diagnosis}
                  onChange={(e) => setFormData({ ...formData, diagnosis: e.target.value })}
                  maxLength={500}
                  required
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Admission Reason & Hospital Stay Summary <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="Summarize initial clinical presentation, hospital course, interventions performed, and clinical recovery..."
                  rows={3}
                  value={formData.symptoms}
                  onChange={(e) => setFormData({ ...formData, symptoms: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Post-Discharge Care & Activity Instructions <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="e.g. Suture line care, wound check in 7 days. Avoid heavy lifting and vigorous exercise for 2 weeks."
                  rows={3}
                  value={formData.treatmentPlan}
                  onChange={(e) => setFormData({ ...formData, treatmentPlan: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">Discharge Medications & Regimen (Optional)</label>
                <textarea
                  className="field__input"
                  placeholder="e.g. Cefuroxime 500mg BD x 5 days, Paracetamol 1g TDS PRN"
                  rows={2}
                  value={formData.prescriptionNotes}
                  onChange={(e) => setFormData({ ...formData, prescriptionNotes: e.target.value })}
                  maxLength={2000}
                  style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">Outpatient Follow-up Clinic Date (Optional)</label>
                <input
                  type="date"
                  className="field__input"
                  min={minFollowUpDate}
                  value={formData.followUpDate}
                  onChange={(e) => setFormData({ ...formData, followUpDate: e.target.value })}
                />
                <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                  Must be a future date after the record date.
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Dynamic Section: General Note Focus */}
        {isGeneralNote && (
          <div className="form-section">
            <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Activity size={15} /> Clinical Progress Note
            </h5>
            <div className="form-grid">
              <div className="field">
                <label className="field__label">
                  Note Subject / Heading <span className="field__required">*</span>
                </label>
                <input
                  type="text"
                  className="field__input"
                  placeholder="e.g. Routine Progress Review, Vitals Follow-up Note, Dietician Advice..."
                  value={formData.diagnosis}
                  onChange={(e) => setFormData({ ...formData, diagnosis: e.target.value })}
                  maxLength={500}
                  required
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Clinical Observations & Observations <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="Record current observations, patient condition, and clinical progress..."
                  rows={3}
                  value={formData.symptoms}
                  onChange={(e) => setFormData({ ...formData, symptoms: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">
                  Recommendations & Next Steps <span className="field__required">*</span>
                </label>
                <textarea
                  className="field__input"
                  placeholder="Enter clinical recommendations, follow-up plan, or lifestyle advice..."
                  rows={3}
                  value={formData.treatmentPlan}
                  onChange={(e) => setFormData({ ...formData, treatmentPlan: e.target.value })}
                  maxLength={2000}
                  required
                  style={{ resize: 'vertical' }}
                />
              </div>

              <div className="field">
                <label className="field__label">Next Clinical Review Date (Optional)</label>
                <input
                  type="date"
                  className="field__input"
                  min={minFollowUpDate}
                  value={formData.followUpDate}
                  onChange={(e) => setFormData({ ...formData, followUpDate: e.target.value })}
                />
                <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                  Must be a future date after the record date.
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Dynamic Section: Standard Consultation Focus */}
        {isConsultation && (
          <>
            <div className="form-section">
              <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Activity size={15} /> Clinical Evaluation & Diagnosis
              </h5>
              <div className="form-grid">
                <div className="field">
                  <label className="field__label">
                    Primary Diagnosis <span className="field__required">*</span>
                  </label>
                  <input
                    type="text"
                    className="field__input"
                    placeholder="e.g. Type 2 Diabetes, Acute Bronchitis"
                    value={formData.diagnosis}
                    onChange={(e) => setFormData({ ...formData, diagnosis: e.target.value })}
                    maxLength={500}
                    required
                  />
                </div>

                <div className="field">
                  <label className="field__label">
                    Symptoms & Clinical Presentation <span className="field__required">*</span>
                  </label>
                  <textarea
                    className="field__input"
                    placeholder="Enter patient symptoms and complaints..."
                    rows={3}
                    value={formData.symptoms}
                    onChange={(e) => setFormData({ ...formData, symptoms: e.target.value })}
                    maxLength={2000}
                    required
                    style={{ resize: 'vertical' }}
                  />
                </div>

                <div className="field">
                  <label className="field__label">
                    Treatment Plan & Medical Advice <span className="field__required">*</span>
                  </label>
                  <textarea
                    className="field__input"
                    placeholder="Enter treatment plan and doctor advice..."
                    rows={3}
                    value={formData.treatmentPlan}
                    onChange={(e) => setFormData({ ...formData, treatmentPlan: e.target.value })}
                    maxLength={2000}
                    required
                    style={{ resize: 'vertical' }}
                  />
                </div>
              </div>
            </div>

            <div className="form-section">
              <h5 className="form-section__title" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Pill size={15} /> Prescriptions & Diagnostics (Optional)
              </h5>
              <div className="form-grid form-grid--2">
                <div className="field">
                  <label className="field__label">Prescription Notes & Dosage</label>
                  <textarea
                    className="field__input"
                    placeholder="e.g. Paracetamol 500mg TDS x 3 days"
                    rows={3}
                    value={formData.prescriptionNotes}
                    onChange={(e) => setFormData({ ...formData, prescriptionNotes: e.target.value })}
                    maxLength={2000}
                    style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', resize: 'vertical' }}
                  />
                </div>

                <div className="field">
                  <label className="field__label">Lab Observations & Test Findings</label>
                  <textarea
                    className="field__input"
                    placeholder="e.g. Blood Sugar: 110 mg/dL, Normal ECG"
                    rows={3}
                    value={formData.labNotes}
                    onChange={(e) => setFormData({ ...formData, labNotes: e.target.value })}
                    maxLength={4000}
                    style={{ resize: 'vertical' }}
                  />
                </div>

                <div className="field form-grid__span-2">
                  <label className="field__label">Follow-up Date (Optional)</label>
                  <input
                    type="date"
                    className="field__input"
                    min={minFollowUpDate}
                    value={formData.followUpDate}
                    onChange={(e) => setFormData({ ...formData, followUpDate: e.target.value })}
                  />
                  <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)', marginTop: '4px' }}>
                    Must be a future date after the record date.
                  </div>
                </div>
              </div>
            </div>
          </>
        )}

        {/* Section: Attachments (File Browser / Local Drag & Drop) */}
        <div className="form-section">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <h5 className="form-section__title" style={{ margin: 0, border: 'none', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <Paperclip size={15} /> {isLabReport ? 'Diagnostic Scans & Lab Documents' : 'Diagnostic Attachments'} ({formData.attachments.length})
            </h5>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
            >
              <Plus size={14} /> Browse Files
            </Button>
          </div>

          {/* Hidden native file input */}
          <input
            type="file"
            ref={fileInputRef}
            style={{ display: 'none' }}
            multiple
            accept=".pdf,.png,.jpg,.jpeg,.dicom,.doc,.docx"
            onChange={handleFileChange}
          />

          {/* Drag & Drop Dropzone */}
          <div
            onDragOver={(e) => {
              e.preventDefault()
              setIsDragging(true)
            }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
            style={{
              border: `2px dashed ${isDragging ? 'var(--clr-primary)' : 'var(--border-default)'}`,
              background: isDragging ? 'rgba(14, 165, 233, 0.08)' : 'var(--bg-base)',
              borderRadius: '12px',
              padding: '24px 16px',
              textAlign: 'center',
              cursor: 'pointer',
              transition: 'all 0.2s ease',
            }}
          >
            <UploadCloud size={28} color="var(--clr-primary)" style={{ margin: '0 auto 8px' }} />
            <div style={{ fontWeight: 600, fontSize: '0.9rem', color: 'var(--text-primary)' }}>
              Click to browse local files or drag & drop here
            </div>
            <div style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginTop: '4px' }}>
              Supports PDF reports, JPEG/PNG scans, DICOM, and Clinical Docs
            </div>
          </div>

          {/* List of Selected Local Files */}
          {formData.attachments.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '12px' }}>
              {formData.attachments.map((att, i) => (
                <div
                  key={i}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '10px 14px',
                    background: 'var(--bg-surface)',
                    borderRadius: '8px',
                    border: '1px solid var(--border-default)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    {att.fileType.includes('image') ? (
                      <ImageIcon size={18} color="var(--clr-accent)" />
                    ) : (
                      <FileSpreadsheet size={18} color="var(--clr-primary)" />
                    )}
                    <div>
                      <div style={{ fontWeight: 600, fontSize: '0.88rem', color: 'var(--text-primary)' }}>
                        {att.fileName}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        {formatFileSize(att.fileSize)} • {att.fileType}
                      </div>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation()
                      handleRemoveAttachment(i)
                    }}
                    style={{
                      background: 'transparent',
                      border: 'none',
                      color: 'var(--clr-danger)',
                      cursor: 'pointer',
                      padding: '4px',
                      borderRadius: '4px',
                    }}
                    title="Remove file"
                  >
                    <Trash2 size={16} />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Form Action Buttons */}
        <div className="form-actions">
          <Button variant="secondary" type="button" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button variant="primary" type="submit" icon={Save} loading={loading}>
            {isEdit ? 'Save Changes' : 'Create Record'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
