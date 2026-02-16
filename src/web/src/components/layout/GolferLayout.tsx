import { NavLink, Outlet } from 'react-router';

export default function GolferLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      {/* Header */}
      <header className="border-b bg-white px-4 py-3">
        <div className="flex items-center justify-between">
          <h1 className="text-lg font-bold">Shadowbrook</h1>
          <NavLink
            to="/golfer/profile"
            className="text-sm font-medium text-blue-600 hover:text-blue-800"
          >
            Profile
          </NavLink>
        </div>
      </header>

      {/* Content */}
      <main className="flex-1 bg-gray-50">
        <Outlet />
      </main>

      {/* Bottom Tab Bar */}
      <nav className="border-t bg-white">
        <div className="flex">
          <NavLink
            to="/golfer/tee-times"
            className={({ isActive }) =>
              `flex-1 border-r py-3 text-center text-sm font-medium ${
                isActive ? 'bg-blue-50 text-blue-600' : 'text-gray-600 hover:bg-gray-50'
              }`
            }
          >
            Tee Times
          </NavLink>
          <NavLink
            to="/golfer/bookings"
            className={({ isActive }) =>
              `flex-1 py-3 text-center text-sm font-medium ${
                isActive ? 'bg-blue-50 text-blue-600' : 'text-gray-600 hover:bg-gray-50'
              }`
            }
          >
            My Bookings
          </NavLink>
        </div>
      </nav>
    </div>
  );
}
