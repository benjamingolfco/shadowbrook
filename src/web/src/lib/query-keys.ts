export const queryKeys = {
  courses: {
    all: ['courses'] as const,
    detail: (id: string) => ['courses', id] as const,
    settings: (id: string) => ['courses', id, 'settings'] as const,
    pricing: (id: string) => ['courses', id, 'pricing'] as const,
  },
  tenants: {
    all: ['tenants'] as const,
    detail: (id: string) => ['tenants', id] as const,
  },
  teeSheets: {
    byDate: (courseId: string, date: string) => ['tee-sheets', courseId, date] as const,
  },
};
