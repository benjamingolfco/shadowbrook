import { useState } from 'react'
import CourseRegistration from './CourseRegistration'
import TeeTimeSettings from './TeeTimeSettings'
import AdminCourses from './AdminCourses'
import './App.css'

type View = 'register' | 'tee-times' | 'admin'

const views: Record<View, { label: string; component: () => React.JSX.Element }> = {
  register: { label: 'Register Course', component: CourseRegistration },
  'tee-times': { label: 'Tee Time Settings', component: TeeTimeSettings },
  admin: { label: 'Admin: All Courses', component: AdminCourses },
}

function App() {
  const [currentView, setCurrentView] = useState<View>('register')

  const ActiveComponent = views[currentView].component

  return (
    <div>
      <nav className="nav">
        {(Object.entries(views) as [View, typeof views[View]][]).map(([key, { label }]) => (
          <button
            key={key}
            onClick={() => setCurrentView(key)}
            className={currentView === key ? 'active' : ''}
          >
            {label}
          </button>
        ))}
      </nav>

      <ActiveComponent />
    </div>
  )
}

export default App
