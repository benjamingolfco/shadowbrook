import { type FormEvent, useEffect, useState } from 'react'

interface Course {
  id: string
  name: string
}

interface TeeTimeSettingsForm {
  courseId: string
  teeTimeIntervalMinutes: string
  firstTeeTime: string
  lastTeeTime: string
}

const initialForm: TeeTimeSettingsForm = {
  courseId: '',
  teeTimeIntervalMinutes: '10',
  firstTeeTime: '07:00',
  lastTeeTime: '18:00',
}

export default function TeeTimeSettings() {
  const [courses, setCourses] = useState<Course[]>([])
  const [form, setForm] = useState<TeeTimeSettingsForm>(initialForm)
  const [submitting, setSubmitting] = useState(false)
  const [loading, setLoading] = useState(true)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    fetch('/courses')
      .then(r => r.json())
      .then((data: Course[]) => {
        setCourses(data)
        if (data.length > 0) {
          setForm(prev => ({ ...prev, courseId: data[0].id }))
          loadSettings(data[0].id)
        }
      })
      .catch(() => setMessage('Error: Could not load courses'))
      .finally(() => setLoading(false))
  }, [])

  async function loadSettings(courseId: string) {
    try {
      const response = await fetch(`/courses/${courseId}/tee-time-settings`)
      if (!response.ok) return
      const data = await response.json()
      if (data.teeTimeIntervalMinutes) {
        setForm(prev => ({
          ...prev,
          teeTimeIntervalMinutes: String(data.teeTimeIntervalMinutes),
          firstTeeTime: data.firstTeeTime.slice(0, 5),
          lastTeeTime: data.lastTeeTime.slice(0, 5),
        }))
      }
    } catch {
      // Settings not configured yet, use defaults
    }
  }

  function handleCourseChange(courseId: string) {
    setForm(prev => ({ ...prev, courseId }))
    setMessage(null)
    loadSettings(courseId)
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setSubmitting(true)
    setMessage(null)

    try {
      const response = await fetch(`/courses/${form.courseId}/tee-time-settings`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          teeTimeIntervalMinutes: Number(form.teeTimeIntervalMinutes),
          firstTeeTime: form.firstTeeTime,
          lastTeeTime: form.lastTeeTime,
        }),
      })

      if (!response.ok) {
        const err = await response.json()
        setMessage(`Error: ${err.error ?? 'Something went wrong'}`)
        return
      }

      setMessage('Tee time settings saved successfully!')
    } catch {
      setMessage('Error: Could not connect to server')
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) {
    return <div className="form-container"><p>Loading courses...</p></div>
  }

  if (courses.length === 0) {
    return (
      <div className="form-container">
        <h1>Tee Time Settings</h1>
        <p>No courses registered yet. Register a course first.</p>
      </div>
    )
  }

  return (
    <div className="form-container">
      <h1>Tee Time Settings</h1>
      <form onSubmit={handleSubmit}>
        <label>
          Course *
          <select
            value={form.courseId}
            onChange={e => handleCourseChange(e.target.value)}
          >
            {courses.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </label>
        <label>
          Tee Time Interval *
          <select
            value={form.teeTimeIntervalMinutes}
            onChange={e => setForm(prev => ({ ...prev, teeTimeIntervalMinutes: e.target.value }))}
          >
            <option value="8">Every 8 minutes</option>
            <option value="10">Every 10 minutes</option>
            <option value="12">Every 12 minutes</option>
          </select>
        </label>
        <div className="row">
          <label>
            First Tee Time *
            <input
              type="time"
              value={form.firstTeeTime}
              onChange={e => setForm(prev => ({ ...prev, firstTeeTime: e.target.value }))}
              required
            />
          </label>
          <label>
            Last Tee Time *
            <input
              type="time"
              value={form.lastTeeTime}
              onChange={e => setForm(prev => ({ ...prev, lastTeeTime: e.target.value }))}
              required
            />
          </label>
        </div>
        <button type="submit" disabled={submitting}>
          {submitting ? 'Saving...' : 'Save Settings'}
        </button>
        {message && <p className="message">{message}</p>}
      </form>
    </div>
  )
}
