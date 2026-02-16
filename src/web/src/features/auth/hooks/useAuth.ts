import { createContext, useContext } from 'react';
import type { User } from '@/types/user';

export type Role = 'admin' | 'operator' | 'golfer';

export interface AuthContextValue {
  user: User | null;
  role: Role;
  isAuthenticated: boolean;
  login: () => void;
  logout: () => void;
  setRole: (role: Role) => void; // dev only
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
