import apiClient from '../../../lib/apiClient'

export const doctorScheduleApi = {
  getMine: () => apiClient.get('/doctor/schedules').then(response => response.data),
  create: data => apiClient.post('/doctor/schedules', data).then(response => response.data),
  update: (id, data) => apiClient.put(`/doctor/schedules/${id}`, data).then(response => response.data),
  cancel: id => apiClient.post(`/doctor/schedules/${id}/cancel`).then(response => response.data),
  delete: id => apiClient.delete(`/doctor/schedules/${id}`),
}
