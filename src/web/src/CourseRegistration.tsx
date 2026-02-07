import { type FormEvent, useState } from 'react'

interface CourseForm {
  name: string
  streetAddress: string
  city: string
  state: string
  zipCode: string
  contactEmail: string
  contactPhone: string
}

const initialForm: CourseForm = {
  name: '',
  streetAddress: '',
  city: '',
  state: '',
  zipCode: '',
  contactEmail: '',
  contactPhone: '',
}

export default function CourseRegistration() {
  const [form, setForm] = useState<CourseForm>(initialForm)
  const [submitting, setSubmitting] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  function updateField(field: keyof CourseForm, value: string) {
    setForm(prev => ({ ...prev, [field]: value }))
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setMessage(null)

    try {
      const response = await fetch('/courses', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      })

      if (!response.ok) {
        const err = await response.json()
        setMessage(`Error: ${err.error ?? 'Something went wrong'}`)
        return
      }

      setMessage('Course registered successfully!')
      setForm(initialForm)
    } catch {
      setMessage('Error: Could not connect to server')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="form-container">
      <h1>Register a Course</h1>
      <form onSubmit={handleSubmit}>
        <label>
          Course Name *
          <input
            type="text"
            value={form.name}
            onChange={e => updateField('name', e.target.value)}
            required
          />
        </label>
        <label>
          Street Address
          <input
            type="text"
            value={form.streetAddress}
            onChange={e => updateField('streetAddress', e.target.value)}
          />
        </label>
        <div className="row">
          <label>
            City
            <input
              type="text"
              value={form.city}
              onChange={e => updateField('city', e.target.value)}
            />
          </label>
          <label>
            State
            <input
              type="text"
              value={form.state}
              onChange={e => updateField('state', e.target.value)}
            />
          </label>
          <label>
            Zip Code
            <input
              type="text"
              value={form.zipCode}
              onChange={e => updateField('zipCode', e.target.value)}
            />
          </label>
        </div>
        <label>
          Contact Email
          <input
            type="email"
            value={form.contactEmail}
            onChange={e => updateField('contactEmail', e.target.value)}
          />
        </label>
        <label>
          Contact Phone
          <input
            type="tel"
            value={form.contactPhone}
            onChange={e => updateField('contactPhone', e.target.value)}
          />
        </label>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Registering...' : 'Register Course'}
        </button>
        {message && <p className="message">{message}</p>}
      </form>
    </div>
  )
}
