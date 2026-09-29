import apiClient from '../../../lib/apiClient'

export const login = async credentials => (await apiClient.post('/auth/login', credentials)).data
export const registerDoctor = async data => (await apiClient.post('/auth/doctor-register', data)).data
