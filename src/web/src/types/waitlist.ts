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
  groupSize: number;
  joinedAt: string;
}

export interface AddGolferToWaitlistRequest {
  firstName: string;
  lastName: string;
  phone: string;
  groupSize: number;
}

export interface AddGolferToWaitlistResponse {
  entryId: string;
  golferName: string;
  golferPhone: string;
  groupSize: number;
  position: number;
  courseName: string;
}

export interface WalkUpWaitlistTodayResponse {
  waitlist: WalkUpWaitlist | null;
  entries: WalkUpWaitlistEntry[];
  requests: WaitlistRequestEntry[];
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

// Walk-up offer (golfer claim flow)
export interface WaitlistOfferResponse {
  token: string;
  courseName: string;
  date: string;       // "yyyy-MM-dd"
  teeTime: string;    // "HH:mm"
  golfersNeeded: number;
  golferName: string;
  status: 'Pending' | 'Accepted' | 'Expired';
  expiresAt: string;  // ISO 8601
}

export interface WaitlistOfferAcceptResponse {
  status: string;
  courseName: string;
  date: string;
  teeTime: string;
  golferName: string;
  message: string;
}
