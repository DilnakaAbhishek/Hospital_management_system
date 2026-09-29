import apiClient from '../../../lib/apiClient'

export const dashboardApi = {
  getAdminStats: () => apiClient.get('/dashboard/admin').then(response => response.data),
}
