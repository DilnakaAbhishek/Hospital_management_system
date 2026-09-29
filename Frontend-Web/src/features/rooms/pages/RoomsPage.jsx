import { Building2, CheckCircle, Plus, Search } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import Button from '../../../components/Button'
import RoomCard from '../components/RoomCard'
import { roomApi } from '../services/roomApi'
import './RoomsPage.css'

const emptyForm = { roomNumber: '', roomName: '', floor: '', description: '' }

export default function RoomsPage() {
  const [rooms, setRooms] = useState([])
  const [form, setForm] = useState(emptyForm)
  const [showForm, setShowForm] = useState(false)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const load = async () => {
    setLoading(true)
    try { setRooms(await roomApi.getAdminRooms()) }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to load rooms.') }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const submit = async event => {
    event.preventDefault()
    if (!window.confirm(`Add room ${form.roomNumber.trim()} and confirm it as available for doctor scheduling?`)) return
    setSaving(true); setError(''); setSuccess('')
    try {
      const created = await roomApi.create(form)
      await roomApi.confirm(created.roomId)
      setForm(emptyForm); setShowForm(false); setSuccess('Room added and confirmed successfully.'); await load()
    } catch (requestError) { setError(requestError.response?.data?.message || 'Unable to add room.') }
    finally { setSaving(false) }
  }

  const confirmRoom = async room => {
    if (!window.confirm(`Confirm ${room.roomNumber} for doctor scheduling?`)) return
    try { await roomApi.confirm(room.roomId); setSuccess('Room confirmed successfully.'); await load() }
    catch (requestError) { setError(requestError.response?.data?.message || 'Unable to confirm room.') }
  }

  const visible = useMemo(() => rooms.filter(room => `${room.roomNumber} ${room.roomName} ${room.floor}`.toLowerCase().includes(search.toLowerCase())), [rooms, search])

  return <div className="page-wrapper">
    <div className="page-header flex items-center justify-between"><div><h1 className="page-title">Available Rooms</h1><p className="page-subtitle">Add and confirm hospital rooms for doctor schedules</p></div><Button icon={Plus} onClick={() => setShowForm(value => !value)}>Add Room</Button></div>
    {showForm && <form className="room-form glass-card" onSubmit={submit}>
      <label>Room Number<input required maxLength="30" value={form.roomNumber} onChange={e => setForm({...form,roomNumber:e.target.value})} placeholder="C-204" /></label>
      <label>Room Name<input required maxLength="100" value={form.roomName} onChange={e => setForm({...form,roomName:e.target.value})} placeholder="Consultation Room 4" /></label>
      <label>Floor<input required maxLength="50" value={form.floor} onChange={e => setForm({...form,floor:e.target.value})} placeholder="Second Floor" /></label>
      <div className="room-form__actions room-form__wide"><Button type="submit" loading={saving} icon={CheckCircle}>Add and Confirm Room</Button><Button variant="outline" onClick={() => setShowForm(false)}>Cancel</Button></div>
    </form>}
    {error && <p className="room-message room-message--error">{error}</p>}{success && <p className="room-message room-message--success">{success}</p>}
    <div className="doctors__search-wrap room-search"><Search size={15}/><input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search room number, name, or floor" /></div>
    {loading ? (
      <div className="room-grid">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="skeleton" style={{ height: '260px', borderRadius: '18px' }} />
        ))}
      </div>
    ) : visible.length === 0 ? (
      <div className="doctor-empty">No rooms have been added.</div>
    ) : (
      <div className="room-grid stagger-children animate-fade-in">
        {visible.map(room => (
          <RoomCard
            key={room.roomId}
            room={room}
            onConfirm={confirmRoom}
          />
        ))}
      </div>
    )}
  </div>
}
