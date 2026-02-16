import { useAuth, type Role } from '../hooks/useAuth';

const roleLabels: Record<Role, string> = {
  admin: 'Admin',
  operator: 'Operator',
  golfer: 'Golfer',
};

const roleOrder: Role[] = ['admin', 'operator', 'golfer'];

export function DevRoleSwitcher() {
  const { role, setRole } = useAuth();

  return (
    <select
      value={role}
      onChange={(e) => setRole(e.target.value as Role)}
      className="fixed bottom-4 left-4 z-50 rounded-md bg-gray-800 px-3 py-2 text-xs font-medium text-white shadow-lg transition hover:bg-gray-700"
    >
      {roleOrder.map((r) => (
        <option key={r} value={r}>
          Role: {roleLabels[r]}
        </option>
      ))}
    </select>
  );
}
