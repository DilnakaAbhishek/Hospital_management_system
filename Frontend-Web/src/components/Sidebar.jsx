import { NavLink } from 'react-router-dom'
import {
  LayoutDashboard,
  Users,
  Stethoscope,
  Calendar,
  Settings,
  UserRound,
  LogOut,
  Activity,
  ShieldAlert,
  Building2,
  CalendarPlus,
  ClipboardList,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react'
import './Sidebar.css'
import { useAuth } from '../features/auth/AuthContext'

const NAV_ITEMS = [
  { label: 'Dashboard',       icon: LayoutDashboard, to: '/dashboard' },
  { label: 'Patients',        icon: Users,           to: '/patients' },
  { label: 'Doctors',         icon: Stethoscope,     to: '/doctors' },
  { label: 'Appointments',    icon: Calendar,        to: '/appointments' },
  { label: 'Medical Records', icon: ClipboardList,   to: '/medical-records' },
]

const BOTTOM_ITEMS = [
  { label: 'Settings',    icon: Settings,  to: '/settings' },
  { label: 'Logout',      icon: LogOut,    to: '/login',   danger: true },
]

export default function Sidebar({ collapsed, onToggle }) {
  const { user, signOut } = useAuth()
  const navItems = user?.role === 'Doctor'
    ? [
        { label: 'Dashboard',             icon: LayoutDashboard, to: '/doctor/dashboard' },
        { label: 'Medical Records',       icon: ClipboardList,   to: '/doctor/medical-records' },
        { label: 'Appointment Schedules', icon: CalendarPlus,    to: '/doctor/schedules' },
        { label: 'Triage Review',         icon: ShieldAlert,     to: '/doctor/triage-review' },
        { label: 'View Profile',          icon: UserRound,       to: '/doctor/profile' },
      ]
    : [...NAV_ITEMS, { label: 'Available Rooms', icon: Building2, to: '/admin/rooms' }, { label: 'Triage Review', icon: ShieldAlert, to: '/triage/review' }]
  const bottomItems = user?.role === 'Doctor' ? [] : BOTTOM_ITEMS.filter(item => item.label !== 'Logout')
  return (
    <aside className={`sidebar ${collapsed ? 'sidebar--collapsed' : ''}`}>
      <div className="sidebar__brand">
        <div className="sidebar__logo">
          <Activity size={22} strokeWidth={2.5} />
        </div>
        {!collapsed && (
          <span className="sidebar__brand-name">
            Medi<span>Core</span>
          </span>
        )}
      </div>

      <button
        id="sidebar-toggle"
        className="sidebar__toggle"
        onClick={onToggle}
        aria-label="Toggle sidebar"
      >
        {collapsed ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}
      </button>

      <nav className="sidebar__nav">
        {!collapsed && <p className="sidebar__section-label">MAIN MENU</p>}
        {navItems.map(({ label, icon: Icon, to }) => (
          <NavLink
            key={to}
            to={to}
            id={`nav-${label.toLowerCase()}`}
            className={({ isActive }) =>
              `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`
            }
          >
            <span className="sidebar__icon"><Icon size={18} strokeWidth={2} /></span>
            {!collapsed && <span className="sidebar__label">{label}</span>}
          </NavLink>
        ))}
      </nav>

      <div className="sidebar__bottom">
        {!collapsed && <p className="sidebar__section-label">ACCOUNT</p>}
        {bottomItems.map(({ label, icon: Icon, to, danger }) => (
          <NavLink
            key={to}
            to={to}
            id={`nav-${label.toLowerCase()}`}
            className={`sidebar__link ${danger ? 'sidebar__link--danger' : ''}`}
          >
            <span className="sidebar__icon"><Icon size={18} strokeWidth={2} /></span>
            {!collapsed && <span className="sidebar__label">{label}</span>}
          </NavLink>
        ))}
        <button type="button" onClick={signOut} className="sidebar__link sidebar__link--danger" id="nav-logout"><span className="sidebar__icon"><LogOut size={18}/></span>{!collapsed && <span className="sidebar__label">Logout</span>}</button>

        {!collapsed && (
          <div className="sidebar__user">
            <div className="sidebar__avatar">{user?.fullName?.[0] || 'U'}</div>
            <div>
              <div className="sidebar__user-name">{user?.role === 'Doctor' ? `Dr. ${user.fullName}` : user?.fullName}</div>
              <div className="sidebar__user-role">{user?.role}</div>
            </div>
          </div>
        )}
      </div>
    </aside>
  )
}
