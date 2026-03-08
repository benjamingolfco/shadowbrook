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

// Walk-up waitlist tee time requests
export interface WaitlistRequestEntry {
  id: string;
  teeTime: string; // "HH:mm"
  golfersNeeded: number;
  status: string;
}

export interface CreateWaitlistRequest {
  date: string;
  teeTime: string;
  golfersNeeded: number;
}

// Walkup join (public/golfer-facing)
export interface VerifyCodeResponse {
  courseWaitlistId: string;
  courseName: string;
  shortCode: string;
}

export interface JoinWaitlistRequest {
  courseWaitlistId: string;
  firstName: string;
  lastName: string;
  phone: string;
}

export interface JoinWaitlistResponse {
  entryId: string;
  golferName: string;
  position: number;
  courseName: string;
}

export interface DuplicateEntryError {
  error: string;
  position: number;
}
