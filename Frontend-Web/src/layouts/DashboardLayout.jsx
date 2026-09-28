import { useState, useEffect, useRef } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import Sidebar from '../components/Sidebar'
import {
  Bell,
  Sun,
  Moon,
  LogOut,
  User as UserIcon,
  Settings,
  CheckCheck,
  Calendar,
  Stethoscope,
  ShieldCheck,
  AlertTriangle,
  Trash2,
  X,
  Loader2,
} from 'lucide-react'
import './DashboardLayout.css'
import { useAuth } from '../features/auth/AuthContext'
import apiClient from '../lib/apiClient'

const PAGE_TITLES = {
  '/dashboard':       { title: 'Dashboard',       subtitle: 'Welcome back, Admin' },
  '/patients':        { title: 'Patients',        subtitle: 'Manage patient records' },
  '/doctors':         { title: 'Doctors',         subtitle: 'Medical staff directory' },
  '/appointments':    { title: 'Appointments',    subtitle: 'Schedule & manage visits' },
  '/medical-records': { title: 'Medical Records', subtitle: 'Manage patient EHR & clinical diagnostics' },
  '/triage':          { title: 'SafeTriage Review Queue', subtitle: 'Review and approve AI-triaged patient assessments' },
  '/triage/review':   { title: 'SafeTriage Review Queue', subtitle: 'Review and approve AI-triaged patient assessments' },
  '/settings':        { title: 'Settings',        subtitle: 'System configuration' },
  '/admin':           { title: 'Available Rooms', subtitle: 'Manage hospital room availability' },
  '/admin/rooms':     { title: 'Available Rooms', subtitle: 'Manage hospital room availability' },
}

const notificationReadKey = (userId) => `hms_read_notifications_${userId || 'current'}`
const notificationDismissedKey = (userId) => `hms_dismissed_notifications_${userId || 'current'}`

const getReadNotificationIds = (userId) => {
  try {
    return new Set(JSON.parse(localStorage.getItem(notificationReadKey(userId)) || '[]'))
  } catch {
    return new Set()
  }
}

const saveReadNotificationIds = (userId, ids) => {
  localStorage.setItem(notificationReadKey(userId), JSON.stringify([...ids]))
}

const getDismissedNotificationIds = (userId) => {
  try {
    return new Set(JSON.parse(localStorage.getItem(notificationDismissedKey(userId)) || '[]'))
  } catch {
    return new Set()
  }
}

const saveDismissedNotificationIds = (userId, ids) => {
  localStorage.setItem(notificationDismissedKey(userId), JSON.stringify([...ids]))
}

