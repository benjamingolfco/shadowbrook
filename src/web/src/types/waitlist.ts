// Walk-up waitlist (daily session model)
export interface WalkUpWaitlist {
  id: string;
  courseId: string;
  shortCode: string;
  date: string;
  status: 'Open' | 'Closed';
  openedAt: string;
  closedAt: string | null;
}

export interface WalkUpWaitlistEntry {
  id: string;
  golferName: string;
  joinedAt: string;
}

export interface WalkUpWaitlistTodayResponse {
  waitlist: WalkUpWaitlist | null;
  entries: WalkUpWaitlistEntry[];
}

// Per-tee-time waitlist requests
export interface WaitlistSettings {
  waitlistEnabled: boolean;
}

export interface WaitlistRequestEntry {
  id: string;
  teeTime: string; // "HH:mm"
  golfersNeeded: number;
  status: string;
}

export interface WaitlistResponse {
  courseWaitlistId: string | null;
  date: string;
  totalGolfersPending: number;
  requests: WaitlistRequestEntry[];
}

export interface CreateWaitlistRequest {
  date: string;
  teeTime: string;
  golfersNeeded: number;
}
