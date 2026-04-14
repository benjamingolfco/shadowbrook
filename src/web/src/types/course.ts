export interface Course {
  id: string;
  name: string;
  tenantId: string;
  tenantName?: string;
  timeZoneId: string;
  streetAddress?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  contactEmail?: string;
  contactPhone?: string;
  createdAt: string;
  updatedAt: string;
}

export interface TeeTimeSettings {
  teeTimeIntervalMinutes: number;
  firstTeeTime: string;
  lastTeeTime: string;
  defaultCapacity: number;
}

export interface Pricing {
  defaultPrice: number | null;
  minPrice: number | null;
  maxPrice: number | null;
  schedules: RateSchedule[];
}

export interface RateSchedule {
  id: string;
  name: string;
  daysOfWeek: number[];
  startTime: string;
  endTime: string;
  price: number;
  invalidReason: string | null;
}
