import { useEffect, useState } from 'react'
import { API_BASE } from './api'

interface Course {
  id: string
  name: string
  streetAddress?: string
  city?: string
  state?: string
  zipCode?: string
  contactEmail?: string
  contactPhone?: string
  createdAt: string
  updatedAt: string
}

export default function AdminCourses() {
  const [courses, setCourses] = useState<Course[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetchCourses()
  }, [])

  async function fetchCourses() {
    try {
      const response = await fetch(`${API_BASE}/courses`)
      if (!response.ok) {
        throw new Error('Failed to fetch courses')
      }
      const data = await response.json()
      setCourses(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error')
    } finally {
      setLoading(false)
    }
  }

  if (loading) {
    return <div className="admin-container"><p>Loading courses...</p></div>
  }

  if (error) {
    return <div className="admin-container"><p className="error">Error: {error}</p></div>
  }

  return (
    <div className="admin-container">
      <h1>All Registered Courses</h1>
      <p className="subtitle">Platform Admin View</p>

      {courses.length === 0 ? (
        <p>No courses registered yet.</p>
      ) : (
        <table className="courses-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Location</th>
              <th>Contact</th>
              <th>Registered</th>
            </tr>
          </thead>
          <tbody>
            {courses.map(course => (
              <tr key={course.id}>
                <td><strong>{course.name}</strong></td>
                <td>
                  {course.streetAddress && <div>{course.streetAddress}</div>}
                  {(course.city || course.state || course.zipCode) && (
                    <div>
                      {course.city}{course.city && course.state ? ', ' : ''}{course.state} {course.zipCode}
                    </div>
                  )}
                </td>
                <td>
                  {course.contactEmail && <div>{course.contactEmail}</div>}
                  {course.contactPhone && <div>{course.contactPhone}</div>}
                </td>
                <td>{new Date(course.createdAt).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
