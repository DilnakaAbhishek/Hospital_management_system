import { createContext, useContext, useEffect, useMemo, useState } from 'react'

const AuthContext = createContext(null)
const tokenKey = 'hms_token'
const userKey = 'hms_user'
const lastActivityKey = 'hms_last_activity'

// Standard clinical inactivity timeout (15 minutes of idle time without user interaction)
export const INACTIVITY_TIMEOUT_MS = 15 * 60 * 1000

const clearStoredSession = () => {
  localStorage.removeItem(tokenKey)
  localStorage.removeItem(userKey)
  localStorage.removeItem(lastActivityKey)
}

const recordActivity = () => {
  try {
    localStorage.setItem(lastActivityKey, Date.now().toString())
  } catch {
    // Ignore storage errors in restricted contexts
  }
}

const getLastActivity = () => {
  try {
    const raw = localStorage.getItem(lastActivityKey)
    return raw ? parseInt(raw, 10) : 0
  } catch {
    return 0
  }
}

const tokenExpiresAt = (token) => {
  try {
    const payload = token.split('.')[1]
    if (!payload) return null
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    const paddedBase64 = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const decoded = new TextDecoder().decode(
      Uint8Array.from(atob(paddedBase64), (character) => character.charCodeAt(0)),
    )
    const { exp } = JSON.parse(decoded)
    return typeof exp === 'number' ? exp * 1000 : null
  } catch {
    return null
  }
}

const readUser = () => {
  const token = localStorage.getItem(tokenKey)
  const expiresAt = tokenExpiresAt(token || '')
  if (!token || !expiresAt || expiresAt <= Date.now()) {
    clearStoredSession()
    return null
  }

  const lastActivity = getLastActivity()
  if (lastActivity && Date.now() - lastActivity >= INACTIVITY_TIMEOUT_MS) {
    clearStoredSession()
    return null
  }

  try {
    return JSON.parse(localStorage.getItem(userKey))
  } catch {
    clearStoredSession()
    return null
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(readUser)

  const signIn = response => {
    const next = { userId: response.userId, doctorId: response.doctorId, fullName: response.fullName, email: response.email, role: response.role }
    localStorage.setItem(tokenKey, response.token)
    localStorage.setItem(userKey, JSON.stringify(next))
    recordActivity()
    sessionStorage.removeItem('hms_logout_reason')
    setUser(next)
    return next
  }

  const updateUser = changes => setUser(current => {
    const next = { ...current, ...changes }
    localStorage.setItem(userKey, JSON.stringify(next))
    return next
  })

  const signOut = () => {
    clearStoredSession()
    sessionStorage.removeItem('hms_logout_reason')
    setUser(null)
  }

  useEffect(() => {
    if (!user) return undefined

    // Initialize activity timestamp if not present
    if (!getLastActivity()) {
      recordActivity()
    }

    // Throttle user activity events so localStorage is updated at most once every 3 seconds
    let lastThrottledTime = 0
    const onUserActivity = () => {
      const now = Date.now()
      if (now - lastThrottledTime > 3000) {
        lastThrottledTime = now
        recordActivity()
      }
    }

    const activityEvents = ['mousedown', 'mousemove', 'keydown', 'scroll', 'touchstart', 'click']
    activityEvents.forEach((evt) => {
      window.addEventListener(evt, onUserActivity, { passive: true })
    })

    // Periodic watchdog: checks idle inactivity and token expiry every 10 seconds
    const checkSession = () => {
      const now = Date.now()

      // 1. Inactivity (idle) check: user inactive for >= 15 minutes
      const lastActive = getLastActivity()
      if (lastActive && now - lastActive >= INACTIVITY_TIMEOUT_MS) {
        sessionStorage.setItem('hms_logout_reason', 'inactive')
        clearStoredSession()
        setUser(null)
        return
      }

      // 2. Absolute token expiry check
      const expiresAt = tokenExpiresAt(localStorage.getItem(tokenKey) || '')
      if (expiresAt && expiresAt <= now) {
        clearStoredSession()
        setUser(null)
      }
    }

    const intervalId = window.setInterval(checkSession, 10000)

    // Check immediately when user switches back to this tab
    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        checkSession()
      }
    }
    document.addEventListener('visibilitychange', onVisibilityChange)

    return () => {
      activityEvents.forEach((evt) => {
        window.removeEventListener(evt, onUserActivity)
      })
      window.clearInterval(intervalId)
      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
  }, [user])

  const value = useMemo(() => ({ user, signIn, signOut, updateUser }), [user])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const useAuth = () => useContext(AuthContext)
