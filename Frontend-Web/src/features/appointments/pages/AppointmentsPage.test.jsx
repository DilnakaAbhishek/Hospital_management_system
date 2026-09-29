import '@testing-library/jest-dom/vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import AppointmentsPage from './AppointmentsPage'
import { useAppointments } from '../hooks/useAppointments'
import { roomApi } from '../../rooms/services/roomApi'

vi.mock('../hooks/useAppointments')
vi.mock('../../rooms/services/roomApi', () => ({
  roomApi: {
    getAvailable: vi.fn(),
  },
}))

const appointment = {
  appointmentId: 42,
  doctorTimeSlotId: 7,
  appointmentNumber: 1,
  patientName: 'Ravi Patient',
  patientPhone: '0771234567',
  patientEmail: 'ravi@example.com',
  doctorName: 'Dr. Ada Lovelace',
  specialty: 'Cardiology',
  startAt: '2027-09-10T08:00:00Z',
  endAt: '2027-09-10T09:00:00Z',
  appointmentType: 'Consultation',
  status: 'Confirmed',
  consultationFee: 4500,
}

const slot = {
  doctorTimeSlotId: 7,
  doctorId: 1,
  doctorName: 'Dr. Ada Lovelace',
  specialty: 'Cardiology',
  startAt: '2027-09-10T08:00:00Z',
  endAt: '2027-09-10T09:00:00Z',
  capacity: 2,
  bookedCount: 1,
  availableCount: 1,
  bookedAppointmentNumbers: [1],
  nextAppointmentNumber: 2,
  consultationFee: 4500,
  isActive: true,
  roomId: 3,
  roomNumber: 'C-101',
  roomName: 'Consultation Room',
  floor: 'First',
}

const doctor = {
  doctorId: 1,
  doctorName: 'Dr. Ada Lovelace',
  specialty: 'Cardiology',
}

const patient = {
  patientId: 5,
  firstName: 'Ravi',
  lastName: 'Patient',
  fullName: 'Ravi Patient',
  phoneNumber: '0771234567',
  email: 'ravi@example.com',
  dateOfBirth: '1990-01-01T00:00:00Z',
}

const defaultHook = {
  appointments: [appointment],
  slots: [slot],
  doctors: [doctor],
  specializations: ['Cardiology'],
  patients: [patient],
  loading: false,
  saving: false,
  error: null,
  doctorError: null,
  refetch: vi.fn(),
  createAppointment: vi.fn(),
  updateAppointment: vi.fn(),
  createSlot: vi.fn(),
  updateSlot: vi.fn(),
  cancelSlot: vi.fn(),
  cancelAppointment: vi.fn(),
}

function mockAppointments(overrides = {}) {
  const state = {
    ...defaultHook,
    ...overrides,
  }
  useAppointments.mockReturnValue(state)
  return state
}

