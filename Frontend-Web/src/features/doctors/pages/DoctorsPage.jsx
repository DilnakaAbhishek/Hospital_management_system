import { useState, useEffect, useMemo } from 'react'
import { Search, RefreshCw, Stethoscope, AlertTriangle } from 'lucide-react'
import Button from '../../../components/Button'
import DoctorCard from '../components/DoctorCard'
import { approveDoctor, approveDoctorDeletion, declineDoctor, getDoctorRegistrations, rejectDoctorDeletion } from '../services/doctorApi'
import { useDebounce } from '../../../hooks/useDebounce'
import './DoctorsPage.css'

export default function DoctorsPage() {
  const [doctors, setDoctors] = useState([])
  const [status, setStatus] = useState('Pending')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 250)
  const [filtering, setFiltering] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      setDoctors(await getDoctorRegistrations(status))
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to load doctor requests.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [status])

  useEffect(() => {
    if (!debouncedSearch) {
      setFiltering(false)
      return undefined
    }
    setFiltering(true)
    const timeoutId = window.setTimeout(() => setFiltering(false), 180)
    return () => window.clearTimeout(timeoutId)
  }, [debouncedSearch])

  const visible = useMemo(() => {
    const term = debouncedSearch.toLowerCase().trim()
    if (!term) return doctors
    return doctors.filter(d =>
      `${d.fullName} ${d.email} ${d.specialization} ${d.slmcLicenseNumber} ${d.nic}`
        .toLowerCase()
        .includes(term)
    )
  }, [doctors, debouncedSearch])

  const isSearching = search !== debouncedSearch || filtering

  const review = async (doctor, approve) => {
    const reason = approve ? '' : window.prompt('Optional reason for declining this registration:', '')
    if (!approve && reason === null) return
    try {
      if (approve) {
        await approveDoctor(doctor.doctorId)
      } else {
        await declineDoctor(doctor.doctorId, reason)
      }
      await load()
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to review request.')
    }
  }

  const handleApproveDeletion = async (doctor) => {
    if (!window.confirm(`Are you sure you want to approve the deletion of Dr. ${doctor.fullName}? This will permanently remove their account.`)) return
    try {
      await approveDoctorDeletion(doctor.doctorId)
      await load()
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to approve deletion request.')
    }
  }

  const handleRejectDeletion = async (doctor) => {
    const reason = window.prompt('Optional reason for cancelling/rejecting this deletion request:', '')
    if (reason === null) return
    try {
      await rejectDoctorDeletion(doctor.doctorId, reason)
      await load()
    } catch (requestError) {
      setError(requestError.response?.data?.message || 'Unable to cancel deletion request.')
    }
  }

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="page-title">Doctor Management</h1>
            <p className="page-subtitle">
              Review doctor registrations, deletion requests, and manage medical staff
            </p>
          </div>
          <Button variant="secondary" icon={RefreshCw} onClick={load} id="refresh-doctors">
            Refresh
          </Button>
        </div>
      </div>

      <div className="doctor-toolbar">
        <div className={`doctors__search-wrap ${isSearching ? 'doctors__search-wrap--filtering' : ''}`}>
          <Search size={15} className="doctors__search-icon" />
          <input
            id="doctor-search"
            className="patients__search"
            type="search"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder="Search by doctor name, specialization, SLMC..."
            autoComplete="off"
          />
        </div>

        <div className="doctor-tabs">
          {[
            { label: 'Pending', value: 'Pending' },
            { label: 'Approved', value: 'Approved' },
            { label: 'Declined', value: 'Declined' },
            { label: 'Deletion Requests', value: 'DeletionPending' },
          ].map(tab => (
            <button
              key={tab.value}
              id={`tab-${tab.value.toLowerCase()}`}
              onClick={() => setStatus(tab.value)}
              className={status === tab.value ? 'active' : ''}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="patients__error" style={{ marginBottom: '20px' }}>
          <AlertTriangle size={16} />
          <span>{error}</span>
        </div>
      )}

      {search && <p className="doctors__search-status" aria-live="polite" role="status">{isSearching ? 'Filtering doctors…' : `${visible.length} doctor${visible.length === 1 ? '' : 's'} found`}</p>}

      {loading || isSearching ? (
        <div className="doctor-request-grid">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="skeleton" style={{ height: '280px', borderRadius: '18px' }} />
          ))}
        </div>
      ) : visible.length === 0 ? (
        <div className="doctor-empty">
          <Stethoscope size={40} strokeWidth={1.5} style={{ margin: '0 auto 12px auto', color: 'var(--text-muted)' }} />
          <p>No {status.toLowerCase()} doctor registrations found.</p>
        </div>
      ) : (
        <div className="doctor-request-grid stagger-children animate-fade-in">
          {visible.map(doctor => (
            <DoctorCard
              key={doctor.doctorId}
              doctor={doctor}
              onReview={review}
              onApproveDeletion={handleApproveDeletion}
              onRejectDeletion={handleRejectDeletion}
            />
          ))}
        </div>
      )}
    </div>
  )
}
