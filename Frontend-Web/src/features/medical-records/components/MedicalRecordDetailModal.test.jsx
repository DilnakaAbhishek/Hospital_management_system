import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import MedicalRecordDetailModal, { getFullAttachmentUrl } from './MedicalRecordDetailModal'

describe('MedicalRecordDetailModal & Attachment Viewer', () => {
  afterEach(cleanup)

  const sampleRecord = {
    medicalRecordId: 12,
    patientId: 5,
    patientName: 'nimal perera',
    patientEmail: 'nimalperera@gmail.com',
    recordDate: '2026-09-22T00:00:00Z',
    recordType: 'Prescription',
    diagnosis: 'Medication',
    symptoms: 'inhaler',
    treatmentPlan: 'Uploaded for clinical review and archival.',
    status: 'Finalized',
    attachments: [
      {
        attachmentId: 6,
        fileName: 'scaled_WhatsApp Image 2026-09-04 at 09.50.33.jpeg',
        fileType: 'image/jpeg',
        fileUrl: '/uploads/patient-scans/scaled_WhatsApp Image 2026-09-04 at 09.50.33.jpeg',
        fileSize: 27012,
        uploadedAt: '2026-09-21T16:00:00Z',
      },
    ],
  }

  it('correctly resolves relative attachment URLs to absolute API origin and preserves absolute/data URLs', () => {
    expect(getFullAttachmentUrl('data:image/jpeg;base64,123')).toBe('data:image/jpeg;base64,123')
    expect(getFullAttachmentUrl('https://cdn.example.com/scan.jpg')).toBe('https://cdn.example.com/scan.jpg')
    const resolved = getFullAttachmentUrl('/uploads/patient-scans/sample.jpeg')
    expect(resolved).toMatch(/\/uploads\/patient-scans\/sample\.jpeg$/)
  })

  it('renders attached scans with image indicator, metadata, and opens full viewer modal', () => {
    render(
      <MedicalRecordDetailModal
        record={sampleRecord}
        onClose={vi.fn()}
        onEdit={vi.fn()}
      />
    )

    // Verify attachment details are displayed in record detail modal
    expect(screen.getByText('scaled_WhatsApp Image 2026-09-04 at 09.50.33.jpeg')).toBeInTheDocument()
    expect(screen.getByText(/26.4 KB/)).toBeInTheDocument()

    // Click "View" button
    const viewButton = screen.getByRole('button', { name: /view/i })
    fireEvent.click(viewButton)

    // Lightbox modal should now be open
    expect(screen.getByText('Download Image')).toBeInTheDocument()
    expect(screen.getByText('100%')).toBeInTheDocument()
  })

  it('triggers download preserving exact file extension (.jpeg) and never adds .txt', async () => {
    // Spy on document.createElement to intercept download anchor
    const createdAnchors = []
    const originalCreateElement = document.createElement.bind(document)
    vi.spyOn(document, 'createElement').mockImplementation((tagName) => {
      const el = originalCreateElement(tagName)
      if (tagName.toLowerCase() === 'a') {
        createdAnchors.push(el)
      }
      return el
    })

    // Mock fetch to return a sample image blob
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      blob: async () => new Blob(['fake-jpg-binary'], { type: 'image/jpeg' }),
    })

    render(
      <MedicalRecordDetailModal
        record={sampleRecord}
        onClose={vi.fn()}
        onEdit={vi.fn()}
      />
    )

    const downloadButton = screen.getByRole('button', { name: /download/i })
    fireEvent.click(downloadButton)

    // Verify anchor download attributes
    await vi.waitFor(() => {
      expect(createdAnchors.length).toBeGreaterThan(0)
      const anchor = createdAnchors[createdAnchors.length - 1]
      expect(anchor.download).toBe('scaled_WhatsApp Image 2026-09-04 at 09.50.33.jpeg')
      expect(anchor.download).not.toContain('.txt')
    })
  })
})
