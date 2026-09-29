import { useState, useEffect, useCallback, useRef } from 'react'
import { patientApi } from '../services/patientApi'

const USE_MOCK = false

export function usePatients({ search = '', gender = '', bloodGroup = '', sortBy = 'createdAt', sortDirection = 'desc', page = 1, pageSize = 12 } = {}) {
  const [patients, setPatients] = useState([])
  const [pagination, setPagination] = useState({ page, pageSize, totalCount: 0, totalPages: 0 })
  const [summary, setSummary] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const latestRequest = useRef(0)

  const loadSummary = useCallback(async () => {
    try {
      setSummary(await patientApi.getSummary())
    } catch {
      // The patient list remains usable if the optional dashboard summary is unavailable.
    }
  }, [])

  const load = useCallback(async () => {
    const requestId = ++latestRequest.current
    setLoading(true)
    setError(null)
    try {
      if (USE_MOCK) {
        await new Promise(r => setTimeout(r, 600))
        setPatients(MOCK_PATIENTS)
      } else {
        const result = await patientApi.getAll({ search, gender, bloodGroup, sortBy, sortDirection, page, pageSize })
        if (requestId !== latestRequest.current) return
        setPatients(result.data || [])
        setPagination({
          page: result.page || page,
          pageSize: result.pageSize || pageSize,
          totalCount: result.totalCount || 0,
          totalPages: result.totalPages || 0,
        })
      }
    } catch (err) {
      if (requestId !== latestRequest.current) return
      setError(err.message || 'Failed to load patients')
      setPatients([])
    } finally {
      if (requestId === latestRequest.current) setLoading(false)
    }
  }, [bloodGroup, gender, page, pageSize, search, sortBy, sortDirection])

  useEffect(() => { load() }, [load])
  useEffect(() => { loadSummary() }, [loadSummary])

  const refetch = async () => {
    await Promise.all([load(), loadSummary()])
  }

  const createPatient = async (data) => {
    if (USE_MOCK) {
      const newP = { ...data, patientId: Date.now(), createdAt: new Date().toISOString() }
      setPatients(prev => [newP, ...prev])
      return newP
    }
    const created = await patientApi.create(data)
    await refetch()
    return created
  }

  const updatePatient = async (id, data) => {
    if (USE_MOCK) {
      setPatients(prev => prev.map(p => (p.patientId === id ? { ...p, ...data } : p)))
      return
    }
    await patientApi.update(id, data)
    await refetch()
  }

  const deletePatient = async (id) => {
    if (USE_MOCK) {
      setPatients(prev => prev.filter(p => p.patientId !== id))
      return
    }
    await patientApi.delete(id)
    await refetch()
  }

  return { patients, pagination, summary, loading, error, refetch, createPatient, updatePatient, deletePatient }
}
