import { useState, useEffect, type ReactNode } from 'react';
import { AuthContext, type Role } from '../hooks/useAuth';
import type { User } from '@/types/user';

interface MockAuthProviderProps {
  children: ReactNode;
}

const STORAGE_KEY = 'shadowbrook-dev-role';

function getMockUser(role: Role): User {
  switch (role) {
    case 'admin':
      return {
        id: 'admin-1',
        name: 'Admin User',
        email: 'admin@shadowbrook.local',
        role: 'admin',
      };
    case 'operator':
      return {
        id: 'operator-1',
        name: 'Course Operator',
        email: 'operator@shadowbrook.local',
        role: 'operator',
      };
    case 'golfer':
      return {
        id: 'golfer-1',
        name: 'John Golfer',
        email: 'golfer@shadowbrook.local',
        role: 'golfer',
      };
  }
}

function getStoredRole(): Role {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'admin' || stored === 'operator' || stored === 'golfer') {
    return stored;
  }
  return 'admin';
}

export function MockAuthProvider({ children }: MockAuthProviderProps) {
  const [role, setRoleState] = useState<Role>(getStoredRole);
  const [user, setUser] = useState<User>(getMockUser(role));

  useEffect(() => {
    setUser(getMockUser(role));
  }, [role]);

  const setRole = (newRole: Role) => {
    localStorage.setItem(STORAGE_KEY, newRole);
    setRoleState(newRole);
  };

  const login = () => {
    // No-op for now
  };

  const logout = () => {
    // No-op for now
  };

  const value = {
    user,
    role,
    isAuthenticated: true,
    login,
    logout,
    setRole,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
