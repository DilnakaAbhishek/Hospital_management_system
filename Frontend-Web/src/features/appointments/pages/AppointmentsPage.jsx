import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertTriangle, CalendarClock, CheckCircle, ChevronDown, Clock, LayoutGrid, MapPin, Pencil, Plus, RefreshCw, Search, Stethoscope, Table2, X, XCircle } from 'lucide-react'
import Button from '../../../components/Button'
import Modal from '../../../components/Modal'
import Table from '../../../components/Table'
import { useDebounce } from '../../../hooks/useDebounce'
import { useAppointments } from '../hooks/useAppointments'
import { roomApi } from '../../rooms/services/roomApi'
import './AppointmentsPage.css'

function SearchableDoctorSelect({
  id,
  doctors,
  selectedSpecialty,
  selectedDoctorName,
  onSelectDoctor,
  disabled = false,
  placeholder = 'Type to search or select doctor...',
  required = false,
}) {
  const [isOpen, setIsOpen] = useState(false)
  const [searchQuery, setSearchQuery] = useState(selectedDoctorName || '')
  const [isTyping, setIsTyping] = useState(false)
  const containerRef = useRef(null)
  const inputRef = useRef(null)

  useEffect(() => {
    setSearchQuery(selectedDoctorName || '')
    setIsTyping(false)
  }, [selectedDoctorName])

  useEffect(() => {
    function handleClickOutside(event) {
      if (containerRef.current && !containerRef.current.contains(event.target)) {
        setIsOpen(false)
        setIsTyping(false)
        setSearchQuery(selectedDoctorName || '')
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
      return () => document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [isOpen, selectedDoctorName])

  const availableDoctors = useMemo(() => {
    return doctors.filter(doctor => {
      const matchesSpecialty = !selectedSpecialty || sameText(doctor.specialty, selectedSpecialty)
      if (!matchesSpecialty) return false

      if (!isTyping && searchQuery === selectedDoctorName) {
        return true
      }

      const q = searchQuery.toLowerCase().trim()
      if (!q) return true

      return (
        (doctor.doctorName && doctor.doctorName.toLowerCase().includes(q)) ||
        (doctor.specialty && doctor.specialty.toLowerCase().includes(q)) ||
        (doctor.doctorId && String(doctor.doctorId).includes(q))
      )
    })
  }, [doctors, selectedSpecialty, searchQuery, selectedDoctorName, isTyping])

  const handleInputChange = (e) => {
    const val = e.target.value
    setSearchQuery(val)
    setIsTyping(true)
    setIsOpen(true)
    const exactMatch = doctors.find(
      (d) => (d.doctorName || d.fullName || '').toLowerCase() === val.trim().toLowerCase()
    )
    if (exactMatch) {
      onSelectDoctor(exactMatch)
    } else if (!val.trim()) {
      onSelectDoctor(null)
    }
  }

  const handleSelect = (doctor) => {
    onSelectDoctor(doctor)
    setSearchQuery(doctor ? (doctor.doctorName || doctor.fullName || '') : '')
    setIsTyping(false)
    setIsOpen(false)
    if (inputRef.current) {
      inputRef.current.blur()
    }
  }

  const handleClear = (e) => {
    e.stopPropagation()
    onSelectDoctor(null)
    setSearchQuery('')
    setIsTyping(false)
    setIsOpen(true)
    if (inputRef.current) {
      inputRef.current.focus()
    }
  }

  return (
    <div className="searchable-doctor-select" ref={containerRef} style={{ position: 'relative' }}>
      <div style={{ position: 'relative', display: 'flex', alignItems: 'center' }}>
        <Search
          size={15}
          style={{
            position: 'absolute',
            left: '12px',
            color: 'var(--clr-primary)',
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />
        <input
          id={id}
          ref={inputRef}
          type="text"
          autoComplete="off"
          required={required && !selectedDoctorName}
          disabled={disabled}
          value={searchQuery}
          placeholder={placeholder}
          style={{
            width: '100%',
            paddingLeft: '34px',
            paddingRight: selectedDoctorName && !disabled ? '54px' : '32px',
          }}
          onChange={handleInputChange}
          onClick={() => {
            if (!disabled) setIsOpen(true)
          }}
          onFocus={() => {
            if (!disabled) setIsOpen(true)
          }}
        />
        <div style={{ position: 'absolute', right: '10px', display: 'flex', alignItems: 'center', gap: '4px' }}>
          {selectedDoctorName && !disabled && (
            <button
              type="button"
              onClick={handleClear}
              style={{
                border: 'none',
                background: 'transparent',
                color: 'var(--text-muted)',
                cursor: 'pointer',
                padding: '2px',
                display: 'flex',
                alignItems: 'center',
                borderRadius: '4px',
              }}
              title="Clear doctor"
            >
              <X size={14} />
            </button>
          )}
          <ChevronDown
            size={16}
            style={{
              color: 'var(--text-muted)',
              pointerEvents: 'none',
              transform: isOpen ? 'rotate(180deg)' : 'none',
              transition: 'transform 0.2s ease',
            }}
          />
        </div>
      </div>

      {isOpen && !disabled && (
        <div
          className="searchable-doctor-menu"
          style={{
            position: 'absolute',
            top: 'calc(100% + 4px)',
            left: 0,
            right: 0,
            maxHeight: '220px',
            overflowY: 'auto',
            background: 'var(--bg-surface, var(--bg-card))',
            border: '1px solid var(--border-default)',
            borderRadius: '10px',
            boxShadow: '0 10px 25px rgba(0,0,0,0.2)',
            zIndex: 100,
          }}
        >
          {availableDoctors.length === 0 ? (
            <div style={{ padding: '12px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '0.85rem' }}>
              No matching doctors found
            </div>
          ) : (
            availableDoctors.map((doc) => {
              const isSelected = selectedDoctorName && doc.doctorName.toLowerCase() === selectedDoctorName.toLowerCase()
              return (
                <div
                  key={doc.doctorId || doc.doctorName}
                  onMouseDown={(e) => {
                    e.preventDefault()
                    handleSelect(doc)
                  }}
                  onClick={() => handleSelect(doc)}
                  style={{
                    padding: '8px 12px',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    background: isSelected ? 'rgba(14, 165, 233, 0.12)' : 'transparent',
                    borderBottom: '1px solid var(--border-subtle, rgba(255,255,255,0.05))',
                    transition: 'background 0.15s ease',
                  }}
                  onMouseEnter={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'var(--bg-base, rgba(255,255,255,0.04))'
                  }}
                  onMouseLeave={(e) => {
                    if (!isSelected) e.currentTarget.style.background = 'transparent'
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <div
                      style={{
                        width: '28px',
                        height: '28px',
                        borderRadius: '50%',
                        background: isSelected ? 'var(--clr-primary)' : 'rgba(14, 165, 233, 0.15)',
                        color: isSelected ? '#fff' : 'var(--clr-primary)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: '0.75rem',
                        fontWeight: 'bold',
                        flexShrink: 0,
                      }}
                    >
                      {doc.doctorName ? doc.doctorName.replace('Dr. ', '').trim().charAt(0).toUpperCase() : 'D'}
                    </div>
                    <div>
                      <div style={{ fontWeight: isSelected ? 700 : 600, fontSize: '0.86rem', color: 'var(--text-primary)' }}>
                        {doc.doctorName}
                      </div>
                      <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>
                        {doc.specialty} {doc.doctorId ? `• ID: #${doc.doctorId}` : ''}
                      </div>
                    </div>
                  </div>
                  {isSelected && <CheckCircle size={15} color="var(--clr-primary)" />}
                </div>
              )
            })
          )}
        </div>
      )}
    </div>
  )
}

const STATUS_FILTERS = ['Confirmed', 'Completed', 'Cancelled']
const SLOT_STATUS_FILTERS = ['Upcoming', 'Completed', 'Cancelled']
const TYPES = ['Consultation', 'Follow-up', 'Check-up', 'Procedure Review', 'Emergency']
const DEFAULT_CONSULTATION_FEE = 2500
const SPECIALTY_CONSULTATION_FEES = {
  Cardiology: 4500,
  Dermatology: 3500,
  'General Medicine': 2500,
  Neurology: 5000,
  Ophthalmology: 3000,
  Orthopedics: 4000,
  Pediatrics: 3000,
}

const STATUS_CONFIG = {
  Upcoming: { icon: Clock, color: 'var(--clr-primary)', bg: 'rgba(14,165,233,0.12)' },
  Confirmed: { icon: CheckCircle, color: 'var(--clr-success)', bg: 'rgba(16,185,129,0.12)' },
  Completed: { icon: CheckCircle, color: 'var(--clr-primary)', bg: 'rgba(14,165,233,0.12)' },
  Cancelled: { icon: XCircle, color: 'var(--clr-danger)', bg: 'rgba(239,68,68,0.12)' },
}

const emptyAppointment = {
  specialty: '',
  doctorName: '',
  doctorTimeSlotId: '',
  appointmentDate: '',
  patientId: '',
  patientName: '',
  patientAge: '',
  patientPhone: '',
  patientEmail: '',
  appointmentType: 'Consultation',
}

const emptySlot = {
  doctorId: '',
  doctorName: '',
  specialty: '',
  date: '',
  startTime: '',
  endTime: '',
  capacity: 1,
  consultationFee: String(DEFAULT_CONSULTATION_FEE),
  roomId: '',
}

export default function AppointmentsPage() {
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('Confirmed')
  const [date, setDate] = useState('')
  const [appointmentOpen, setAppointmentOpen] = useState(false)
  const [editTarget, setEditTarget] = useState(null)
  const [slotOpen, setSlotOpen] = useState(false)
  const [slotView, setSlotView] = useState('table')
  const [slotStatus, setSlotStatus] = useState('Upcoming')
  const [slotSearch, setSlotSearch] = useState('')
  const [editSlotTarget, setEditSlotTarget] = useState(null)
  const [cancelSlotTarget, setCancelSlotTarget] = useState(null)
  const [slotCancelReason, setSlotCancelReason] = useState('')
  const [cancelTarget, setCancelTarget] = useState(null)
  const [cancelReason, setCancelReason] = useState('')
  const [appointmentForm, setAppointmentForm] = useState(emptyAppointment)
  const [slotForm, setSlotForm] = useState(emptySlot)
  const [availableRooms, setAvailableRooms] = useState([])
  const [checkingRooms, setCheckingRooms] = useState(false)
  const [roomError, setRoomError] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const debouncedSlotSearch = useDebounce(slotSearch, 300)

  const filters = useMemo(() => ({ search: debouncedSearch, status, date, sortBy: 'startAt', sortDirection: 'asc' }), [debouncedSearch, date, status])
  const {
    appointments,
    slots,
    doctors: doctorDirectory,
    specializations,
    patients,
    loading,
    saving,
    error,
    doctorError,
    refetch,
    createAppointment,
    updateAppointment,
    createSlot,
    updateSlot,
    cancelSlot,
    cancelAppointment,
  } = useAppointments(filters)

  const today = new Date().toISOString().slice(0, 10)
  const availableUpcomingSlotCount = slots
    .filter(slot => slot.isActive && isFutureSlot(slot))
    .reduce((sum, slot) => sum + Math.max(0, (slot.availableCount ?? slot.capacity - slot.bookedCount)), 0)
  const stats = [
    { label: "Today's Appointments", value: appointments.filter(a => a.startAt?.slice(0, 10) === today).length, icon: CalendarClock, tone: 'primary' },
    { label: 'Available Slots', value: availableUpcomingSlotCount, icon: Clock, tone: 'blue' },
    { label: 'Confirmed', value: appointments.filter(a => a.status === 'Confirmed').length, icon: CheckCircle, tone: 'success' },
    { label: 'Completed', value: appointments.filter(a => a.status === 'Completed').length, icon: Stethoscope, tone: 'purple' },
  ]
  const doctors = useMemo(() => doctorDirectory, [doctorDirectory])
  const appointmentSpecializations = useMemo(() => {
    if (specializations.length > 0) return specializations
    return Array.from(new Set(doctors.map(doctor => String(doctor.specialty || '').trim()).filter(Boolean))).sort()
  }, [doctors, specializations])
  const appointmentDoctorOptions = useMemo(() => {
    if (!appointmentForm.specialty) return []
    return doctors.filter(doctor => sameText(doctor.specialty, appointmentForm.specialty))
  }, [appointmentForm.specialty, doctors])
  const selectedDoctorSlots = useMemo(() => {
    const term = appointmentForm.doctorName.trim().toLowerCase()
    if (!term) return []
    return slots.filter(slot =>
      slot.doctorName.toLowerCase() === term &&
      sameText(slot.specialty, appointmentForm.specialty) &&
      slot.isActive && isFutureSlot(slot) &&
      (
        (slot.availableCount ?? slot.capacity - slot.bookedCount) > 0 ||
        String(slot.doctorTimeSlotId) === String(appointmentForm.doctorTimeSlotId)
      ))
  }, [appointmentForm.doctorName, appointmentForm.doctorTimeSlotId, appointmentForm.specialty, slots])
  const selectedSlot = selectedDoctorSlots.find(slot => String(slot.doctorTimeSlotId) === String(appointmentForm.doctorTimeSlotId))
  const hasNoUpcomingSessions = Boolean(appointmentForm.doctorName && selectedDoctorSlots.length === 0)
  const selectedPatient = patients.find(patient => String(patient.patientId) === String(appointmentForm.patientId))
  const selectedDoctor = doctors.find(doctor => doctor.doctorName.toLowerCase() === appointmentForm.doctorName.trim().toLowerCase())
  const slotDoctorOptions = useMemo(() => doctors.filter(doctor => doctor.doctorId), [doctors])
  const selectedSlotDoctor = doctors.find(doctor => String(doctor.doctorId) === String(slotForm.doctorId))
  const selectedConsultationFee = getConsultationFee({
    doctor: selectedDoctor,
    slot: selectedSlot,
    specialty: selectedSlot?.specialty || selectedDoctor?.specialty,
  })
  const filteredAppointments = useMemo(() => {
    const term = debouncedSearch.trim().toLowerCase()
    return appointments
      .filter(appointment => {
        const matchesSearch = !term || [
          appointment.patientName,
          appointment.patientPhone,
          appointment.patientEmail,
          appointment.doctorName,
          appointment.specialty,
        ].some(value => String(value || '').toLowerCase().includes(term))
        const matchesStatus = appointment.status === status
        const matchesDate = !date || (appointment.startAt || '').slice(0, 10) === date
        return matchesSearch && matchesStatus && matchesDate
      })
      .slice()
      .sort((a, b) => {
        const timeA = new Date(a.createdAt || a.startAt || 0).getTime()
        const timeB = new Date(b.createdAt || b.startAt || 0).getTime()
        if (timeB !== timeA) return timeB - timeA
        return (b.appointmentId || 0) - (a.appointmentId || 0)
      })
  }, [appointments, date, debouncedSearch, status])
  const filteredSlots = useMemo(() => {
    const term = debouncedSlotSearch.trim().toLowerCase()
    return slots.filter(slot => {
      const displayStatus = getSlotDisplayStatus(slot)
      const matchesStatus = displayStatus === slotStatus
      const matchesSearch = !term || [
        slot.doctorName,
        slot.specialty,
        formatDateTime(slot.startAt),
        formatTime(slot.endAt),
        formatSlotLocation(slot),
        displayStatus,
        `${slot.bookedCount}/${slot.capacity}`,
        `${Math.max(0, (slot.availableCount ?? slot.capacity - slot.bookedCount))} available`,
        slot.nextAppointmentNumber ? `#${slot.nextAppointmentNumber}` : '',
      ].some(value => String(value || '').toLowerCase().includes(term))
      return matchesStatus && matchesSearch
    })
  }, [debouncedSlotSearch, slots, slotStatus])

  const closeAppointmentModal = () => {
    setAppointmentOpen(false)
    setEditTarget(null)
    setAppointmentForm(emptyAppointment)
  }

  const openCreateAppointment = () => {
    setEditTarget(null)
    setAppointmentForm(emptyAppointment)
    setAppointmentOpen(true)
  }

  const openEditAppointment = (appointment) => {
    const patient = patients.find(p => String(p.patientId) === String(appointment.patientId))
    setEditTarget(appointment)
    setAppointmentForm({
      specialty: appointment.specialty || '',
      doctorName: appointment.doctorName || '',
      doctorTimeSlotId: String(appointment.doctorTimeSlotId || ''),
      appointmentDate: toDateInputValue(appointment.startAt),
      patientId: appointment.patientId ? String(appointment.patientId) : '',
      patientName: appointment.patientName || '',
      patientAge: patient ? String(getPatientAge(patient)) : '',
      patientPhone: appointment.patientPhone || '',
      patientEmail: appointment.patientEmail || '',
      appointmentType: appointment.appointmentType || 'Consultation',
    })
    setAppointmentOpen(true)
  }

  const handleAppointmentSubmit = async (e) => {
    e.preventDefault()
    if (!appointmentForm.doctorTimeSlotId || !appointmentForm.appointmentDate) return
    if (!selectedSlot || !isFutureSlot(selectedSlot)) return

    const payload = {
      patientId: appointmentForm.patientId ? Number(appointmentForm.patientId) : null,
      patientName: appointmentForm.patientName,
      patientPhone: appointmentForm.patientPhone,
      patientEmail: appointmentForm.patientEmail,
      appointmentType: appointmentForm.appointmentType,
      consultationFee: selectedConsultationFee,
      doctorTimeSlotId: Number(appointmentForm.doctorTimeSlotId),
    }
    if (editTarget) {
      await updateAppointment(editTarget.appointmentId, {
        ...payload,
        status: editTarget.status,
      })
    } else {
      await createAppointment(payload)
    }
    closeAppointmentModal()
  }

  const handlePatientNameChange = (value) => {
    const matchedPatient = patients.find(patient => getPatientFullName(patient).toLowerCase() === value.trim().toLowerCase())

    if (matchedPatient) {
      setAppointmentForm({
        ...appointmentForm,
        patientId: String(matchedPatient.patientId),
        patientName: getPatientFullName(matchedPatient),
        patientAge: String(getPatientAge(matchedPatient)),
        patientPhone: matchedPatient.phoneNumber || '',
        patientEmail: matchedPatient.email || '',
      })
      return
    }

    setAppointmentForm({
      ...appointmentForm,
      patientId: '',
      patientName: value,
    })
  }

  const handleSlotSubmit = async (e) => {
    e.preventDefault()
    const resolvedDoctor = slotDoctorOptions.find(doctor => String(doctor.doctorId) === String(slotForm.doctorId))
    if (!resolvedDoctor) {
      setRoomError('Select an approved registered doctor.')
      return
    }

    if (!slotForm.roomId) {
      setRoomError('Check availability and select a room.')
      return
    }

    const consultationFee = Number(slotForm.consultationFee)
    if (!Number.isFinite(consultationFee) || consultationFee <= 0 || consultationFee > 1000000) {
      setRoomError('Consultation fee must be between LKR 0.01 and LKR 1,000,000.00.')
      return
    }

    if (!/^\d+(\.\d{1,2})?$/.test(String(slotForm.consultationFee).trim())) {
      setRoomError('Consultation fee can contain a maximum of two decimal places.')
      return
    }

    const capacity = Number(slotForm.capacity)
    if (!Number.isInteger(capacity) || capacity < 1 || capacity > 50) {
      setRoomError('Slot capacity must be between 1 and 50.')
      return
    }

    if (!editSlotTarget) {
      const activeDoctorSlots = (slots || []).filter(
        s => s.doctorId === Number(resolvedDoctor.doctorId) && s.isActive && new Date(s.endAt) > new Date()
      )
      if (activeDoctorSlots.length >= 50) {
        setRoomError('This doctor has reached the maximum limit of 50 active appointment schedules. Please complete or cancel existing schedules before creating new ones.')
        return
      }
    }

    const payload = {
      doctorId: Number(resolvedDoctor.doctorId),
      doctorName: resolvedDoctor.doctorName,
      specialty: resolvedDoctor.specialty,
      startAt: new Date(`${slotForm.date}T${slotForm.startTime}`).toISOString(),
      endAt: new Date(`${slotForm.date}T${slotForm.endTime}`).toISOString(),
      capacity,
      consultationFee,
      roomId: Number(slotForm.roomId),
      isActive: true,
    }

    try {
      if (editSlotTarget) {
        await updateSlot(editSlotTarget.doctorTimeSlotId, payload)
      } else {
        await createSlot(payload)
      }
      closeSlotModal()
    } catch (requestError) {
      setRoomError(requestError.response?.data?.message || requestError.message || `Unable to ${editSlotTarget ? 'update' : 'create'} slot.`)
    }
  }

  const openCreateSlot = () => {
    setEditSlotTarget(null)
    setSlotForm(emptySlot)
    setAvailableRooms([])
    setRoomError('')
    setSlotOpen(true)
  }

  const openEditSlot = (slot) => {
    const matchedDoctor = slotDoctorOptions.find(doctor => String(doctor.doctorId) === String(slot.doctorId))
      || slotDoctorOptions.find(doctor => doctor.doctorName.toLowerCase() === String(slot.doctorName || '').toLowerCase())
    setEditSlotTarget(slot)
    setSlotForm({
      doctorId: matchedDoctor?.doctorId ? String(matchedDoctor.doctorId) : (slot.doctorId ? String(slot.doctorId) : ''),
      doctorName: slot.doctorName || '',
      specialty: slot.specialty || '',
      date: toDateInputValue(slot.startAt),
      startTime: toTimeInputValue(slot.startAt),
      endTime: toTimeInputValue(slot.endAt),
      capacity: slot.capacity || 1,
      consultationFee: String(slot.consultationFee ?? DEFAULT_CONSULTATION_FEE),
      roomId: slot.roomId ? String(slot.roomId) : '',
    })
    setAvailableRooms(slot.roomId ? [slot] : [])
    setRoomError('')
    setSlotOpen(true)
  }

  const closeSlotModal = () => {
    setSlotOpen(false)
    setEditSlotTarget(null)
    setSlotForm(emptySlot)
    setAvailableRooms([])
    setRoomError('')
  }

  const resetRoomSelection = (nextSlotForm) => {
    setSlotForm({ ...slotForm, ...nextSlotForm, roomId: '' })
    setAvailableRooms([])
    setRoomError('')
  }

  const checkAvailableRooms = async () => {
    if (!slotForm.date || !slotForm.startTime || !slotForm.endTime) {
      setRoomError('Select date, start time, and end time first.')
      return
    }

    const startAt = new Date(`${slotForm.date}T${slotForm.startTime}`)
    const endAt = new Date(`${slotForm.date}T${slotForm.endTime}`)
    if (Number.isNaN(startAt.getTime()) || Number.isNaN(endAt.getTime()) || endAt <= startAt) {
      setRoomError('Slot end time must be after start time.')
      return
    }

    setCheckingRooms(true)
    setRoomError('')
    try {
      const rooms = await roomApi.getAvailable(
        startAt.toISOString(),
        endAt.toISOString(),
        editSlotTarget?.doctorTimeSlotId,
      )
      setAvailableRooms(rooms)
      setSlotForm(current => ({
        ...current,
        roomId: rooms.some(room => String(room.roomId) === String(current.roomId)) ? current.roomId : '',
      }))
      if (rooms.length === 0) {
        setRoomError('No confirmed rooms are available for this time period.')
      }
    } catch (requestError) {
      setRoomError(requestError.response?.data?.message || 'Unable to check room availability.')
    } finally {
      setCheckingRooms(false)
    }
  }

  const selectSlotDoctor = (doctorId) => {
    const doctor = slotDoctorOptions.find(value => String(value.doctorId) === String(doctorId))
    if (!doctor) {
      setSlotForm({ ...slotForm, doctorId: '', doctorName: '', specialty: '' })
      return
    }

    setSlotForm({
      ...slotForm,
      doctorId: String(doctor.doctorId),
      doctorName: doctor.doctorName,
      specialty: doctor.specialty || '',
      consultationFee: slotForm.consultationFee || String(getConsultationFee({ doctor, specialty: doctor.specialty })),
    })
    setRoomError('')
  }

  const handleSlotCancel = async (e) => {
    e.preventDefault()
    if (!cancelSlotTarget) return
    await cancelSlot(cancelSlotTarget.doctorTimeSlotId, slotCancelReason)
    setCancelSlotTarget(null)
    setSlotCancelReason('')
  }

  const handleCancel = async (e) => {
    e.preventDefault()
    if (!cancelTarget) return
    await cancelAppointment(cancelTarget.appointmentId, cancelReason)
    setCancelTarget(null)
    setCancelReason('')
  }

  const columns = [
    {
      key: 'patient',
      label: 'Patient',
      render: (a) => (
        <div className="appt-patient-cell">
          <span className="appt-avatar">{getInitials(a.patientName)}</span>
          <div>
            <strong>{a.patientName}</strong>
            <span>{a.patientPhone}</span>
          </div>
        </div>
      ),
    },
    {
      key: 'date',
      label: 'Date',
      render: (a) => formatDateOnly(a.startAt),
    },
    {
      key: 'number',
      label: 'No. / Time',
      render: (a) => (
        <div className="appt-number-cell">
          <strong>#{a.appointmentNumber || '-'}</strong>
          <span>{formatTime(a.startAt)}</span>
        </div>
      ),
    },
    {
      key: 'doctor',
      label: 'Doctor / Slot',
      render: (a) => (
        <div className="appt-doctor-cell">
          <strong><Stethoscope size={13} /> {a.doctorName}</strong>
          <span>{a.specialty} · {formatDateTime(a.startAt)}-{formatTime(a.endAt)}</span>
        </div>
      ),
    },
    { key: 'appointmentType', label: 'Type' },
    {
      key: 'consultationFee',
      label: 'Fee',
      render: (a) => formatCurrency(a.consultationFee ?? getConsultationFee({
        doctor: doctors.find(doctor => doctor.doctorName.toLowerCase() === String(a.doctorName || '').toLowerCase()),
        slot: a,
        specialty: a.specialty,
      })),
    },
    { key: 'status', label: 'Status', width: '120px', render: (a) => <StatusPill status={a.status} /> },
    {
      key: 'actions',
      label: '',
      align: 'right',
      width: '150px',
      render: (a) => (
        <div className="appt-actions">
          <Button
            variant="secondary"
            size="sm"
            icon={Pencil}
            onClick={() => openEditAppointment(a)}
            disabled={a.status === 'Cancelled' || a.status === 'Completed'}
            className="appt-icon-btn"
            aria-label={`Edit appointment for ${a.patientName}`}
          />
          <Button
            variant="danger"
            size="sm"
            icon={X}
            onClick={() => setCancelTarget(a)}
            disabled={a.status === 'Cancelled' || a.status === 'Completed'}
            className="appt-icon-btn"
            aria-label={`Cancel appointment for ${a.patientName}`}
          />
        </div>
      ),
    },
  ]

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="appt-header">
          <div>
            <h1 className="page-title">Appointments</h1>
            <p className="page-subtitle">Admin scheduling, doctor time slots, capacity, and patient visit workflow</p>
          </div>
          <div className="appt-header__actions">
            <Button variant="secondary" icon={RefreshCw} onClick={refetch}>Refresh</Button>
            <Button variant="secondary" icon={CalendarClock} onClick={openCreateSlot}>Add Slot</Button>
            <Button variant="primary" icon={Plus} onClick={openCreateAppointment}>New Appointment</Button>
          </div>
        </div>
      </div>

      <div className="appt-stats animate-fade-in">
        {stats.map(s => (
          <div key={s.label} className={`appt-stat appt-stat--${s.tone}`}>
            <span className="appt-stat__icon"><s.icon size={18} /></span>
            <div>
              <span className="appt-stat__label">{s.label}</span>
              <strong className="appt-stat__val">{s.value}</strong>
            </div>
          </div>
        ))}
      </div>

      <div className="appt-panel">
        <div className="appt-toolbar">
          <label className="appt-search">
            <Search size={15} />
            <input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Search patient, phone, doctor" />
          </label>
          <select value={status} onChange={e => setStatus(e.target.value)} className="appt-filter">
            {STATUS_FILTERS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <input type="date" value={date} onChange={e => setDate(e.target.value)} className="appt-filter" />
        </div>

        {error && (
          <div className="appt-error">
            <AlertTriangle size={16} />
            <span>{error}</span>
          </div>
        )}

        <Table columns={columns} data={filteredAppointments} loading={loading} emptyMessage="No appointments match the current filters" />
      </div>

      <div className="appt-slots">
        <div className="appt-section__header appt-section__header--split">
          <div className="appt-section__title">
            <Clock size={16} />
            <h3>Doctor Time Slots</h3>
            <span className="appt-section__count">{filteredSlots.length}/{slots.length}</span>
          </div>
          <div className="appt-slot-controls">
            <label className="appt-search appt-search--slots">
              <Search size={15} />
              <input type="search" value={slotSearch} onChange={e => setSlotSearch(e.target.value)} placeholder="Search doctor, specialty, room" />
            </label>
            <select value={slotStatus} onChange={e => setSlotStatus(e.target.value)} className="appt-filter">
              {SLOT_STATUS_FILTERS.map(value => <option key={value} value={value}>{value}</option>)}
            </select>
            <div className="appt-view-toggle" aria-label="Doctor time slot view">
              <button type="button" className={slotView === 'table' ? 'is-active' : ''} onClick={() => setSlotView('table')} aria-pressed={slotView === 'table'} aria-label="Table view" title="Table view">
                <Table2 size={15} />
              </button>
              <button type="button" className={slotView === 'cards' ? 'is-active' : ''} onClick={() => setSlotView('cards')} aria-pressed={slotView === 'cards'} aria-label="Card view" title="Card view">
                <LayoutGrid size={15} />
              </button>
            </div>
          </div>
        </div>
        {filteredSlots.length === 0 ? (
          <div className="appt-slot-empty">No {slotStatus.toLowerCase()} doctor time slots match the current search.</div>
        ) : slotView === 'table' ? (
          <div className="appt-slot-table-wrap">
            <table className="appt-slot-table">
              <thead>
                <tr>
                  <th className="slot-col-doctor">Doctor</th>
                  <th className="slot-col-specialty">Specialty</th>
                  <th className="slot-col-time">Date / Time</th>
                  <th className="slot-col-room">Room</th>
                  <th className="slot-col-bookings">Bookings</th>
                  <th className="slot-col-next">Next No.</th>
                  <th className="slot-col-status">Status</th>
                  <th className="slot-col-actions" aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {filteredSlots.map(slot => (
                  <tr key={slot.doctorTimeSlotId}>
                    <td className="slot-col-doctor"><strong title={slot.doctorName}>{slot.doctorName}</strong></td>
                    <td className="slot-col-specialty">{slot.specialty}</td>
                    <td className="slot-col-time">{formatDateTime(slot.startAt)}-{formatTime(slot.endAt)}</td>
                    <td className="slot-col-room">{formatSlotLocation(slot) || '-'}</td>
                    <td className="slot-col-bookings">
                      <div className="appt-slot-capacity">
                        <span>{slot.bookedCount}/{slot.capacity}</span>
                      </div>
                    </td>
                    <td className="slot-col-next">
                      {slot.nextAppointmentNumber > 0
                        ? `#${slot.nextAppointmentNumber} at ${formatTime(slot.startAt)}`
                        : '-'}
                    </td>
                    <td className="slot-col-status"><StatusPill status={getSlotDisplayStatus(slot)} /></td>
                    <td className="slot-col-actions">
                      <div className="appt-slot-row-actions">
                        <Button
                          variant="secondary"
                          size="sm"
                          icon={Pencil}
                          className="appt-slot__icon-btn"
                          onClick={() => openEditSlot(slot)}
                          disabled={!canManageSlot(slot)}
                          aria-label="Edit doctor time slot"
                        />
                        <Button
                          variant="danger"
                          size="sm"
                          icon={X}
                          className="appt-slot__icon-btn"
                          onClick={() => setCancelSlotTarget(slot)}
                          disabled={!canManageSlot(slot)}
                          aria-label="Cancel doctor time slot"
                        />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
        <div className="appt-slot-grid">
          {filteredSlots.map(slot => (
            <div className="appt-slot" key={slot.doctorTimeSlotId}>
              <div>
                <strong>{slot.doctorName}</strong>
                <span>{slot.specialty}</span>
              </div>
              <p>{formatDateTime(slot.startAt)}-{formatTime(slot.endAt)}</p>
              {formatSlotLocation(slot) && (
                <small className="appt-slot__location"><MapPin size={13} /> {formatSlotLocation(slot)}</small>
              )}
              <meter min="0" max={slot.capacity} value={slot.bookedCount} />
              {slot.nextAppointmentNumber > 0 && (
                <small>Next #{slot.nextAppointmentNumber} at {formatTime(slot.startAt)}</small>
              )}
              <small>{slot.bookedCount}/{slot.capacity} booked · {slot.availableCount ?? slot.capacity - slot.bookedCount} available</small>
              <div className="appt-slot__actions">
                <Button
                  variant="secondary"
                  size="sm"
                    icon={Pencil}
                    className="appt-slot__icon-btn"
                    onClick={() => openEditSlot(slot)}
                    disabled={!canManageSlot(slot)}
                    aria-label="Edit doctor time slot"
                  />
                <Button
                  variant="danger"
                  size="sm"
                    icon={X}
                    className="appt-slot__icon-btn"
                    onClick={() => setCancelSlotTarget(slot)}
                    disabled={!canManageSlot(slot)}
                    aria-label="Cancel doctor time slot"
                  />
              </div>
            </div>
          ))}
        </div>
        )}
      </div>

      <Modal
        open={appointmentOpen}
        onClose={closeAppointmentModal}
        title={editTarget ? 'Edit Appointment' : 'Create Appointment'}
        subtitle={editTarget ? 'Select a new available date and session' : 'Book on behalf of a patient'}
        size="lg"
        id="appointment-form-modal"
      >
        <form className="appt-form" onSubmit={handleAppointmentSubmit}>
          {editTarget && (
            <p className="appt-form__hint appt-form__wide" style={{ color: 'var(--color-primary)', fontWeight: 500, margin: '0 0 10px 0' }}>
              Patient and doctor details are locked. Select another available date and session if you wish to reschedule.
            </p>
          )}
          <div className="appt-form__wide appt-top-row">
            <label>Specialization
              <select
                required
                value={appointmentForm.specialty}
                onChange={e => {
                  setAppointmentForm({
                    ...appointmentForm,
                    specialty: e.target.value,
                    doctorName: '',
                    doctorTimeSlotId: '',
                    appointmentDate: '',
                  })
                }}
                disabled={Boolean(editTarget) || Boolean(doctorError) || appointmentSpecializations.length === 0}
              >
                <option value="">{appointmentSpecializations.length ? 'Select specialization' : 'No specializations available'}</option>
                {appointmentSpecializations.map(specialty => (
                  <option key={specialty} value={specialty}>{specialty}</option>
                ))}
              </select>
            </label>
            <label>Doctor Name
              <SearchableDoctorSelect
                doctors={doctors}
                selectedSpecialty={appointmentForm.specialty}
                selectedDoctorName={appointmentForm.doctorName}
                disabled={Boolean(editTarget) || Boolean(doctorError) || doctors.length === 0}
                placeholder={appointmentForm.specialty ? 'Type to search doctor...' : 'Type to search all doctors...'}
                required
                onSelectDoctor={(doctor) => {
                  setAppointmentForm(prev => ({
                    ...prev,
                    doctorName: doctor ? doctor.doctorName : '',
                    specialty: doctor?.specialty || prev.specialty,
                    doctorTimeSlotId: '',
                    appointmentDate: '',
                  }))
                }}
              />
            </label>
            <label>Appointment date
              <select required value={appointmentForm.appointmentDate}
                disabled={!appointmentForm.doctorName || hasNoUpcomingSessions}
                onChange={e => setAppointmentForm(prev => ({ ...prev, appointmentDate: e.target.value, doctorTimeSlotId: '' }))}>
                <option value="">{hasNoUpcomingSessions ? 'No dates available' : 'Select appointment date'}</option>
                {Array.from(new Set(selectedDoctorSlots.map(slot => toDateInputValue(slot.startAt)))).sort().map(day => (
                  <option key={day} value={day}>{formatDateOnly(`${day}T00:00:00`)}</option>
                ))}
              </select>
            </label>
            <label>Appointment session
              <select required value={appointmentForm.doctorTimeSlotId}
                disabled={!appointmentForm.appointmentDate || hasNoUpcomingSessions}
                onChange={e => setAppointmentForm(prev => ({ ...prev, doctorTimeSlotId: e.target.value }))}>
                <option value="">{hasNoUpcomingSessions ? 'No sessions available' : 'Select appointment session'}</option>
                {selectedDoctorSlots.filter(slot => toDateInputValue(slot.startAt) === appointmentForm.appointmentDate).map(slot => (
                  <option key={slot.doctorTimeSlotId} value={slot.doctorTimeSlotId}>
                    {formatTime(slot.startAt)} - {formatTime(slot.endAt)}
                  </option>
                ))}
              </select>
            </label>
          </div>
          {doctorError && (
            <p className="appt-form__hint appt-form__hint--error appt-form__wide">{doctorError}</p>
          )}
          {!doctorError && !loading && appointmentSpecializations.length === 0 && (
            <p className="appt-form__hint appt-form__wide">No approved doctor specializations are available.</p>
          )}
          {appointmentForm.specialty && appointmentDoctorOptions.length === 0 && (
            <p className="appt-form__hint appt-form__wide">No approved doctors are available for this specialization.</p>
          )}
          {hasNoUpcomingSessions && (
            <p className="appt-form__hint appt-form__hint--error appt-form__wide" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <AlertTriangle size={15} /> No upcoming sessions are available for this doctor.
            </p>
          )}
          {selectedSlot && (
            <div className="appt-details appt-form__wide">
              <div>
                <strong>
                  {editTarget
                    ? String(selectedSlot.doctorTimeSlotId) === String(editTarget.doctorTimeSlotId)
                      ? `Appointment #${editTarget.appointmentNumber} (Current Session)`
                      : `Moving from #${editTarget.appointmentNumber} → New Session`
                    : 'Appointment details'}
                </strong>
                {editTarget && String(selectedSlot.doctorTimeSlotId) !== String(editTarget.doctorTimeSlotId) && (
                  <>
                    <span>
                      New queue number: {selectedSlot.nextAppointmentNumber > 0 ? `#${selectedSlot.nextAppointmentNumber}` : 'Assigned upon save'}
                    </span>
                    <small>A new queue number for this doctor's session will be automatically assigned on save.</small>
                  </>
                )}
                {!editTarget && (
                  <>
                    <span>Next available appointment number: {selectedSlot.nextAppointmentNumber > 0 ? `#${selectedSlot.nextAppointmentNumber}` : 'Unavailable'}</span>
                    <small>This number may change if someone books before you confirm.</small>
                  </>
                )}
                <span>
                  Appointment time:{' '}
                  {formatDateTime(selectedSlot.startAt)}
                </span>
                {formatSlotLocation(selectedSlot) && (
                  <span className="appt-details__location">
                    <MapPin size={13} /> Location: {formatSlotLocation(selectedSlot)}
                  </span>
                )}
              </div>
              <div className="appt-fee">
                <span>Consultation fee</span>
                <strong>{formatCurrency(selectedConsultationFee)}</strong>
              </div>
            </div>
          )}
          <label>Patient Name
            <input
              required
              disabled={Boolean(editTarget) || hasNoUpcomingSessions}
              readOnly={Boolean(editTarget)}
              list="appointment-patients"
              value={appointmentForm.patientName}
              onChange={e => handlePatientNameChange(e.target.value)}
              placeholder={hasNoUpcomingSessions ? 'Doctor has no upcoming sessions' : 'Select existing patient or type a new name'}
            />
            <datalist id="appointment-patients">
              {patients.map(patient => (
                <option key={patient.patientId} value={getPatientFullName(patient)}>
                  {patient.phoneNumber}
                </option>
              ))}
            </datalist>
          </label>
          <label>Patient Age
            <input
              type="number"
              min="0"
              disabled={Boolean(editTarget) || hasNoUpcomingSessions}
              value={appointmentForm.patientAge}
              onChange={e => setAppointmentForm({ ...appointmentForm, patientAge: e.target.value })}
              readOnly={Boolean(editTarget) || Boolean(selectedPatient)}
            />
          </label>
          <label>Patient Phone
            <input
              required
              disabled={Boolean(editTarget) || hasNoUpcomingSessions}
              readOnly={Boolean(editTarget)}
              value={appointmentForm.patientPhone}
              onChange={e => setAppointmentForm({ ...appointmentForm, patientPhone: e.target.value })}
            />
          </label>
          <label>Patient Email
            <input
              type="email"
              disabled={Boolean(editTarget) || hasNoUpcomingSessions}
              readOnly={Boolean(editTarget)}
              value={appointmentForm.patientEmail}
              onChange={e => setAppointmentForm({ ...appointmentForm, patientEmail: e.target.value })}
            />
          </label>
          <label>Appointment Type
            <select
              disabled={Boolean(editTarget) || hasNoUpcomingSessions}
              value={appointmentForm.appointmentType}
              onChange={e => setAppointmentForm({ ...appointmentForm, appointmentType: e.target.value })}
            >
              {TYPES.map(t => <option key={t} value={t}>{t}</option>)}
            </select>
          </label>
          <div className="appt-form__actions">
            <Button variant="secondary" onClick={closeAppointmentModal}>Close</Button>
            <Button
              type="submit"
              loading={saving}
              disabled={hasNoUpcomingSessions || Boolean(doctorError)}
            >
              {editTarget ? 'Save Changes' : 'Create Appointment'}
            </Button>
          </div>
        </form>
      </Modal>

      <Modal
        open={slotOpen}
        onClose={closeSlotModal}
        title={editSlotTarget ? 'Edit Doctor Time Slot' : 'Add Doctor Time Slot'}
        subtitle={editSlotTarget ? 'Update doctor availability' : 'Add on behalf of a doctor'}
        size="md"
        id="slot-form-modal"
      >
        <form className="appt-form" onSubmit={handleSlotSubmit}>
          <label>Doctor Name
            <select
              required
              value={slotForm.doctorId}
              onChange={e => selectSlotDoctor(e.target.value)}
            >
              <option value="">Select doctor</option>
              {slotDoctorOptions.map(doctor => (
                <option key={doctor.doctorId || doctor.doctorName} value={doctor.doctorId}>
                  {doctor.doctorName}
                </option>
              ))}
            </select>
          </label>
          <label>Specialty
            <input
              required
              readOnly
              value={selectedSlotDoctor?.specialty || slotForm.specialty}
              placeholder="Select an approved doctor first"
            />
          </label>
          <label>Date
            <input required type="date" value={slotForm.date} onChange={e => resetRoomSelection({ date: e.target.value })} />
          </label>
          <label>Start Time
            <input required type="time" value={slotForm.startTime} onChange={e => resetRoomSelection({ startTime: e.target.value })} />
          </label>
          <label>End Time
            <input required type="time" value={slotForm.endTime} onChange={e => resetRoomSelection({ endTime: e.target.value })} />
          </label>
          <label>Appointment Capacity (Max 50)
            <input required type="number" min="1" max="50" value={slotForm.capacity} onChange={e => setSlotForm({ ...slotForm, capacity: e.target.value })} />
          </label>
          <label>Consultation Fee (LKR)
            <input
              required
              type="number"
              min="0.01"
              max="1000000"
              step="0.01"
              value={slotForm.consultationFee}
              onChange={e => setSlotForm({ ...slotForm, consultationFee: e.target.value })}
              placeholder="2500.00"
            />
          </label>
          <label className="appt-form__wide">Available Room
            <select
              required
              value={slotForm.roomId}
              onChange={e => {
                setSlotForm({ ...slotForm, roomId: e.target.value })
                setRoomError('')
              }}
            >
              <option value="">{availableRooms.length ? 'Select an available room' : 'Check availability first'}</option>
              {availableRooms.map(room => (
                <option key={room.roomId} value={room.roomId}>
                  {formatRoomOption(room)}
                </option>
              ))}
            </select>
          </label>
          {roomError && (
            <p className="appt-form__hint appt-form__hint--error appt-form__wide">{roomError}</p>
          )}
          <p className="appt-form__hint appt-form__wide">
            Appointments use the scheduled session start time. Appointment numbers indicate queue order.
          </p>
          <div className="appt-form__actions appt-form__wide">
            <Button type="button" variant="outline" icon={RefreshCw} loading={checkingRooms} onClick={checkAvailableRooms}>Check Available Rooms</Button>
            <Button variant="secondary" onClick={closeSlotModal}>Close</Button>
            <Button type="submit" loading={saving}>{editSlotTarget ? 'Update Slot' : 'Save Slot'}</Button>
          </div>
        </form>
      </Modal>

      <Modal open={Boolean(cancelSlotTarget)} onClose={() => setCancelSlotTarget(null)} title="Cancel Doctor Time Slot" subtitle="Booked appointments will be cancelled" size="sm" id="cancel-slot-modal">
        <form className="appt-form appt-form--single" onSubmit={handleSlotCancel}>
          <label>Cancellation Message
            <textarea
              rows="4"
              value={slotCancelReason}
              onChange={e => setSlotCancelReason(e.target.value)}
              placeholder="Example: Doctor is unavailable. Please contact the hospital to reschedule."
            />
          </label>
          <div className="appt-form__actions">
            <Button variant="secondary" onClick={() => setCancelSlotTarget(null)}>Close</Button>
            <Button variant="danger" type="submit" loading={saving}>Cancel Slot</Button>
          </div>
        </form>
      </Modal>

      <Modal open={Boolean(cancelTarget)} onClose={() => setCancelTarget(null)} title="Cancel Appointment" subtitle="A reason is required for audit history" size="sm" id="cancel-appointment-modal">
        <form className="appt-form appt-form--single" onSubmit={handleCancel}>
          <label>Cancellation Reason
            <textarea required rows="4" value={cancelReason} onChange={e => setCancelReason(e.target.value)} />
          </label>
          <div className="appt-form__actions">
            <Button variant="secondary" onClick={() => setCancelTarget(null)}>Close</Button>
            <Button variant="danger" type="submit" loading={saving}>Cancel Appointment</Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}

function StatusPill({ status }) {
  const config = STATUS_CONFIG[status] || STATUS_CONFIG.Confirmed
  const Icon = config.icon
  return (
    <span className="appt-status" style={{ background: config.bg, color: config.color }}>
      <Icon size={12} />
      {status}
    </span>
  )
}

function getConsultationFee({ doctor, slot, specialty }) {
  const fee = doctor?.consultationFee ?? slot?.consultationFee
  if (Number.isFinite(Number(fee)) && Number(fee) >= 0) return Number(fee)

  const resolvedSpecialty = specialty || doctor?.specialty || slot?.specialty
  return SPECIALTY_CONSULTATION_FEES[resolvedSpecialty] ?? DEFAULT_CONSULTATION_FEE
}

function formatRoomOption(room) {
  const label = [room.roomNumber, room.roomName, room.floor].filter(Boolean).join(' - ')
  return label || `Room ${room.roomId}`
}

function formatSlotLocation(slot) {
  return [slot?.roomNumber, slot?.roomName, slot?.floor]
    .map(value => String(value || '').trim())
    .filter(Boolean)
    .join(', ')
}

function isFutureSlot(slot) {
  return Boolean(slot?.startAt) && new Date(slot.startAt).getTime() > Date.now()
}

function getSlotDisplayStatus(slot) {
  if (!slot?.isActive) return 'Cancelled'
  if (slot?.endAt && new Date(slot.endAt).getTime() <= Date.now()) return 'Completed'
  return 'Upcoming'
}

function canManageSlot(slot) {
  return getSlotDisplayStatus(slot) === 'Upcoming'
}

function sameText(a, b) {
  return String(a || '').trim().toLowerCase() === String(b || '').trim().toLowerCase()
}

function formatCurrency(value) {
  return new Intl.NumberFormat('en-LK', {
    style: 'currency',
    currency: 'LKR',
    maximumFractionDigits: 0,
  }).format(Number(value) || 0)
}

function getInitials(name = '') {
  return name.split(' ').filter(Boolean).map(part => part[0]).join('').slice(0, 2).toUpperCase()
}

function getPatientFullName(patient) {
  return patient.fullName || [patient.firstName, patient.lastName].filter(Boolean).join(' ')
}

function getPatientAge(patient) {
  if (typeof patient.age === 'number') return patient.age
  if (!patient.dateOfBirth) return ''

  const today = new Date()
  const birth = new Date(patient.dateOfBirth)
  let age = today.getFullYear() - birth.getFullYear()
  const monthDiff = today.getMonth() - birth.getMonth()
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) age--
  return age
}

function formatDateTime(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function formatDateOnly(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
}

function formatTime(value) {
  if (!value) return ''
  return new Intl.DateTimeFormat('en-LK', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function toDateInputValue(value) {
  if (!value) return ''
  const date = new Date(value)
  const localDate = new Date(date.getTime() - (date.getTimezoneOffset() * 60000))
  return localDate.toISOString().slice(0, 10)
}

function toTimeInputValue(value) {
  if (!value) return ''
  const date = new Date(value)
  const localDate = new Date(date.getTime() - (date.getTimezoneOffset() * 60000))
  return localDate.toISOString().slice(11, 16)
}
