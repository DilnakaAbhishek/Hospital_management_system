import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PatientForm, { validatePatientForm } from './PatientForm'

describe('PatientForm Component and Validation', () => {
  afterEach(() => {
    cleanup()
  })

  const validNewPatient = {
    firstName: 'Nimal',
    lastName: 'Perera',
    dateOfBirth: '1995-04-12',
    gender: 'Male',
    nic: '199512345678',
    phoneNumber: '0771234567',
    email: 'nimal@example.com',
    address: '123 Galle Road, Colombo',
    bloodGroup: 'B+',
    emergencyContactName: 'Kamala Perera',
    emergencyContactPhone: '0711234567',
  }

  describe('Placeholders', () => {
    it('displays 12-digit NIC placeholder and 10-digit phone placeholders', () => {
      render(
        <PatientForm
          open={true}
          onClose={vi.fn()}
          onSubmit={vi.fn()}
          loading={false}
          error={null}
        />
      )

      const nicInput = screen.getByLabelText(/NIC Number/i)
      expect(nicInput).toHaveAttribute('placeholder', 'e.g. 199012345678 (12 digits)')

      const phoneInput = screen.getByLabelText(/^Phone Number/i)
      expect(phoneInput).toHaveAttribute('placeholder', 'e.g. 0771234567 (10 digits)')

      const emergencyPhoneInput = screen.getByLabelText(/Contact Phone/i)
      expect(emergencyPhoneInput).toHaveAttribute('placeholder', 'e.g. 0711234567 (10 digits)')
    })
  })

  describe('validatePatientForm function', () => {
    it('passes with valid 12-digit NIC and 10-digit phones', () => {
      expect(validatePatientForm(validNewPatient)).toBeNull()
    })

    it('rejects NIC with fewer than 12 digits', () => {
      const invalid = { ...validNewPatient, nic: '123456789' }
      expect(validatePatientForm(invalid)).toBe('NIC must be a 12-digit number.')
    })

    it('rejects old 9-digit format NIC', () => {
      const invalid = { ...validNewPatient, nic: '950123456V' }
      expect(validatePatientForm(invalid)).toBe('NIC must be a 12-digit number.')
    })

    it('rejects NIC with more than 12 digits', () => {
      const invalid = { ...validNewPatient, nic: '1995123456789' }
      expect(validatePatientForm(invalid)).toBe('NIC must be a 12-digit number.')
    })

    it('rejects phone number with fewer than 10 digits', () => {
      const invalid = { ...validNewPatient, phoneNumber: '077123456' }
      expect(validatePatientForm(invalid)).toBe('Phone number must be a 10-digit number.')
    })

    it('rejects phone number with more than 10 digits', () => {
      const invalid = { ...validNewPatient, phoneNumber: '07712345678' }
      expect(validatePatientForm(invalid)).toBe('Phone number must be a 10-digit number.')
    })

    it('rejects non-numeric phone number', () => {
      const invalid = { ...validNewPatient, phoneNumber: '077123456a' }
      expect(validatePatientForm(invalid)).toBe('Phone number must be a 10-digit number.')
    })

    it('rejects emergency contact phone with non-10 digits', () => {
      const invalid = { ...validNewPatient, emergencyContactPhone: '07112345' }
      expect(validatePatientForm(invalid)).toBe('Emergency contact phone must be a 10-digit number.')
    })
  })

  describe('Form submission and UI validation display', () => {
    it('shows error banner when submitting non-12-digit NIC in Add mode', async () => {
      const onSubmit = vi.fn()
      render(
        <PatientForm
          open={true}
          onClose={vi.fn()}
          onSubmit={onSubmit}
          loading={false}
          error={null}
        />
      )

      fireEvent.change(screen.getByLabelText(/^First Name/i), { target: { value: 'Nimal' } })
      fireEvent.change(screen.getByLabelText(/^Last Name/i), { target: { value: 'Perera' } })
      fireEvent.change(screen.getByLabelText(/^Date of Birth/i), { target: { value: '1995-04-12' } })
      fireEvent.change(screen.getByLabelText(/^NIC Number/i), { target: { value: '950123456V' } }) // Old format, not 12 digits
      fireEvent.change(screen.getByLabelText(/^Phone Number/i), { target: { value: '0771234567' } })
      fireEvent.change(screen.getByLabelText(/^Email Address/i), { target: { value: 'nimal@example.com' } })
      fireEvent.change(screen.getByLabelText(/^Address/i), { target: { value: 'Colombo' } })
      fireEvent.change(screen.getByLabelText(/^Contact Name/i), { target: { value: 'Kamala' } })
      fireEvent.change(screen.getByLabelText(/^Contact Phone/i), { target: { value: '0711234567' } })

      fireEvent.click(screen.getByRole('button', { name: /Register Patient/i }))

      expect(screen.getByText('NIC must be a 12-digit number.')).toBeInTheDocument()
      expect(onSubmit).not.toHaveBeenCalled()
    })

    it('shows error banner when submitting non-10-digit phone number in Add mode', async () => {
      const onSubmit = vi.fn()
      render(
        <PatientForm
          open={true}
          onClose={vi.fn()}
          onSubmit={onSubmit}
          loading={false}
          error={null}
        />
      )

      fireEvent.change(screen.getByLabelText(/^First Name/i), { target: { value: 'Nimal' } })
      fireEvent.change(screen.getByLabelText(/^Last Name/i), { target: { value: 'Perera' } })
      fireEvent.change(screen.getByLabelText(/^Date of Birth/i), { target: { value: '1995-04-12' } })
      fireEvent.change(screen.getByLabelText(/^NIC Number/i), { target: { value: '199512345678' } })
      fireEvent.change(screen.getByLabelText(/^Phone Number/i), { target: { value: '07712345' } }) // 8 digits
      fireEvent.change(screen.getByLabelText(/^Email Address/i), { target: { value: 'nimal@example.com' } })
      fireEvent.change(screen.getByLabelText(/^Address/i), { target: { value: 'Colombo' } })
      fireEvent.change(screen.getByLabelText(/^Contact Name/i), { target: { value: 'Kamala' } })
      fireEvent.change(screen.getByLabelText(/^Contact Phone/i), { target: { value: '0711234567' } })

      fireEvent.click(screen.getByRole('button', { name: /Register Patient/i }))

      expect(screen.getByText('Phone number must be a 10-digit number.')).toBeInTheDocument()
      expect(onSubmit).not.toHaveBeenCalled()
    })

    it('submits trimmed values when valid 12-digit NIC and 10-digit phones are entered', async () => {
      const onSubmit = vi.fn()
      render(
        <PatientForm
          open={true}
          onClose={vi.fn()}
          onSubmit={onSubmit}
          loading={false}
          error={null}
        />
      )

      fireEvent.change(screen.getByLabelText(/^First Name/i), { target: { value: '  Nimal ' } })
      fireEvent.change(screen.getByLabelText(/^Last Name/i), { target: { value: '  Perera ' } })
      fireEvent.change(screen.getByLabelText(/^Date of Birth/i), { target: { value: '1995-04-12' } })
      fireEvent.change(screen.getByLabelText(/^NIC Number/i), { target: { value: '  199512345678  ' } })
      fireEvent.change(screen.getByLabelText(/^Phone Number/i), { target: { value: '  0771234567  ' } })
      fireEvent.change(screen.getByLabelText(/^Email Address/i), { target: { value: '  nimal@example.com ' } })
      fireEvent.change(screen.getByLabelText(/^Address/i), { target: { value: '  Colombo ' } })
      fireEvent.change(screen.getByLabelText(/^Contact Name/i), { target: { value: '  Kamala ' } })
      fireEvent.change(screen.getByLabelText(/^Contact Phone/i), { target: { value: '  0711234567  ' } })

      fireEvent.click(screen.getByRole('button', { name: /Register Patient/i }))

      expect(onSubmit).toHaveBeenCalledTimes(1)
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
        firstName: 'Nimal',
        lastName: 'Perera',
        nic: '199512345678',
        phoneNumber: '0771234567',
        emergencyContactPhone: '0711234567',
        email: 'nimal@example.com',
      }))
    })

    it('shows error banner when submitting non-12-digit NIC in Edit mode', async () => {
      const onSubmit = vi.fn()
      const existingPatient = {
        patientId: 10,
        ...validNewPatient,
        nic: '950123456V', // legacy NIC
      }

      render(
        <PatientForm
          open={true}
          onClose={vi.fn()}
          onSubmit={onSubmit}
          patient={existingPatient}
          loading={false}
          error={null}
        />
      )

      fireEvent.click(screen.getByRole('button', { name: /Save Changes/i }))

      expect(screen.getByText('NIC must be a 12-digit number.')).toBeInTheDocument()
      expect(onSubmit).not.toHaveBeenCalled()
    })

    it('submits successfully in Edit mode when 12-digit NIC and 10-digit phone are valid', async () => {
      const onSubmit = vi.fn()
      const existingPatient = {
        patientId: 10,
        ...validNewPatient,
      }

      render(
        <PatientForm
          open={true}
          onClose={vi.fn()}
          onSubmit={onSubmit}
          patient={existingPatient}
          loading={false}
          error={null}
        />
      )

      fireEvent.click(screen.getByRole('button', { name: /Save Changes/i }))

      expect(onSubmit).toHaveBeenCalledTimes(1)
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
        nic: '199512345678',
        phoneNumber: '0771234567',
      }))
    })
  })
})
