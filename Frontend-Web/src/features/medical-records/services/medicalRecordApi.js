import apiClient from '../../../lib/apiClient'

export const medicalRecordApi = {
  getAll: async (params = {}) => {
    const res = await apiClient.get('/medicalrecord', { params })
    return res.data
  },

  getSummary: async () => {
    const res = await apiClient.get('/medicalrecord/summary')
    return res.data
  },

  getById: async (id) => {
    const res = await apiClient.get(`/medicalrecord/${id}`)
    return res.data
  },

  getByPatientId: async (patientId) => {
    const res = await apiClient.get(`/medicalrecord/patient/${patientId}`)
    return res.data
  },

  getPatientMedicalRecords: async (patientId) => {
    const res = await apiClient.get(`/medicalrecord/patient/${patientId}`)
    return res.data
  },

  getMyRecords: async () => {
    const res = await apiClient.get('/medicalrecord/me')
    return res.data
  },

  create: async (data) => {
    const res = await apiClient.post('/medicalrecord', data)
    return res.data
  },

  createMedicalRecord: async (data) => {
    const res = await apiClient.post('/medicalrecord', data)
    return res.data
  },

  update: async (id, data) => {
    const res = await apiClient.put(`/medicalrecord/${id}`, data)
    return res.data
  },

  delete: async (id) => {
    const res = await apiClient.delete(`/medicalrecord/${id}`)
    return res.data
  },

  addAttachment: async (id, attachmentData) => {
    const res = await apiClient.post(`/medicalrecord/${id}/attachments`, attachmentData)
    return res.data
  },

  uploadAttachment: async (id, file) => {
    const formData = new FormData()
    formData.append('file', file)
    const res = await apiClient.post(`/medicalrecord/${id}/upload-attachment`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    })
    return res.data
  },

  deleteAttachment: async (id, attachmentId) => {
    const res = await apiClient.delete(`/medicalrecord/${id}/attachments/${attachmentId}`)
    return res.data
  },
}
