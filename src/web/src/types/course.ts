export interface Course {
  id: string;
  name: string;
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
}

export interface Pricing {
  flatRatePrice: number;
}
