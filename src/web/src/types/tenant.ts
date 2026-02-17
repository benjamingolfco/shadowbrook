export interface Tenant {
  id: string;
  organizationName: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  courseCount?: number;
  createdAt: string;
  updatedAt: string;
}

export interface TenantDetail extends Tenant {
  courses: Array<{
    id: string;
    name: string;
  }>;
}
