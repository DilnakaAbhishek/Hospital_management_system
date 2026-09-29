import { useCallback, useEffect, useMemo, useState } from 'react'
import { AlertTriangle, CheckCircle2, ClipboardCheck, FileClock, RefreshCw, Search, XCircle, ShieldCheck } from 'lucide-react'
import Button from '../../../components/Button'
import { triageApi } from '../services/triageApi'
import { useAuth } from '../../auth/AuthContext'
import './TriageReviewPage.css'

export default function TriageReviewPage() {
  const { user } = useAuth()
  const canMakeClinicalDecision = user?.role === 'Doctor'
  const [workflows, setWorkflows] = useState([])
  const [selected, setSelected] = useState(null)
  const [auditEvents, setAuditEvents] = useState([])
  const [loading, setLoading] = useState(true)
  const [acting, setActing] = useState(false)
  const [error, setError] = useState('')
  const [pendingDecision, setPendingDecision] = useState(null)
  const [reviewNote, setReviewNote] = useState('')
  const [success, setSuccess] = useState('')
  const [queueSearch, setQueueSearch] = useState('')

  const filteredWorkflows = useMemo(() => {
    const query = queueSearch.trim().toLowerCase()
    if (!query) return workflows
    return workflows.filter(workflow => [
      workflow.workflowId,
      workflow.triageLevel,
      workflow.uncertaintyState,
      ...(workflow.redFlags || []),
      ...(workflow.urgentFlags || []),
      ...(workflow.clinicalReviewFlags || []),
      ...(workflow.riskFactors || []),
    ].join(' ').toLowerCase().includes(query))
  }, [workflows, queueSearch])

  const loadQueue = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const queue = await triageApi.getPending()
      setWorkflows(queue)
      if (!selected && queue.length) await selectWorkflow(queue[0])
      if (!queue.length) { setSelected(null); setAuditEvents([]) }
    } catch (err) { setError(err.response?.data?.message || 'Unable to load the clinical review queue.') }
    finally { setLoading(false) }
  // selected intentionally only controls initial selection; refreshing must not discard reviewer context.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const selectWorkflow = async workflow => {
    setSelected(workflow); setAuditEvents([]); setError(''); setPendingDecision(null); setReviewNote('')
    try {
      const [details, events] = await Promise.all([triageApi.getWorkflow(workflow.workflowId), triageApi.getAuditEvents(workflow.workflowId)])
      setSelected(details); setAuditEvents(events)
    } catch (err) { setError(err.response?.data?.message || 'Unable to load workflow details.') }
  }

  useEffect(() => { loadQueue() }, [loadQueue])

  const review = async decision => {
    if (!selected || (decision === 'ClinicianResponse' && !reviewNote.trim())) return
    setActing(true); setError('')
    try {
      await triageApi.review(selected.workflowId, decision, decision === 'ClinicianResponse' ? undefined : reviewNote, decision === 'ClinicianResponse' ? reviewNote.trim() : undefined)
      setSuccess(`${decisionLabel(decision)} was recorded for workflow #${selected.workflowId}.`)
      setPendingDecision(null); setReviewNote('')
      await loadQueue()
    } catch (err) { setError(err.response?.data?.message || 'Unable to record the clinical decision.') }
    finally { setActing(false) }
  }

  return <div className="page-wrapper triage-review">
    <div className="page-header triage-review__heading">
      <div><h1 className="page-title">SafeTriage Clinical Review</h1><p className="page-subtitle">Review AI safety escalations and record the clinician’s independent decision</p></div>
      <Button variant="secondary" icon={RefreshCw} loading={loading} onClick={loadQueue}>Refresh queue</Button>
    </div>
    <div className="triage-review__notice"><ShieldCheck size={18}/><span><strong>Clinical action required:</strong> these records were escalated by SafeTriage. They are decision support, not diagnoses—review the evidence and decide independently.</span></div>
    {workflows.some(w => w.priorityLevel === 'Critical' || w.triageLevel === 'Emergency') && (
      <div className="triage-review__emergency-banner">
        <AlertTriangle size={20} />
        <div>
          <strong>🚨 IMMEDIATE EMERGENCY ESCALATION ACTIVE</strong>
          <div>One or more patients reported critical emergency symptoms. Immediate clinical evaluation or emergency dispatch required.</div>
        </div>
      </div>
    )}
    {!canMakeClinicalDecision && <div className="triage-review__readonly"><ShieldCheck size={18}/><span><strong>Read-only administrator view:</strong> only an authorised doctor can record a clinical triage decision.</span></div>}
    {error && <div className="triage-review__error"><XCircle size={17}/>{error}</div>}
    {success && <div className="triage-review__success"><CheckCircle2 size={17}/>{success}</div>}
    <div className="triage-review__layout">
      <section className="glass-card triage-review__queue">
        <div className="triage-review__section-title"><FileClock size={18}/><h3>Pending review</h3><span>{filteredWorkflows.length}{queueSearch.trim().length > 0 ? ` / ${workflows.length}` : ''}</span></div>
        <label className="triage-review__search"><Search size={15}/><input value={queueSearch} onChange={event => setQueueSearch(event.target.value)} placeholder="Search ID, urgency, or safety signal" aria-label="Search pending clinical reviews" />{queueSearch && <button type="button" onClick={() => setQueueSearch('')} aria-label="Clear search">×</button>}</label>
        {loading ? <p className="text-muted">Loading workflows…</p> : workflows.length === 0 ? <div className="triage-review__empty"><CheckCircle2 size={30}/><p>No workflows are awaiting clinical review.</p></div> : filteredWorkflows.length === 0 ? <div className="triage-review__empty"><Search size={28}/><p>No pending workflows match this search.</p></div> : filteredWorkflows.map(workflow => {
          const isEmergency = workflow.priorityLevel === 'Critical' || workflow.triageLevel === 'Emergency'
          return (
            <button key={workflow.workflowId} className={`triage-review__queue-item ${isEmergency ? 'triage-review__queue-item--critical' : ''} ${selected?.workflowId === workflow.workflowId ? 'triage-review__queue-item--active' : ''}`} onClick={() => selectWorkflow(workflow)}>
              <div>
                <strong>
                  {isEmergency && <span className="triage-review__badge-emergency">EMERGENCY</span>}
                  Workflow #{workflow.workflowId}
                </strong>
              </div>
              <span>{workflow.triageLevel} · {workflow.uncertaintyState}</span>
              {workflow.assignedDoctorName && <span className="triage-review__badge-assigned">👤 {workflow.assignedDoctorName}</span>}
              {workflow.targetSpecialty && !workflow.assignedDoctorName && <span className="triage-review__badge-assigned">🩺 {workflow.targetSpecialty}</span>}
              <small>{workflow.redFlags?.join(', ') || workflow.riskFactors?.join(', ') || 'Clinical review required'}</small>
            </button>
          )
        })}
      </section>
      <section className="glass-card triage-review__details">
        {!selected ? <div className="triage-review__empty"><ClipboardCheck size={34}/><p>Select a workflow to review its safety summary.</p></div> : <>
          <div className="triage-review__section-title"><AlertTriangle size={18}/><h3>{selected.priorityLevel === 'Critical' && <span className="triage-review__badge-emergency" style={{ fontSize: '.75rem', padding: '3px 8px', marginRight: 8 }}>CRITICAL EMERGENCY</span>}Workflow #{selected.workflowId}</h3><span className="triage-review__level">{humanize(selected.triageLevel)}</span></div>
          {selected.assignedDoctorName && <div style={{ margin: '-8px 0 12px', fontSize: '.78rem', color: 'var(--clr-primary)', fontWeight: 600 }}>Assigned: {selected.assignedDoctorName} {selected.targetSpecialty ? `(${selected.targetSpecialty})` : ''}</div>}
          <div className="triage-review__review-required"><AlertTriangle size={18}/><div><strong>Clinical decision required</strong><span>This workflow remains in the queue until you approve the SafeTriage suggestion or provide your own final suggestion.</span></div></div>
          <section className="triage-review__privacy-note"><ShieldCheck size={15}/><span><strong>Minimum necessary information:</strong> this view shows symptom information needed for clinical review. Contact details and account data are redacted.</span></section>
          <Info title="Patient’s submitted report" items={selected.originalComplaint ? [selected.originalComplaint] : []} fallback="The original patient report was not recorded." />
          <Info title="Collected information" items={(selected.requirements || []).filter(item => item.state === 'Answered').map(item => `${humanize(item.key.replaceAll('_', ' '))}: ${item.value}`)} />
          <Info title="Safety concern" items={uniqueItems([
            ...(selected.redFlags || []),
            ...(selected.urgentFlags || []),
            ...(selected.clinicalReviewFlags || []),
            ...(selected.riskFactors || []),
          ])} className="triage-review__info--safety" />
          <Info title="SafeTriage suggestion" items={selected.safeTriageSuggestion ? [selected.safeTriageSuggestion] : []} fallback="SafeTriage suggestion unavailable." />
          <details key={selected.workflowId} className="triage-review__audit"><summary><span>View technical details</span><small>Optional · no raw patient text</small></summary><p>Use this only when you need to verify the workflow controls, tool use, retries, or safe-failure handling.</p>{auditEvents.map(event => <div key={`${event.stage}-${event.createdAt}`}><strong>{event.stage}</strong><span>{event.eventType}</span>{event.tool && <small>Tool: {event.tool}</small>}{event.outcome && <small>{event.outcome}</small>}{event.validationPassed !== null && event.validationPassed !== undefined && <small>Validation: {event.validationPassed ? 'passed' : 'failed'}</small>}{event.retryCount > 0 && <small>Retries: {event.retryCount}</small>}{event.errorCode && <small className="triage-review__trace-error">Safe failure: {event.errorCode}</small>}<time>{event.durationMs !== null && event.durationMs !== undefined ? `${event.durationMs} ms · ` : ''}{new Date(event.createdAt).toLocaleString()}</time></div>)}</details>
          {canMakeClinicalDecision ? <section className="triage-review__decision"><h4>Record your clinical decision</h4><p>Choose the action that reflects your independent assessment. A confirmation panel will appear before anything is saved.</p><div className="triage-review__actions"><Button variant="primary" icon={CheckCircle2} loading={acting} disabled={!selected.safeTriageSuggestion} onClick={() => setPendingDecision('Approved')}>Approve SafeTriage suggestion</Button><Button variant="secondary" icon={ClipboardCheck} disabled={acting} onClick={() => setPendingDecision('ClinicianResponse')}>Provide my own suggestion</Button></div></section> : <section className="triage-review__decision"><h4>Clinical decision restricted</h4><p>Administrators can inspect this workflow and its audit trail, but only a doctor can approve or provide a final suggestion.</p></section>}
        </>}
      </section>
    </div>
    {pendingDecision && <ReviewDecisionDialog decision={pendingDecision} note={reviewNote} acting={acting} onNoteChange={setReviewNote} onCancel={() => { setPendingDecision(null); setReviewNote('') }} onConfirm={() => review(pendingDecision)} />}
  </div>
}

