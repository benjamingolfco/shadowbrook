export const queryKeys = {
  courses: {
    all: ['courses'] as const,
    detail: (id: string) => ['courses', id] as const,
    settings: (id: string) => ['courses', id, 'settings'] as const,
    pricing: (id: string) => ['courses', id, 'pricing'] as const,
  },
  organizations: {
    all: ['organizations'] as const,
    detail: (id: string) => ['organizations', id] as const,
  },
  users: {
    all: ['users'] as const,
    detail: (id: string) => ['users', id] as const,
  },
  analytics: {
    summary: ['analytics', 'summary'] as const,
    fillRates: (courseId?: string) => ['analytics', 'fill-rates', courseId] as const,
    bookings: (courseId?: string) => ['analytics', 'bookings', courseId] as const,
    popularTimes: (courseId?: string) => ['analytics', 'popular-times', courseId] as const,
    waitlist: (courseId?: string) => ['analytics', 'waitlist', courseId] as const,
  },
  teeSheets: {
    byDate: (courseId: string, date: string) => ['tee-sheets', courseId, date] as const,
    weeklyStatus: (courseId: string, startDate: string) => ['tee-sheets', courseId, 'week', startDate] as const,
    bookingCount: (courseId: string, date: string) => ['tee-sheets', courseId, date, 'booking-count'] as const,
  },
  walkUpWaitlist: {
    today: (courseId: string) => ['walkup-waitlist', courseId, 'today'] as const,
  },
  walkupJoin: {
    verify: (code: string) => ['walkup-join', 'verify', code] as const,
  },
  walkUpOffer: {
    byToken: (token: string) => ['walk-up-offer', token] as const,
  },
  devSms: {
    all: ['dev-sms'] as const,
    byGolfer: (golferId: string) => ['dev-sms', 'golfer', golferId] as const,
  },
  walkUpQr: {
    status: (shortCode: string) => ['walkup-qr', 'status', shortCode] as const,
  },
  features: {
    all: ['features'] as const,
    byCourse: (courseId: string) => ['features', courseId] as const,
  },
  deadLetters: {
    all: ['dead-letters'] as const,
  },
};
