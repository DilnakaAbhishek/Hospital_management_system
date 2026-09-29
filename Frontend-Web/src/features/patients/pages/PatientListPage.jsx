import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, Search, LayoutGrid, List, RefreshCw, Users, Trash2, AlertTriangle, ChevronLeft, ChevronRight, UserPlus, UserRound, Eye } from 'lucide-react'
import { usePatients } from '../hooks/usePatients'
import PatientCard from '../components/PatientCard'
import PatientForm from '../components/PatientForm'
import Table from '../../../components/Table'
import Button from '../../../components/Button'
import Modal from '../../../components/Modal'
import { BloodGroupBadge } from '../../../components/Badge'
import { calculateAge, formatDate, getInitials, nameToGradient } from '../../../lib/utils'
import { useDebounce } from '../../../hooks/useDebounce'
import './PatientListPage.css'

export default function PatientListPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState('')
  const [view, setView] = useState('grid')
  const [filterGender, setFilterGender] = useState('all')
  const [filterBlood, setFilterBlood] = useState('all')
  const [sort, setSort] = useState('createdAt:desc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(12)
  const debouncedSearch = useDebounce(search, 250)

  const [formOpen, setFormOpen] = useState(false)
  const [editPatient, setEditPatient] = useState(null)
  const [formError, setFormError] = useState(null)
  const [deleteTarget, setDeleteTarget] = useState(null)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)

  const [sortBy, sortDirection] = sort.split(':')
  const { patients, pagination, summary, loading, error, refetch, createPatient, updatePatient, deletePatient } = usePatients({
    search: debouncedSearch,
    gender: filterGender === 'all' ? '' : filterGender,
    bloodGroup: filterBlood === 'all' ? '' : filterBlood,
    sortBy,
    sortDirection,
    page,
    pageSize,
  })

  const updateFilter = (setter) => (event) => {
    setter(event.target.value)
    setPage(1)
  }

  const handleOpenAdd = () => { setEditPatient(null); setFormError(null); setFormOpen(true) }
  const handleOpenEdit = (p) => { setEditPatient(p); setFormError(null); setFormOpen(true) }
  const handleCloseForm = () => { setFormOpen(false); setEditPatient(null); setFormError(null) }

  const handleSubmit = async (data) => {
    setSaving(true)
    setFormError(null)
    try {
      if (editPatient) {
        await updatePatient(editPatient.patientId, data)
      } else {
        await createPatient(data)
      }
      setFormOpen(false)
      setEditPatient(null)
    } catch (err) {
      setFormError(err.response?.data?.message || err.response?.data?.title || 'Unable to save the patient record. Please try again.')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async () => {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await deletePatient(deleteTarget.patientId)
      setDeleteTarget(null)
    } finally {
      setDeleting(false)
    }
  }

  const columns = [
    {
      key: 'patient',
      label: 'Patient',
      render: (p) => {
        const [c1, c2] = nameToGradient(p.firstName)
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div style={{
              width: '34px', height: '34px', borderRadius: '8px', flexShrink: 0,
              background: `linear-gradient(135deg, ${c1}, ${c2})`,
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              fontWeight: 700, fontSize: '0.8rem', color: 'white',
            }}>
              {getInitials(p.firstName, p.lastName)}
            </div>
            <div>
              <div style={{ fontWeight: 600, color: 'var(--text-primary)', fontSize: '0.88rem' }}>
                {p.firstName} {p.lastName}
              </div>
              <div style={{ fontSize: '0.74rem', color: 'var(--text-muted)' }}>{p.nic}</div>
            </div>
          </div>
        )
      }
    },
    { key: 'gender', label: 'Gender', render: (p) => p.gender },
    { key: 'age', label: 'Age', render: (p) => `${calculateAge(p.dateOfBirth)} yrs` },
    { key: 'bloodGroup', label: 'Blood', render: (p) => <BloodGroupBadge group={p.bloodGroup} /> },
    { key: 'phoneNumber', label: 'Phone', render: (p) => p.phoneNumber },
    { key: 'email', label: 'Email', render: (p) => p.email },
    {
      key: 'createdAt',
      label: 'Registered',
      render: (p) => formatDate(p.createdAt),
    },
    {
      key: 'actions',
      label: '',
      align: 'right',
      render: (p) => (
        <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
          <Button variant="secondary" size="sm" icon={Eye} onClick={() => navigate(`/patients/${p.patientId}`)} id={`table-view-${p.patientId}`}>View</Button>
          <Button variant="primary" size="sm" onClick={() => handleOpenEdit(p)} id={`table-edit-${p.patientId}`}>Edit</Button>
          <Button variant="danger" size="sm" onClick={() => setDeleteTarget(p)} id={`table-delete-${p.patientId}`}>Delete</Button>
        </div>
      ),
    },
  ]

  return (
    <div className="page-wrapper">
      <div className="page-header">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="page-title">Patient Records</h1>
            <p className="page-subtitle">
              {pagination.totalCount} total patients · {patients.length} shown
            </p>
          </div>
          <div style={{ display: 'flex', gap: '10px' }}>
            <Button variant="secondary" icon={RefreshCw} onClick={refetch} id="refresh-patients">
              Refresh
            </Button>
            <Button variant="primary" icon={Plus} onClick={handleOpenAdd} id="add-patient-btn">
              Add Patient
            </Button>
          </div>
        </div>
      </div>

      <section className="patients__overview" aria-label="Patient record summary">
        <SummaryCard icon={Users} label="Total Patients" value={summary?.totalPatients ?? '—'} tone="primary" />
        <SummaryCard icon={UserPlus} label="Registered This Month" value={summary?.registeredThisMonth ?? '—'} tone="success" />
        <SummaryCard icon={UserRound} label="Female Patients" value={summary?.femaleCount ?? '—'} tone="pink" />
        <SummaryCard icon={UserRound} label="Male Patients" value={summary?.maleCount ?? '—'} tone="blue" />
      </section>

      <div className="patients__filters">
        <div className="patients__search-wrap">
          <Search size={15} className="patients__search-icon" />
          <input
            id="patient-search"
            className="patients__search"
            type="search"
            placeholder="Search by name, NIC, email…"
            value={search}
            onChange={updateFilter(setSearch)}
          />
        </div>

        <select
          id="filter-gender"
          className="patients__select"
          value={filterGender}
          onChange={updateFilter(setFilterGender)}
        >
          <option value="all">All Genders</option>
          <option value="Male">Male</option>
          <option value="Female">Female</option>
          <option value="Other">Other</option>
        </select>

        <select
          id="filter-blood"
          className="patients__select"
          value={filterBlood}
          onChange={updateFilter(setFilterBlood)}
        >
          <option value="all">All Blood Groups</option>
          {['A+','A-','B+','B-','AB+','AB-','O+','O-'].map(b => (
            <option key={b} value={b}>{b}</option>
          ))}
        </select>

        <select className="patients__select" value={sort} onChange={updateFilter(setSort)} aria-label="Sort patients">
          <option value="createdAt:desc">Newest registered</option>
          <option value="createdAt:asc">Oldest registered</option>
          <option value="name:asc">Name A–Z</option>
          <option value="name:desc">Name Z–A</option>
          <option value="dob:desc">Youngest first</option>
          <option value="dob:asc">Oldest first</option>
        </select>

        <div className="patients__view-toggle">
          <button
            id="view-grid"
            className={`patients__view-btn ${view === 'grid' ? 'patients__view-btn--active' : ''}`}
            onClick={() => setView('grid')}
            aria-label="Grid view"
          >
            <LayoutGrid size={16} />
          </button>
          <button
            id="view-list"
            className={`patients__view-btn ${view === 'list' ? 'patients__view-btn--active' : ''}`}
            onClick={() => setView('list')}
            aria-label="List view"
          >
            <List size={16} />
          </button>
        </div>
      </div>

      {error && (
        <div className="patients__error">
          <AlertTriangle size={16} />
          <span>{error} – showing demo data</span>
        </div>
      )}

      {view === 'grid' && (
        <div className="patients__grid stagger-children animate-fade-in">
          {loading
            ? Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="skeleton" style={{ height: '220px', borderRadius: '16px' }} />
              ))
            : patients.length === 0
              ? (
                <div className="patients__empty">
                  <Users size={40} strokeWidth={1.5} />
                  <p>No patients match your filters</p>
                </div>
              )
              : patients.map(p => (
                  <PatientCard
                    key={p.patientId}
                    patient={p}
                    onEdit={handleOpenEdit}
                    onDelete={setDeleteTarget}
                    onView={patient => navigate(`/patients/${patient.patientId}`)}
                  />
                ))
          }
        </div>
      )}

      {view === 'list' && (
        <div className="animate-fade-in">
          <Table
            columns={columns}
            data={patients}
            loading={loading}
            emptyMessage="No patients match your search"
          />
        </div>
      )}

      {!loading && pagination.totalPages > 1 && (
        <nav className="patients__pagination" aria-label="Patient list pages">
          <span className="patients__page-summary">
            Page {pagination.page} of {pagination.totalPages}
          </span>
          <div className="patients__page-actions">
            <select
              className="patients__page-size"
              value={pageSize}
              onChange={event => { setPageSize(Number(event.target.value)); setPage(1) }}
              aria-label="Patients per page"
            >
              <option value={12}>12 per page</option>
              <option value={24}>24 per page</option>
              <option value={48}>48 per page</option>
            </select>
            <Button variant="secondary" size="sm" icon={ChevronLeft} disabled={pagination.page <= 1} onClick={() => setPage(current => current - 1)}>
              Previous
            </Button>
            <Button variant="secondary" size="sm" icon={ChevronRight} disabled={pagination.page >= pagination.totalPages} onClick={() => setPage(current => current + 1)}>
              Next
            </Button>
          </div>
        </nav>
      )}

      <PatientForm
        open={formOpen}
        onClose={handleCloseForm}
        onSubmit={handleSubmit}
        patient={editPatient}
        loading={saving}
        error={formError}
      />

      <Modal
        open={Boolean(deleteTarget)}
        onClose={() => setDeleteTarget(null)}
        title="Delete Patient"
        subtitle="This action cannot be undone"
        size="sm"
        id="delete-confirm-modal"
      >
        <div className="delete-confirm">
          <div className="delete-confirm__icon">
            <Trash2 size={28} />
          </div>
          <p className="delete-confirm__msg">
            Are you sure you want to delete{' '}
            <strong>{deleteTarget?.firstName} {deleteTarget?.lastName}</strong>?
            All their records will be permanently removed.
          </p>
          <div className="delete-confirm__actions">
            <Button variant="secondary" onClick={() => setDeleteTarget(null)} id="cancel-delete">
              Cancel
            </Button>
            <Button variant="danger" onClick={handleDelete} loading={deleting} id="confirm-delete">
              Delete Patient
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  )
}

function SummaryCard({ icon: Icon, label, value, tone }) {
  return (
    <div className={`patients__summary-card patients__summary-card--${tone}`}>
      <span className="patients__summary-icon"><Icon size={18} /></span>
      <div>
        <span className="patients__summary-label">{label}</span>
        <strong className="patients__summary-value">{value}</strong>
      </div>
    </div>
  )
}