function Info({ title, items, fallback, className = '' }) {
  if (!items?.length && !fallback) return null
  return <section className={`triage-review__info ${className}`.trim()}><h4>{title}</h4>{items?.length ? <ul>{items.map(item => <li key={item}>{item}</li>)}</ul> : <p>{fallback}</p>}</section>
}

function ReviewDecisionDialog({ decision, note, acting, onNoteChange, onCancel, onConfirm }) {
  const content = decisionCopy(decision)
  const Icon = content.icon
  return <div className="triage-review__modal-backdrop" role="presentation"><section className="triage-review__modal" role="dialog" aria-modal="true" aria-labelledby="review-decision-title"><div className="triage-review__modal-icon"><Icon size={24}/></div><h2 id="review-decision-title">{content.title}</h2><p>{content.description}</p><label htmlFor="clinical-review-note">{decision === 'ClinicianResponse' ? 'Your final suggestion (required)' : 'Clinical note (optional)'}</label><textarea id="clinical-review-note" value={note} maxLength={decision === 'ClinicianResponse' ? 4000 : 1000} onChange={event => onNoteChange(event.target.value)} placeholder={content.placeholder} /><div className="triage-review__modal-actions"><Button variant="secondary" disabled={acting} onClick={onCancel}>Cancel</Button><Button variant={content.variant} icon={Icon} loading={acting} disabled={decision === 'ClinicianResponse' && !note.trim()} onClick={onConfirm}>{content.confirm}</Button></div></section></div>
}

function decisionCopy(decision) {
  if (decision === 'Approved') return { title: 'Approve SafeTriage recommendation?', description: 'You confirm that the proposed triage level is appropriate after your independent clinical review. This completes the review.', confirm: 'Confirm approval', placeholder: 'Reasoning or follow-up plan, if useful', variant: 'primary', icon: CheckCircle2 }
  return { title: 'Provide your own suggestion', description: 'This response will be saved as the final clinical result and shown to the patient.', confirm: 'Save final response', placeholder: 'Enter your final suggestion for the patient', variant: 'primary', icon: ClipboardCheck }
}

function decisionLabel(decision) { return decisionCopy(decision).title.replace('?', '') }
function humanize(value) { return value?.replace(/([a-z])([A-Z])/g, '$1 $2') || 'Unknown' }
function uniqueItems(items) { return [...new Set(items.filter(Boolean))] }