const formatRelativeTime = (rawTime, fallback) => {
  if (!rawTime) return fallback || 'Recently'
  const diffMs = Date.now() - rawTime
  if (diffMs < 0) return 'Just now'
  const diffSec = Math.floor(diffMs / 1000)
  if (diffSec < 60) return 'Just now'
  const diffMin = Math.floor(diffSec / 60)
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHours = Math.floor(diffMin / 60)
  if (diffHours < 24) return `${diffHours}h ago`
  const diffDays = Math.floor(diffHours / 24)
  if (diffDays === 1) return 'Yesterday'
  if (diffDays < 7) return `${diffDays}d ago`
  const d = new Date(rawTime)
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

export default function DashboardLayout() {
  const { user, signOut } = useAuth()
  const [collapsed, setCollapsed] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()

  const [showNotifications, setShowNotifications] = useState(false)
  const [showProfileMenu, setShowProfileMenu] = useState(false)
  const [notifications, setNotifications] = useState([])
  const [notificationsLoading, setNotificationsLoading] = useState(false)

  const notifRef = useRef(null)
  const profileRef = useRef(null)

  const unreadCount = notifications.filter((n) => !n.read).length

  const [theme, setTheme] = useState(() => {
    const saved = localStorage.getItem('hms_theme') || 'light'
    if (saved === 'dark') {
      document.body.classList.add('dark')
    } else {
      document.body.classList.remove('dark')
    }
    return saved
  })

  const toggleTheme = () => {
    setTheme((prev) => {
      const next = prev === 'light' ? 'dark' : 'light'
      localStorage.setItem('hms_theme', next)
      if (next === 'dark') {
        document.body.classList.add('dark')
      } else {
        document.body.classList.remove('dark')
      }
      return next
    })
  }

  // Close dropdowns on outside click
  useEffect(() => {
    const handleClickOutside = (e) => {
      if (notifRef.current && !notifRef.current.contains(e.target)) {
        setShowNotifications(false)
      }
      if (profileRef.current && !profileRef.current.contains(e.target)) {
        setShowProfileMenu(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  const handleClearAllNotifications = () => {
    const dismissedIds = getDismissedNotificationIds(user?.userId)
    notifications.forEach((notification) => dismissedIds.add(notification.id))
    saveDismissedNotificationIds(user?.userId, dismissedIds)
    setNotifications([])
  }

  const handleMarkAllRead = () => {
    const readIds = getReadNotificationIds(user?.userId)
    notifications.forEach((n) => readIds.add(n.id))
    saveReadNotificationIds(user?.userId, readIds)
    setNotifications((prev) => prev.map((n) => ({ ...n, read: true })))
  }

  const handleDismissOne = (e, notifId) => {
    e.stopPropagation()
    const dismissedIds = getDismissedNotificationIds(user?.userId)
    dismissedIds.add(notifId)
    saveDismissedNotificationIds(user?.userId, dismissedIds)
    setNotifications((prev) => prev.filter((n) => n.id !== notifId))
  }

  const handleNotificationClick = (notif) => {
    const readIds = getReadNotificationIds(user?.userId)
    readIds.add(notif.id)
    saveReadNotificationIds(user?.userId, readIds)
    setNotifications((prev) =>
      prev.map((n) => (n.id === notif.id ? { ...n, read: true } : n))
    )
    setShowNotifications(false)
    if (notif.targetUrl) {
      navigate(notif.targetUrl)
      if (notif.targetUrl.includes('#')) {
        const hash = notif.targetUrl.split('#')[1]
        let attempts = 0
        const intervalId = setInterval(() => {
          attempts++
          const el = document.getElementById(hash)
          if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' })
            clearInterval(intervalId)
          } else if (attempts >= 25) {
            clearInterval(intervalId)
          }
        }, 100)
      }
    }
  }

  const loadNotifications = async (background = false) => {
    if (!user) return
    if (!background && notifications.length === 0) {
      setNotificationsLoading(true)
    }
    const readIds = getReadNotificationIds(user.userId)
    const dismissedIds = getDismissedNotificationIds(user.userId)
    const items = []

    try {
      // 1. Triage / Clinical reviews (All authenticated users)
      try {
        const { data: triageData } = await apiClient.get('/triage-workflows/notifications')
        if (Array.isArray(triageData)) {
          triageData.forEach((item) => {
            const notifId = `triage:${item.triageWorkflowId || item.workflowId || item.id}`
            if (!dismissedIds.has(notifId)) {
              items.push({
                id: notifId,
                type: 'clinical',
                category: 'Clinical',
                title: item.isEmergency
                  ? 'Urgent Clinical Review'
                  : user.role === 'Patient'
                  ? 'Clinical Assessment Reviewed'
                  : 'Clinical Review Needed',
                message: item.message || 'Clinical decision support update available.',
                time: item.createdAt ? new Date(item.createdAt).toLocaleString() : 'Recently',
                rawTime: item.createdAt ? new Date(item.createdAt).getTime() : 0,
                read: readIds.has(notifId),
                targetUrl:
                  user.role === 'Doctor'
                    ? '/doctor/triage-review'
                    : user.role === 'Admin'
                    ? '/triage/review'
                    : '/dashboard',
                isEmergency: !!item.isEmergency,
              })
            }
          })
        }
      } catch {
        // Triage notifications optional
      }

      // 2. Appointment notifications
      try {
        const apptEndpoint =
          user.role?.toLowerCase() === 'patient'
            ? '/appointment/notifications'
            : '/appointment/staff-notifications'
        const { data: apptData } = await apiClient.get(apptEndpoint)
        if (Array.isArray(apptData)) {
          apptData.forEach((item) => {
            const notifId = `appointment:${item.appointmentNotificationId || item.id}`
            if (!dismissedIds.has(notifId)) {
              const msg = item.message || ''
              let apptTitle = 'Appointment Update'
              const lower = msg.toLowerCase()
              const isBooking = lower.includes('new appointment') || lower.includes('booked')

              const isReschedule = lower.includes('reschedul')

              // In clinical practice, routine appointment bookings should not generate bell notifications
              // for any user (Doctor, Admin, or Patient) to avoid alert fatigue; bookings are viewed in schedules.
              if (isBooking) {
                return
              }

              // Rescheduled notifications are strictly for the patient side (not for doctors or admins)
              if (user.role?.toLowerCase() !== 'patient' && isReschedule) {
                return
              }

              if (lower.includes('cancel')) {
                apptTitle = 'Appointment Cancelled'
              } else if (isReschedule) {
                apptTitle = 'Appointment Rescheduled'
              }

              items.push({
                id: notifId,
                type: 'appointment',
                category: 'Appointments',
                title: apptTitle,
                message: msg,
                time: item.createdAt ? new Date(item.createdAt).toLocaleString() : 'Recently',
                rawTime: item.createdAt ? new Date(item.createdAt).getTime() : 0,
                read: readIds.has(notifId),
                targetUrl: user.role === 'Doctor' ? '/doctor/dashboard#upcoming-appointments' : '/appointments',
                isEmergency: false,
              })
            }
          })
        }
      } catch {
        // Appointments notifications optional
      }

      // 3. Admin-specific doctor registration and deletion alerts
      if (user.role?.toLowerCase() === 'admin') {
        try {
          const { data: adminData } = await apiClient.get('/admin/doctor-registrations/notifications')
          if (Array.isArray(adminData)) {
            adminData.forEach((item) => {
              const notifId = `admin:${item.id}`
              if (!dismissedIds.has(notifId)) {
                items.push({
                  id: notifId,
                  type: item.type === 'doctor-deletion' ? 'deletion' : 'registration',
                  category: 'Admin',
                  title: item.title,
                  message: item.message,
                  time: item.time ? new Date(item.time).toLocaleString() : 'Recently',
                  rawTime: item.time ? new Date(item.time).getTime() : 0,
                  read: readIds.has(notifId),
                  targetUrl: item.targetUrl || '/doctors',
                  isEmergency: item.priority === 'high',
                })
              }
            })
          }
        } catch {
          // Admin registration alerts optional
        }
      }

      // 4. Medical record notifications
      try {
        const mrEndpoint =
          user.role?.toLowerCase() === 'patient'
            ? '/medicalrecord/notifications/patient'
            : '/medicalrecord/notifications/staff'
        const { data: mrData } = await apiClient.get(mrEndpoint)
        if (Array.isArray(mrData)) {
          mrData.forEach((item) => {
            const notifId = `medicalrecord:${item.medicalRecordNotificationId}`
            if (!dismissedIds.has(notifId)) {
              const isAttachment = item.eventType === 'AttachmentUploaded'
              items.push({
                id: notifId,
                type: 'medical-record',
                category: 'Medical Records',
                title: isAttachment
                  ? user.role?.toLowerCase() === 'patient'
                    ? 'Document Added to Your Record'
                    : 'Patient Uploaded a Document'
                  : 'New Medical Record Available',
                message: item.message || 'A medical record update is available.',
                time: item.createdAt ? new Date(item.createdAt).toLocaleString() : 'Recently',
                rawTime: item.createdAt ? new Date(item.createdAt).getTime() : 0,
                read: readIds.has(notifId),
                targetUrl:
                  user.role === 'Doctor'
                    ? '/doctor/medical-records'
                    : user.role === 'Admin'
                    ? '/medical-records'
                    : '/medical-records',
                isEmergency: false,
              })
            }
          })
        }
      } catch {
        // Medical record notifications optional
      }

      items.sort((a, b) => b.rawTime - a.rawTime)
      setNotifications(items)
    } finally {
      setNotificationsLoading(false)
    }
  }

  useEffect(() => {
    if (!user) return undefined

    loadNotifications()
    const refreshNotifications = () => {
      if (document.visibilityState === 'visible') loadNotifications(true)
    }
    const intervalId = window.setInterval(refreshNotifications, 30000)
    document.addEventListener('visibilitychange', refreshNotifications)

    return () => {
      window.clearInterval(intervalId)
      document.removeEventListener('visibilitychange', refreshNotifications)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.userId, user?.role])

  const handleSignOut = () => {
    signOut()
    navigate('/login')
  }

  const pathKey = '/' + location.pathname.split('/')[1]
  const doctorMeta =
    location.pathname === '/doctor/profile'
      ? { title: 'Profile', subtitle: 'Manage your professional profile' }
      : location.pathname === '/doctor/triage-review'
      ? { title: 'SafeTriage Review', subtitle: 'Clinical decision-support oversight' }
      : location.pathname === '/doctor/schedules'
      ? { title: 'Appointment Schedules', subtitle: 'Reserve rooms and manage your schedules' }
      : location.pathname === '/doctor/medical-records'
      ? { title: 'Medical Records', subtitle: 'Clinical consultations and diagnoses' }
      : { title: 'Doctor Dashboard', subtitle: `Welcome Dr. ${user?.fullName || ''}` }

  const pageMeta =
    user?.role === 'Doctor'
      ? doctorMeta
      : PAGE_TITLES[pathKey] || { title: 'MediCore', subtitle: '' }

  return (
    <div className={`layout ${collapsed ? 'layout--collapsed' : ''}`}>
      <Sidebar collapsed={collapsed} onToggle={() => setCollapsed((c) => !c)} />

      <div className="layout__content">
        <header className="topbar">
          <div className="topbar__left">
            <div className="topbar__page-info">
              <h1 className="topbar__title">{pageMeta.title}</h1>
              <p className="topbar__subtitle">{pageMeta.subtitle}</p>
            </div>
          </div>

          <div className="topbar__right">
            {/* Theme Toggle */}
            <button
              className="topbar__icon-btn"
              onClick={toggleTheme}
              aria-label="Toggle theme"
              id="theme-toggle-btn"
              title="Toggle theme"
            >
              {theme === 'light' ? <Moon size={18} /> : <Sun size={18} />}
            </button>

            {/* Notifications Popover */}
            <div className="topbar__dropdown-wrap" ref={notifRef}>
              <button
                className="topbar__icon-btn"
                id="notifications-btn"
                aria-label="Notifications"
                onClick={() => {
                  setShowNotifications((prev) => {
                    if (!prev) loadNotifications(notifications.length > 0)
                    return !prev
                  })
                  setShowProfileMenu(false)
                }}
                title="Notifications"
              >
                <Bell size={18} />
                {unreadCount > 0 && (
                  <span className="topbar__badge">{unreadCount}</span>
                )}
              </button>

              {showNotifications && (
                <div className="topbar__popover notifications-popover">
                  <div className="notifications-header">
                    <div className="notifications-header-left">
                      <span className="notifications-title">Notifications</span>
                      {unreadCount > 0 ? (
                        <span className="notifications-badge-count">
                          <span className="notifications-badge-dot" />
                          {unreadCount} new
                        </span>
                      ) : notifications.length > 0 ? (
                        <span className="notifications-badge-all">
                          {notifications.length}
                        </span>
                      ) : null}
                      {notificationsLoading && (
                        <Loader2 size={13} className="animate-spin text-primary opacity-70" />
                      )}
                    </div>
                    <div className="notifications-header-actions">
                      {unreadCount > 0 && (
                        <button
                          type="button"
                          className="notifications-action-btn"
                          title="Mark all as read"
                          onClick={handleMarkAllRead}
                        >
                          <CheckCheck size={14} />
                          <span>Mark all read</span>
                        </button>
                      )}
                      {notifications.length > 0 && (
                        <button
                          type="button"
                          className="notifications-action-btn notifications-action-btn--clear"
                          title="Clear all notifications"
                          onClick={handleClearAllNotifications}
                        >
                          <Trash2 size={13} />
                          <span>Clear</span>
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="notifications-list">
                    {notificationsLoading && notifications.length === 0 ? (
                      <div className="notifications-skeleton-list">
                        {[1, 2, 3].map((i) => (
                          <div key={i} className="notification-skeleton-item">
                            <div className="skeleton-icon" />
                            <div className="skeleton-lines">
                              <div className="skeleton-line skeleton-line--title" />
                              <div className="skeleton-line skeleton-line--desc" />
                              <div className="skeleton-line skeleton-line--time" />
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : notifications.length === 0 ? (
                      <div className="notification-empty">
                        <div className="notification-empty-icon">
                          <Bell size={24} />
                        </div>
                        <div className="notification-empty-title">All caught up!</div>
                        <div className="notification-empty-desc">
                          No notifications right now. Alerts and updates will appear here in real-time.
                        </div>
                      </div>
                    ) : (
                      notifications.map((notif) => (
                        <div
                          key={notif.id}
                          className={`notification-item ${
                            !notif.read ? 'notification-item--unread' : 'notification-item--read'
                          } ${notif.isEmergency ? 'notification-item--emergency' : ''}`}
                          onClick={() => handleNotificationClick(notif)}
                          role="button"
                          tabIndex={0}
                        >
                          <div
                            className={`notification-icon-wrap ${
                              notif.isEmergency ? 'notification-icon-wrap--emergency' : ''
                            }`}
                          >
                            {notif.isEmergency ? (
                              <AlertTriangle size={15} className="notif-icon notif-icon--danger" />
                            ) : notif.category === 'Appointments' ? (
                              <Calendar size={15} className="notif-icon notif-icon--appt" />
                            ) : notif.category === 'Clinical' ? (
                              <Stethoscope size={15} className="notif-icon notif-icon--clinical" />
                            ) : notif.type === 'deletion' ? (
                              <AlertTriangle size={15} className="notif-icon notif-icon--danger" />
                            ) : (
                              <ShieldCheck size={15} className="notif-icon notif-icon--admin" />
                            )}
                          </div>

                          <div className="notification-body">
                            <div className="notification-top-row">
                              <span className="notification-title">{notif.title}</span>
                              <div className="notification-meta-tags">
                                {notif.isEmergency && (
                                  <span className="notification-pill notification-pill--emergency">Urgent</span>
                                )}
                                {notif.category && (
                                  <span className={`notification-pill notification-pill--${notif.category.toLowerCase()}`}>
                                    {notif.category}
                                  </span>
                                )}
                              </div>
                            </div>
                            {notif.message && (
                              <div className="notification-msg">{notif.message}</div>
                            )}
                            <div className="notification-time-row">
                              <span className="notification-time">
                                {formatRelativeTime(notif.rawTime, notif.time)}
                              </span>
                            </div>
                          </div>

                          <div className="notification-actions-wrap">
                            {!notif.read && (
                              <span className="notification-unread-dot" title="Unread notification" />
                            )}
                            <button
                              type="button"
                              className="notification-dismiss-btn"
                              title="Dismiss notification"
                              onClick={(e) => handleDismissOne(e, notif.id)}
                              aria-label="Dismiss"
                            >
                              <X size={13} />
                            </button>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              )}
            </div>

            {/* User Profile Popover */}
            <div className="topbar__dropdown-wrap" ref={profileRef}>
              <button
                className="topbar__avatar"
                id="profile-btn"
                aria-label="Profile"
                onClick={() => {
                  setShowProfileMenu((prev) => !prev)
                  setShowNotifications(false)
                }}
                title="Account Menu"
              >
                {user?.fullName?.[0] || 'U'}
              </button>

              {showProfileMenu && (
                <div className="topbar__popover profile-popover">
                  <div className="profile-header">
                    <div className="profile-name">{user?.fullName || 'MediCore User'}</div>
                    <div className="profile-email">{user?.email || 'user@medicore.com'}</div>
                    <span className="profile-role-badge">{user?.role || 'Staff'}</span>
                  </div>

                  {user?.role === 'Doctor' && (
                    <button
                      type="button"
                      className="profile-menu-item"
                      onClick={() => {
                        setShowProfileMenu(false)
                        navigate('/doctor/profile')
                      }}
                    >
                      <UserIcon size={16} /> My Doctor Profile
                    </button>
                  )}

                  <button
                    type="button"
                    className="profile-menu-item"
                    onClick={() => {
                      toggleTheme()
                    }}
                  >
                    {theme === 'light' ? <Moon size={16} /> : <Sun size={16} />} Switch to {theme === 'light' ? 'Dark' : 'Light'} Mode
                  </button>

                  <button
                    type="button"
                    className="profile-menu-item profile-menu-item--danger"
                    onClick={handleSignOut}
                  >
                    <LogOut size={16} /> Sign Out
                  </button>
                </div>
              )}
            </div>
          </div>
        </header>

        <main className="layout__main">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
