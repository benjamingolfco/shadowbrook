export interface User {
  id: string;
  email: string;
  displayName: string;
  role: string;
  organization: { id: string; name: string } | null;
  organizations: { id: string; name: string }[] | null;
  courses: { id: string; name: string }[];
  permissions: string[];
}
