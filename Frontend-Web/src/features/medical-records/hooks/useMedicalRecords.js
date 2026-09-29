import { useState, useEffect, useCallback } from 'react'
import { medicalRecordApi } from '../services/medicalRecordApi'

function extractErrorMessage(err, fallback) {
  if (err?.response?.data) {
    const data = err.response.data
    if (typeof data === 'string') return data
    if (data.message) return data.message
    if (data.errors && typeof data.errors === 'object') {
      const messages = Object.values(data.errors).flat().filter(Boolean)
      if (messages.length > 0) return messages.join('; ')
    }
    if (data.title) return data.title
  }
  return err?.message || fallback
}

export function useMedicalRecords({
  search = '',
  recordType = '',
  status = '',
  doctorId = undefined,
  fromDate = '',
  toDate = '',
  sortBy = 'date',
  sortDirection = 'desc',
  page = 1,
  pageSize = 10,
} = {}) {
  const [records, setRecords] = useState([])
  const [summary, setSummary] = useState(null)
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [recordsRes, summaryRes] = await Promise.all([
        medicalRecordApi.getAll({
          search: search || undefined,
          recordType: recordType || undefined,
          status: status || undefined,
          doctorId: doctorId || undefined,
          fromDate: fromDate || undefined,
          toDate: toDate || undefined,
          sortBy: sortBy || 'date',
          sortDirection: sortDirection || 'desc',
          page,
          pageSize,
        }),
        medicalRecordApi.getSummary().catch(() => null),
      ])

      setRecords(recordsRes.data || [])
      setTotalCount(recordsRes.totalCount || 0)
      setTotalPages(recordsRes.totalPages || 1)
      if (summaryRes) setSummary(summaryRes)
    } catch (err) {
      console.error('Failed to fetch medical records:', err)
      setError(extractErrorMessage(err, 'Failed to load medical records'))
    } finally {
      setLoading(false)
    }
  }, [search, recordType, status, doctorId, fromDate, toDate, sortBy, sortDirection, page, pageSize])

  useEffect(() => {
    load()
  }, [load])

  const createRecord = async (data) => {
    setSaving(true)
    try {
      const res = await medicalRecordApi.create(data)
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: extractErrorMessage(err, 'Failed to create record') }
    } finally {
      setSaving(false)
    }
  }

  const updateRecord = async (id, data) => {
    setSaving(true)
    try {
      const res = await medicalRecordApi.update(id, data)
      setRecords((prev) =>
        prev.map((r) => (r.medicalRecordId === id ? { ...r, ...data } : r))
      )
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: extractErrorMessage(err, 'Failed to update record') }
    } finally {
      setSaving(false)
    }
  }

  const deleteRecord = async (id) => {
    setSaving(true)
    try {
      await medicalRecordApi.delete(id)
      setRecords((prev) => prev.filter((r) => r.medicalRecordId !== id))
      setTotalCount((prev) => Math.max(0, prev - 1))
      await load()
      return { success: true }
    } catch (err) {
      return { success: false, error: extractErrorMessage(err, 'Failed to delete record') }
    } finally {
      setSaving(false)
    }
  }

  const addAttachment = async (recordId, attachmentData) => {
    try {
      const res = await medicalRecordApi.addAttachment(recordId, attachmentData)
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: extractErrorMessage(err, 'Failed to add attachment') }
    }
  }

  const uploadAttachment = async (recordId, file) => {
    try {
      const res = await medicalRecordApi.uploadAttachment(recordId, file)
      await load()
      return { success: true, data: res }
    } catch (err) {
      return { success: false, error: extractErrorMessage(err, 'Failed to upload attachment') }
    }
  }

  const deleteAttachment = async (recordId, attachmentId) => {
    try {
      await medicalRecordApi.deleteAttachment(recordId, attachmentId)
      await load()
      return { success: true }
    } catch (err) {
      return { success: false, error: extractErrorMessage(err, 'Failed to delete attachment') }
    }
  }

  return {
    records,
    summary,
    totalCount,
    totalPages,
    loading,
    saving,
    error,
    refetch: load,
    createRecord,
    updateRecord,
    deleteRecord,
    addAttachment,
    uploadAttachment,
    deleteAttachment,
  }
}
