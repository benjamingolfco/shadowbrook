export type AppUserRole = 'Admin' | 'Owner' | 'Staff';

export interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  role: AppUserRole;
  organization: { id: string; name: string } | null;
  courses: { id: string; name: string }[];
  permissions: string[];
}
