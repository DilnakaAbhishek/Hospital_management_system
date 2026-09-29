import './Table.css'

export default function Table({
  columns,
  data = [],
  loading = false,
  emptyMessage = 'No records found',
  emptyText,
  onRowClick,
  rowKey,
}) {
  const displayEmptyMessage = emptyText || emptyMessage

  const getRowKey = (row, i) => {
    if (typeof rowKey === 'function') {
      const k = rowKey(row)
      if (k != null) return k
    }
    if (typeof rowKey === 'string' && row[rowKey] != null) {
      return row[rowKey]
    }
    return (
      row.medicalRecordId ??
      row.appointmentId ??
      row.id ??
      row._id ??
      (row.patientId != null && !row.medicalRecordId ? row.patientId : null) ??
      i
    )
  }

  if (loading) {
    return (
      <div className="table-wrapper">
        <table className="table">
          <thead>
            <tr>
              {columns.map(col => (
                <th key={col.key} style={{ width: col.width }}>{col.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: 5 }).map((_, i) => (
              <tr key={i}>
                {columns.map(col => (
                  <td key={col.key}>
                    <div className="skeleton" style={{ height: '16px', borderRadius: '4px' }} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  }

  return (
    <div className="table-wrapper">
      <table className="table">
        <thead>
          <tr>
            {columns.map(col => (
              <th key={col.key} style={{ width: col.width }} className={col.align ? `text-${col.align}` : ''}>
                {col.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="table__empty">
                {displayEmptyMessage}
              </td>
            </tr>
          ) : (
            data.map((row, i) => (
              <tr
                key={getRowKey(row, i)}
                className={onRowClick ? 'table__row--clickable' : ''}
                onClick={() => onRowClick?.(row)}
              >
                {columns.map(col => (
                  <td key={col.key} className={col.align ? `text-${col.align}` : ''}>
                    {col.render ? col.render(row) : row[col.key]}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