describe('AppointmentsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAppointments()
  })

  it('shows loading, empty, and error appointment states', () => {
    const { container, rerender } = render(<AppointmentsPage />)

    useAppointments.mockReturnValue({ ...defaultHook, loading: true, appointments: [], slots: [] })
    rerender(<AppointmentsPage />)
    expect(container.querySelectorAll('.skeleton').length).toBeGreaterThan(0)

    useAppointments.mockReturnValue({ ...defaultHook, appointments: [], slots: [], error: null })
    rerender(<AppointmentsPage />)
    expect(screen.getByText('No appointments match the current filters')).toBeInTheDocument()

    useAppointments.mockReturnValue({ ...defaultHook, appointments: [], slots: [], error: 'Failed to load appointments' })
    rerender(<AppointmentsPage />)
    expect(screen.getByText('Failed to load appointments')).toBeInTheDocument()
  })

  it('does not submit an incomplete create appointment form', () => {
    const state = mockAppointments()
    render(<AppointmentsPage />)

    fireEvent.click(screen.getByRole('button', { name: /new appointment/i }))
    fireEvent.click(screen.getByRole('button', { name: /create appointment/i }))

    expect(state.createAppointment).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog', { name: /create appointment/i })).toBeInTheDocument()
  })

  it('shows the session start for a later queue number', () => {
    mockAppointments({ appointments: [{ ...appointment, appointmentNumber: 2 }] })
    render(<AppointmentsPage />)
    const row = screen.getByText('Ravi Patient').closest('tr')
    const time = new Intl.DateTimeFormat('en-LK', { hour: '2-digit', minute: '2-digit' })
    expect(within(row).getByText('#2')).toBeInTheDocument()
    expect(within(row).getByText(time.format(new Date(slot.startAt)))).toBeInTheDocument()
    expect(within(row).queryByText(time.format(new Date('2026-09-10T08:30:00Z')))).not.toBeInTheDocument()
  })

  it('books by date and session without sending an appointment number', async () => {
    const state = mockAppointments()
    render(<AppointmentsPage />)
    fireEvent.click(screen.getByRole('button', { name: /new appointment/i }))
    fireEvent.change(screen.getByLabelText('Specialization'), { target: { value: 'Cardiology' } })
    fireEvent.change(screen.getByLabelText('Doctor Name'), { target: { value: doctor.doctorName } })
    fireEvent.change(screen.getByLabelText('Appointment date'), { target: { value: '2027-09-10' } })
    fireEvent.change(screen.getByLabelText('Appointment session'), { target: { value: '7' } })
    fireEvent.change(screen.getByPlaceholderText('Select existing patient or type a new name'), { target: { value: patient.fullName } })
    expect(screen.getByText('Next available appointment number: #2')).toBeInTheDocument()
    expect(screen.getByText('This number may change if someone books before you confirm.')).toBeInTheDocument()
    expect(screen.queryByText('Select Appointment No')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: /^create appointment$/i }))
    await waitFor(() => expect(state.createAppointment).toHaveBeenCalledTimes(1))
    const payload = state.createAppointment.mock.calls[0][0]
    expect(payload.doctorTimeSlotId).toBe(7)
    expect(payload).not.toHaveProperty('appointmentNumber')
    expect(payload).not.toHaveProperty('appointmentId')
  })

  it('clears the selected session when the date changes', () => {
    mockAppointments({ slots: [slot, { ...slot, doctorTimeSlotId: 8, startAt: '2027-09-11T08:00:00Z', endAt: '2027-09-11T09:00:00Z' }] })
    render(<AppointmentsPage />)
    fireEvent.click(screen.getByRole('button', { name: /new appointment/i }))
    fireEvent.change(screen.getByLabelText('Specialization'), { target: { value: 'Cardiology' } })
    fireEvent.change(screen.getByLabelText('Doctor Name'), { target: { value: doctor.doctorName } })
    fireEvent.change(screen.getByLabelText('Appointment date'), { target: { value: '2027-09-10' } })
    fireEvent.change(screen.getByLabelText('Appointment session'), { target: { value: '7' } })
    fireEvent.change(screen.getByLabelText('Appointment date'), { target: { value: '2027-09-11' } })
    expect(screen.getByLabelText('Appointment session')).toHaveValue('')
    expect(screen.queryByText(/Next available appointment number:/)).not.toBeInTheDocument()
  })

  it('requires a cancellation reason before calling the cancel API', () => {
    const state = mockAppointments()
    render(<AppointmentsPage />)

    fireEvent.click(screen.getByLabelText('Cancel appointment for Ravi Patient'))
    fireEvent.click(screen.getByRole('button', { name: /^cancel appointment$/i }))

    expect(state.cancelAppointment).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog', { name: /cancel appointment/i })).toBeInTheDocument()
  })

  it('shows status without a manual status dropdown and retains appointment actions', () => {
    render(<AppointmentsPage />)
    const row = screen.getByText('Ravi Patient').closest('tr')
    expect(within(row).queryByRole('combobox')).not.toBeInTheDocument()
    expect(within(row).getByText('Confirmed')).toBeInTheDocument()
    expect(within(row).getByLabelText('Edit appointment for Ravi Patient')).toBeEnabled()
    expect(within(row).getByLabelText('Cancel appointment for Ravi Patient')).toBeEnabled()
  })

  it('validates slot consultation fee precision after room selection', async () => {
    const state = mockAppointments()
    roomApi.getAvailable.mockResolvedValue([
      { roomId: 9, roomNumber: 'C-109', roomName: 'Procedure Room', floor: 'First' },
    ])
    render(<AppointmentsPage />)

    fireEvent.click(screen.getByRole('button', { name: /add slot/i }))
    fireEvent.change(screen.getByLabelText(/doctor name/i), { target: { value: '1' } })
    fireEvent.change(screen.getByLabelText(/^date$/i), { target: { value: '2027-09-12' } })
    fireEvent.change(screen.getByLabelText(/start time/i), { target: { value: '09:00' } })
    fireEvent.change(screen.getByLabelText(/end time/i), { target: { value: '10:00' } })
    fireEvent.click(screen.getByRole('button', { name: /check available rooms/i }))

    await waitFor(() => expect(roomApi.getAvailable).toHaveBeenCalled())
    fireEvent.change(screen.getByLabelText(/available room/i), { target: { value: '9' } })
    fireEvent.change(screen.getByLabelText(/consultation fee/i), { target: { value: '2500.555' } })
    fireEvent.submit(screen.getByRole('button', { name: /save slot/i }).closest('form'))

    expect(await screen.findByText('Consultation fee can contain a maximum of two decimal places.')).toBeInTheDocument()
    expect(state.createSlot).not.toHaveBeenCalled()
  })
})
