import { useEffect, useState } from 'react'

interface Course {
  id: string
  name: string
}

interface TeeSheetSlot {
  time: string
  status: string
  golferName: string | null
  playerCount: number
}

interface TeeSheetResponse {
  courseId: string
  courseName: string
  date: string
  slots: TeeSheetSlot[]
}

export default function TeeSheet() {
  const [courses, setCourses] = useState<Course[]>([])
  const [selectedCourseId, setSelectedCourseId] = useState<string>('')
  const [selectedDate, setSelectedDate] = useState<string>(getTodayDate())
  const [teeSheet, setTeeSheet] = useState<TeeSheetResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetch('/courses')
      .then(r => r.json())
      .then((data: Course[]) => {
        setCourses(data)
        if (data.length > 0) {
          setSelectedCourseId(data[0].id)
        }
      })
      .catch(() => setError('Error: Could not load courses'))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    if (!selectedCourseId) return

    let cancelled = false

    fetch(`/tee-sheets?courseId=${selectedCourseId}&date=${selectedDate}`)
      .then(async response => {
        if (!response.ok) {
          const errorData = await response.json()
          throw new Error(errorData.error || 'Failed to load tee sheet')
        }
        return response.json()
      })
      .then(data => {
        if (!cancelled) {
          setTeeSheet(data)
          setError(null)
        }
      })
      .catch(err => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Unknown error')
          setTeeSheet(null)
        }
      })

    return () => {
      cancelled = true
    }
  }, [selectedCourseId, selectedDate])

  function handleCourseChange(courseId: string) {
    setSelectedCourseId(courseId)
  }

  function handleDateChange(date: string) {
    setSelectedDate(date)
  }

  if (loading) {
    return <div className="admin-container"><p>Loading courses...</p></div>
  }

  if (courses.length === 0) {
    return (
      <div className="admin-container">
        <h1>Tee Sheet</h1>
        <p>No courses registered yet. Register a course first.</p>
      </div>
    )
  }

  return (
    <div className="admin-container">
      <h1>Tee Sheet</h1>
      <p className="subtitle">View the day's tee time bookings</p>

      <div className="row" style={{ marginBottom: '1.5rem', gap: '1rem' }}>
        <label style={{ flex: 1 }}>
          Course
          <select
            value={selectedCourseId}
            onChange={e => handleCourseChange(e.target.value)}
          >
            {courses.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </label>
        <label style={{ flex: 1 }}>
          Date
          <input
            type="date"
            value={selectedDate}
            onChange={e => handleDateChange(e.target.value)}
          />
        </label>
      </div>

      {error && <p className="error">{error}</p>}

      {teeSheet && (
        <>
          <h2 style={{ marginTop: '1rem', marginBottom: '0.5rem', fontSize: '1.25rem' }}>
            {teeSheet.courseName} - {formatDate(teeSheet.date)}
          </h2>
          <table className="courses-table">
            <thead>
              <tr>
                <th>Time</th>
                <th>Status</th>
                <th>Golfer</th>
                <th>Players</th>
              </tr>
            </thead>
            <tbody>
              {teeSheet.slots.map((slot, index) => (
                <tr key={index}>
                  <td><strong>{formatTime(slot.time)}</strong></td>
                  <td>
                    <span style={{
                      padding: '0.25rem 0.5rem',
                      borderRadius: '4px',
                      fontSize: '0.85rem',
                      fontWeight: 500,
                      backgroundColor: slot.status === 'booked' ? '#e8f5e9' : '#f5f5f5',
                      color: slot.status === 'booked' ? '#2e7d32' : '#666',
                    }}>
                      {slot.status === 'booked' ? 'Booked' : 'Open'}
                    </span>
                  </td>
                  <td>{slot.golferName || '—'}</td>
                  <td>{slot.status === 'booked' ? slot.playerCount : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  )
}

function getTodayDate(): string {
  const today = new Date()
  return today.toISOString().split('T')[0]
}

function formatDate(dateString: string): string {
  const date = new Date(dateString + 'T00:00:00')
  return date.toLocaleDateString('en-US', { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })
}

function formatTime(timeString: string): string {
  const [hours, minutes] = timeString.split(':')
  const hour = parseInt(hours, 10)
  const ampm = hour >= 12 ? 'PM' : 'AM'
  const displayHour = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour
  return `${displayHour}:${minutes} ${ampm}`
}
