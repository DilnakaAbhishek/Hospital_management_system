import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import TriageReviewPage from './TriageReviewPage'
import { triageApi } from '../services/triageApi'

vi.mock('../services/triageApi', () => ({ triageApi: {
  getPending: vi.fn(), getWorkflow: vi.fn(), getAuditEvents: vi.fn(), review: vi.fn(),
} }))
vi.mock('../../auth/AuthContext', () => ({ useAuth: () => ({ user: { role: 'Doctor' } }) }))

const workflow = {
  workflowId: 42, triageLevel: 'ClinicalReview', originalComplaint: 'My original complaint',
  safeTriageSuggestion: 'Saved SafeTriage suggestion', requirements: [
    { key: 'onset', state: 'Answered', value: 'last Wednesday' },
    { key: 'severity_score', state: 'Declined', value: null },
    { key: 'progression', state: 'Unknown', value: null },
    { key: 'other_context', state: 'NotApplicable', value: null },
  ],
}

beforeEach(() => {
  vi.clearAllMocks()
  triageApi.getPending.mockResolvedValue([workflow])
  triageApi.getWorkflow.mockResolvedValue(workflow)
  triageApi.getAuditEvents.mockResolvedValue([{ stage: 'Extraction', eventType: 'Completed', createdAt: '2026-09-19T12:00:00Z' }])
  triageApi.review.mockResolvedValue({})
})
afterEach(cleanup)

it('shows accumulated information and keeps technical details collapsed', async () => {
  render(<TriageReviewPage />)
  expect(await screen.findByText('My original complaint')).toBeInTheDocument()
  expect(screen.getByText('onset: last Wednesday')).toBeInTheDocument()
  expect(screen.queryByText('Unavailable information')).not.toBeInTheDocument()
  expect(screen.queryByText('severity score: Declined')).not.toBeInTheDocument()
  expect(screen.queryByText('progression: Unknown')).not.toBeInTheDocument()
  expect(screen.queryByText('other context: Not Applicable')).not.toBeInTheDocument()
  expect(screen.queryByText('Emergency red flags')).not.toBeInTheDocument()
  expect(screen.queryByText('Urgent warning signs')).not.toBeInTheDocument()
  expect(screen.queryByText('Serious or high-risk context')).not.toBeInTheDocument()
  expect(screen.queryByText('Grounded clinical facts')).not.toBeInTheDocument()
  expect(screen.queryByText('Decision basis')).not.toBeInTheDocument()
  expect(screen.queryByText('Information limitations')).not.toBeInTheDocument()
  const summary = screen.getByText('View technical details')
  expect(summary.closest('details')).not.toHaveAttribute('open')
  expect(screen.queryByRole('button', { name: 'Request more information' })).not.toBeInTheDocument()
})

it('combines actual safety signals into one concern section', async () => {
  triageApi.getPending.mockResolvedValue([{ ...workflow, redFlags: ['High fever'] }])
  triageApi.getWorkflow.mockResolvedValue({ ...workflow, redFlags: ['High fever'], urgentFlags: ['Rapid worsening'] })
  render(<TriageReviewPage />)
  expect(await screen.findByText('Safety concern')).toBeInTheDocument()
  expect(screen.getAllByText('High fever')).toHaveLength(2)
  expect(screen.getByText('Rapid worsening')).toBeInTheDocument()
  expect(screen.queryByText('Emergency red flags')).not.toBeInTheDocument()
  expect(screen.queryByText('Urgent warning signs')).not.toBeInTheDocument()
})

it('requires a clinician response and submits it as the final response', async () => {
  render(<TriageReviewPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Provide my own suggestion' }))
  const save = screen.getByRole('button', { name: 'Save final response' })
  expect(save).toBeDisabled()
  fireEvent.change(screen.getByRole('textbox', { name: 'Your final suggestion (required)' }), { target: { value: '   ' } })
  expect(save).toBeDisabled()
  fireEvent.change(screen.getByRole('textbox', { name: 'Your final suggestion (required)' }), { target: { value: 'Please follow the agreed care plan.' } })
  fireEvent.click(save)
  await waitFor(() => expect(triageApi.review).toHaveBeenCalledWith(42, 'ClinicianResponse', undefined, 'Please follow the agreed care plan.'))
})

it('approves the saved SafeTriage suggestion', async () => {
  render(<TriageReviewPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Approve SafeTriage suggestion' }))
  fireEvent.click(screen.getByRole('button', { name: 'Confirm approval' }))
  await waitFor(() => expect(triageApi.review).toHaveBeenCalledWith(42, 'Approved', '', undefined))
})
