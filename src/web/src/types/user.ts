export type Role = 'admin' | 'operator' | 'golfer';

export interface User {
  id: string;
  name: string;
  email: string;
  role: Role;
}
