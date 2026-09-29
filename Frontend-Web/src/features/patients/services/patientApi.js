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

export const patientApi = {
  getAll: (params) => apiClient.get(`/patient?${toParams(params)}`).then(r => r.data),
  getSummary: () => apiClient.get('/patient/summary').then(r => r.data),
  getById: (id) => apiClient.get(`/patient/${id}`).then(r => r.data),
  getAppointmentHistory: (id) => apiClient.get(`/patient/${id}/appointments`).then(r => r.data),
  create: (data) => apiClient.post('/patient', data).then(r => r.data),
  update: (id, data) => apiClient.put(`/patient/${id}`, data).then(r => r.data),
  delete: (id) => apiClient.delete(`/patient/${id}`).then(r => r.data),
}

export const MOCK_PATIENTS = [
  {
    patientId: 1,
    firstName: 'Amal',
    lastName: 'Perera',
    dateOfBirth: '1985-03-14',
    gender: 'Male',
    nic: '850314123V',
    phoneNumber: '+94 77 234 5678',
    email: 'amal.perera@email.com',
    address: '45 Galle Rd, Colombo 03',
    bloodGroup: 'B+',
    emergencyContactName: 'Kamala Perera',
    emergencyContactPhone: '+94 71 234 5678',
    createdAt: '2024-01-10T08:00:00Z',
  },
  {
    patientId: 2,
    firstName: 'Nimesha',
    lastName: 'Silva',
    dateOfBirth: '1992-07-22',
    gender: 'Female',
    nic: '920722234V',
    phoneNumber: '+94 76 345 6789',
    email: 'nimesha.silva@email.com',
    address: '12 Kandy Rd, Peradeniya',
    bloodGroup: 'O+',
    emergencyContactName: 'Ruwan Silva',
    emergencyContactPhone: '+94 70 345 6789',
    createdAt: '2024-01-15T10:30:00Z',
  },
  {
    patientId: 3,
    firstName: 'Chaminda',
    lastName: 'Fernando',
    dateOfBirth: '1978-11-05',
    gender: 'Male',
    nic: '781105345V',
    phoneNumber: '+94 71 456 7890',
    email: 'chaminda.f@email.com',
    address: '78 High St, Galle',
    bloodGroup: 'A+',
    emergencyContactName: 'Dilani Fernando',
    emergencyContactPhone: '+94 72 456 7890',
    createdAt: '2024-02-01T09:00:00Z',
  },
  {
    patientId: 4,
    firstName: 'Priya',
    lastName: 'Rajapaksa',
    dateOfBirth: '1999-04-18',
    gender: 'Female',
    nic: '990418456V',
    phoneNumber: '+94 75 567 8901',
    email: 'priya.r@email.com',
    address: '23 Temple Rd, Nugegoda',
    bloodGroup: 'AB+',
    emergencyContactName: 'Rohan Rajapaksa',
    emergencyContactPhone: '+94 73 567 8901',
    createdAt: '2024-02-10T14:00:00Z',
  },
  {
    patientId: 5,
    firstName: 'Kasun',
    lastName: 'Wickramasinghe',
    dateOfBirth: '1965-09-30',
    gender: 'Male',
    nic: '650930567V',
    phoneNumber: '+94 77 678 9012',
    email: 'kasun.w@email.com',
    address: '9 Park Ave, Maharagama',
    bloodGroup: 'O-',
    emergencyContactName: 'Sunethra Wickramasinghe',
    emergencyContactPhone: '+94 76 678 9012',
    createdAt: '2024-03-05T11:00:00Z',
  },
  {
    patientId: 6,
    firstName: 'Thilini',
    lastName: 'Dissanayake',
    dateOfBirth: '2001-12-12',
    gender: 'Female',
    nic: '011212678V',
    phoneNumber: '+94 74 789 0123',
    email: 'thilini.d@email.com',
    address: '56 Lake Rd, Kandy',
    bloodGroup: 'A-',
    emergencyContactName: 'Upali Dissanayake',
    emergencyContactPhone: '+94 71 789 0123',
    createdAt: '2024-03-20T16:00:00Z',
  },
]
