import { useAuth, type Role } from '../hooks/useAuth';

const roleLabels: Record<Role, string> = {
  admin: 'Admin',
  operator: 'Operator',
  golfer: 'Golfer',
};

const roleOrder: Role[] = ['admin', 'operator', 'golfer'];

export function DevRoleSwitcher() {
  const { role, setRole } = useAuth();

  if (!import.meta.env.DEV) {
    return null;
  }

  const cycleRole = () => {
    const currentIndex = roleOrder.indexOf(role);
    const nextIndex = (currentIndex + 1) % roleOrder.length;
    const nextRole = roleOrder[nextIndex];
    if (nextRole) {
      setRole(nextRole);
    }
  };

  return (
    <button
      onClick={cycleRole}
      className="fixed bottom-4 left-4 z-50 rounded-md bg-gray-800 px-3 py-2 text-xs font-medium text-white shadow-lg transition hover:bg-gray-700"
      title="Click to cycle role"
    >
      Role: {roleLabels[role]}
    </button>
  );
}
