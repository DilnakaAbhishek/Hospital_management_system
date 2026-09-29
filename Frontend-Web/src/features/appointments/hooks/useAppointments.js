import { useCallback, useEffect, useState } from 'react'
import { appointmentApi } from '../services/appointmentApi'
import { patientApi, MOCK_PATIENTS } from '../../patients/services/patientApi'
import { getDoctorRegistrations } from '../../doctors/services/doctorApi'

export function useAppointments(filters) {
  const [appointments, setAppointments] = useState([])
  const [slots, setSlots] = useState([])
  const [doctors, setDoctors] = useState([])
  const [specializations, setSpecializations] = useState([])
  const [patients, setPatients] = useState([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)
  const [doctorError, setDoctorError] = useState(null)

  const load = useCallback(async ({ silent = false, background = false } = {}) => {
    if (!silent) {
      setLoading(true)
      setError(null)
      setDoctorError(null)
    }
    try {
      const [appointmentResult, slotResult] = await Promise.all([
        appointmentApi.getAll({ ...filters, pageSize: 50 }),
        appointmentApi.getSlots({ onlyAvailable: false }),
      ])
      const normalizedSlots = Array.isArray(slotResult) ? slotResult : []
      setAppointments(appointmentResult.data || appointmentResult)
      setSlots(normalizedSlots)
      if (silent) {
        if (!background) setError(null)
        return
      }

      let loadedDoctors = []
      try {
        const doctorResult = await appointmentApi.getDoctors()
        if (doctorResult?.length) {
          loadedDoctors = doctorResult
        } else {
          const registrationResult = await getDoctorRegistrations('Approved')
          loadedDoctors = mapDoctorRegistrations(registrationResult)
        }
      } catch {
        try {
          const registrationResult = await getDoctorRegistrations('Approved')
          loadedDoctors = mapDoctorRegistrations(registrationResult)
        } catch {
          setDoctorError('Unable to load approved doctors.')
        }
      }
      setDoctors(loadedDoctors)

      try {
        const specializationResult = await appointmentApi.getSpecializations()
        setSpecializations(normalizeSpecializations(specializationResult?.length ? specializationResult : loadedDoctors.map(doctor => doctor.specialty)))
      } catch {
        setSpecializations(normalizeSpecializations(loadedDoctors.map(doctor => doctor.specialty)))
      }

      try {
        const patientResult = await patientApi.getAll({ pageSize: 50 })
        setPatients(patientResult.data || patientResult || [])
      } catch {
        setPatients(MOCK_PATIENTS)
      }
    } catch (err) {
      if (silent) {
        if (!background) setError(err.response?.data?.message || err.message || 'Failed to refresh appointments')
        return
      }
      setError(err.response?.data?.message || err.message || 'Failed to load appointments')
      setAppointments([])
      setSlots([])
      setDoctors([])
      setSpecializations([])
      setPatients(MOCK_PATIENTS)
    } finally {
      if (!silent) setLoading(false)
    }
  }, [filters])

  useEffect(() => {
    load()
    const timer = setInterval(() => {
      if (!document.hidden) load({ silent: true, background: true })
    }, 30000)
    return () => clearInterval(timer)
  }, [load])

  const runMutation = async (action) => {
    setSaving(true)
    setError(null)
    try {
      const result = await action()
      await load({ silent: true })
      return result
    } catch (err) {
      setError(err.response?.data?.message || err.message || 'Failed to save appointment changes')
      throw err
    } finally {
      setSaving(false)
    }
  }

  return {
    appointments,
    slots,
    doctors,
    specializations,
    patients,
    loading,
    saving,
    error,
    doctorError,
    refetch: () => load({ silent: true }),
    createAppointment: (data) => runMutation(() => appointmentApi.create(data)),
    updateAppointment: (id, data) => runMutation(() => appointmentApi.update(id, data)),
    createSlot: (data) => runMutation(() => appointmentApi.createSlot(data)),
    updateSlot: (id, data) => runMutation(() => appointmentApi.updateSlot(id, data)),
    cancelSlot: (id, reason) => runMutation(() => appointmentApi.cancelSlot(id, reason)),
    cancelAppointment: (id, reason) => runMutation(() => appointmentApi.cancel(id, reason)),
    rescheduleAppointment: (id, slotId) => runMutation(() => appointmentApi.reschedule(id, slotId)),
  }
}

function mapDoctorRegistrations(doctors = []) {
  return doctors.map(doctor => ({
    doctorId: doctor.doctorId,
    doctorName: doctor.fullName ? `Dr. ${doctor.fullName}` : `Dr. ${[doctor.firstName, doctor.lastName].filter(Boolean).join(' ')}`.trim(),
    specialty: doctor.specialization || doctor.specialty || '',
  })).filter(doctor => doctor.doctorId && doctor.doctorName !== 'Dr.')
}

function normalizeSpecializations(values = []) {
  return Array.from(new Set(values.map(value => String(value || '').trim()).filter(Boolean))).sort()
}
