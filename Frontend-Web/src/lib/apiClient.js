import axios from 'axios'

const BASE_URL = import.meta.env.VITE_API_URL || '/api'

const apiClient = axios.create({
  baseURL: BASE_URL,
  timeout: 20000,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('hms_token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
      try {
        localStorage.setItem('hms_last_activity', Date.now().toString())
      } catch {
        // Ignore storage errors in restricted contexts
      }
    }
    return config
  },
  (error) => Promise.reject(error),
)

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // A failed login is expected to return 401. Do not reload the page before
    // LoginPage can show the server's "Invalid email or password" message.
    const isLoginRequest = error.config?.url?.endsWith('/auth/login')
    if (error.response?.status === 401 && !isLoginRequest) {
      localStorage.removeItem('hms_token')
      localStorage.removeItem('hms_user')
      localStorage.removeItem('hms_last_activity')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  },
)

export default apiClient
