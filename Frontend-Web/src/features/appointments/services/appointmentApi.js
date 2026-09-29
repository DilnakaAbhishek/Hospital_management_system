import apiClient from '../../../lib/apiClient'

const toParams = (params = {}) => {
  const query = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '' && value !== 'all') {
      query.append(key, value)
    }
  })
  return query.toString()
}

export const appointmentApi = {
  getAll: (params) => apiClient.get(`/appointment?${toParams(params)}`).then(r => r.data),
  create: (data) => apiClient.post('/appointment', data).then(r => r.data),
  update: (id, data) => apiClient.put(`/appointment/${id}`, data).then(r => r.data),
  cancel: (id, reason) => apiClient.post(`/appointment/${id}/cancel`, { reason }).then(r => r.data),
  reschedule: (id, doctorTimeSlotId) => apiClient.post(`/appointment/${id}/reschedule`, { doctorTimeSlotId }).then(r => r.data),
  getDoctors: (params) => apiClient.get(`/appointment/doctors?${toParams(params)}`).then(r => r.data),
  getSpecializations: () => apiClient.get('/appointment/specializations').then(r => r.data),
  getSlots: (params) => apiClient.get(`/appointment/slots?${toParams(params)}`).then(r => r.data),
  getAvailableSlots: (params) => apiClient.get(`/appointment/available-slots?${toParams(params)}`).then(r => r.data),
  createSlot: (data) => apiClient.post('/appointment/slots', data).then(r => r.data),
  updateSlot: (id, data) => apiClient.put(`/appointment/slots/${id}`, data).then(r => r.data),
  cancelSlot: (id, reason) => apiClient.post(`/appointment/slots/${id}/cancel`, { reason }).then(r => r.data),
}

export const MOCK_SLOTS = [
  {
    doctorTimeSlotId: 1,
    doctorName: 'Dr. Priyantha Jayawardena',
    specialty: 'General Medicine',
    startAt: '2026-08-08T09:00:00Z',
    endAt: '2026-08-08T11:00:00Z',
    capacity: 4,
    bookedCount: 2,
    availableCount: 2,
    isActive: true,
  },
  {
    doctorTimeSlotId: 2,
    doctorName: 'Dr. Chamari Gunaratne',
    specialty: 'Cardiology',
    startAt: '2026-08-08T14:00:00Z',
    endAt: '2026-08-08T16:00:00Z',
    capacity: 3,
    bookedCount: 1,
    availableCount: 2,
    isActive: true,
  },
]

export const MOCK_DOCTORS = Array.from(
  new Map(MOCK_SLOTS.map(slot => [slot.doctorName, slot.specialty]))
  .entries(),
  ([doctorName, specialty]) => ({ doctorName, specialty })
)

export const MOCK_APPOINTMENTS = [
  {
    appointmentId: 1,
    doctorTimeSlotId: 1,
    appointmentNumber: 1,
    patientName: 'Amal Perera',
    patientPhone: '+94 77 234 5678',
    patientEmail: 'amal.perera@email.com',
    doctorName: 'Dr. Priyantha Jayawardena',
    specialty: 'General Medicine',
    startAt: '2026-08-08T09:00:00Z',
    endAt: '2026-08-08T11:00:00Z',
    slotCapacity: 4,
    bookedCount: 2,
    appointmentType: 'Consultation',
    reason: 'Fever and cough',
    status: 'Confirmed',
    createdAt: '2026-08-08T04:30:00Z',
  },
  {
    appointmentId: 2,
    doctorTimeSlotId: 2,
    appointmentNumber: 1,
    patientName: 'Nimesha Silva',
    patientPhone: '+94 76 345 6789',
    patientEmail: 'nimesha.silva@email.com',
    doctorName: 'Dr. Chamari Gunaratne',
    specialty: 'Cardiology',
    startAt: '2026-08-08T14:00:00Z',
    endAt: '2026-08-08T16:00:00Z',
    slotCapacity: 3,
    bookedCount: 1,
    appointmentType: 'Follow-up',
    reason: 'Review ECG results',
    status: 'Confirmed',
    createdAt: '2026-08-08T05:00:00Z',
  },
]
