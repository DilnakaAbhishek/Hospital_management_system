import apiClient from '../../../lib/apiClient'

const params = (startAt, endAt) => startAt && endAt ? { startAt, endAt } : {}

export const roomApi = {
  getAdminRooms: (startAt, endAt) => apiClient.get('/admin/rooms', { params: params(startAt, endAt) }).then(response => response.data),
  create: data => apiClient.post('/admin/rooms', data).then(response => response.data),
  update: (id, data) => apiClient.put(`/admin/rooms/${id}`, data).then(response => response.data),
  confirm: id => apiClient.post(`/admin/rooms/${id}/confirm`).then(response => response.data),
  getAvailable: (startAt, endAt, excludeScheduleId) => apiClient.get('/rooms/available', { params: { startAt, endAt, ...(excludeScheduleId ? { excludeScheduleId } : {}) } }).then(response => response.data),
}
