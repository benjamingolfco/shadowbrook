import { NavLink, Outlet } from 'react-router';

export default function GolferLayout() {
  return (
    <div className="flex min-h-screen flex-col">
      {/* Header */}
      <header className="border-b bg-white px-4 py-3">
        <div className="flex items-center justify-between">
          <h1 className="text-lg font-bold">Teeforce</h1>
        </div>
      </header>

      {/* Content */}
      <main className="flex-1 bg-canvas">
        <Outlet />
      </main>

      {/* Bottom Tab Bar */}
      <nav className="border-t bg-white">
        <div className="flex">
          <NavLink
            to="/golfer/tee-times"
            className={({ isActive }) =>
              `flex-1 border-r py-3 text-center text-sm font-medium ${
                isActive ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-muted'
              }`
            }
          >
            Tee Times
          </NavLink>
          <NavLink
            to="/golfer/bookings"
            className={({ isActive }) =>
              `flex-1 py-3 text-center text-sm font-medium ${
                isActive ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-muted'
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
