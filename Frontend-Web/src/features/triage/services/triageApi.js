import apiClient from '../../../lib/apiClient'

export const triageApi = {
  getPending: () => apiClient.get('/triage-workflows/clinical-review/pending').then(response => response.data),
  getWorkflow: id => apiClient.get(`/triage-workflows/${id}/clinical-review`).then(response => response.data),
  getAuditEvents: id => apiClient.get(`/triage-workflows/${id}/audit-events`).then(response => response.data),
  review: (id, decision, note, finalResponse) => apiClient.post(`/triage-workflows/${id}/review`, { decision, note, finalResponse }).then(response => response.data),
}
