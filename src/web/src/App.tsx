import { useState } from 'react'
import CourseRegistration from './CourseRegistration'
import AdminCourses from './AdminCourses'
import './App.css'

type View = 'register' | 'admin'

function App() {
  const [currentView, setCurrentView] = useState<View>('register')

  return (
    <div>
      <nav className="nav">
        <button
          onClick={() => setCurrentView('register')}
          className={currentView === 'register' ? 'active' : ''}
        >
          Register Course
        </button>
        <button
          onClick={() => setCurrentView('admin')}
          className={currentView === 'admin' ? 'active' : ''}
        >
          Admin: View All Courses
        </button>
      </nav>

      {currentView === 'register' ? <CourseRegistration /> : <AdminCourses />}
    </div>
  )
}

export default App
