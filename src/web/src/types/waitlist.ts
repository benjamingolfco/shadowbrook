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
  courseName: string;
}

export interface WalkUpWaitlistTodayResponse {
  waitlist: WalkUpWaitlist | null;
  entries: WalkUpWaitlistEntry[];
  openings: WaitlistOpeningEntry[];
}

// Walk-up waitlist tee time openings
export interface FilledGolfer {
  golferId: string;
  golferName: string;
  groupSize: number;
}

export interface WaitlistOpeningEntry {
  id: string;
  teeTime: string; // ISO 8601 DateTime
  slotsAvailable: number;
  slotsRemaining: number;
  status: string;
  filledGolfers: FilledGolfer[];
}

export interface CreateTeeTimeOpeningRequest {
  teeTime: string;
  slotsAvailable: number;
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
  golferId: string;
  golferName: string;
  position: number;
  courseName: string;
}

export interface DuplicateEntryError {
  error: string;
}

export interface DuplicateOpeningError {
  error: string;
  existingSlotsAvailable: number;
  existingSlotsRemaining: number;
  existingOpeningId: string;
  isFull: boolean;
}

// Walk-up offer (golfer claim flow)
export interface WaitlistOfferResponse {
  token: string;
  courseName: string;
  teeTime: string;    // ISO 8601 DateTime
  slotsAvailable: number;
  golferName: string;
  status: 'Pending' | 'Accepted' | 'Rejected';
}

export interface WaitlistOfferAcceptResponse {
  status: string;
  message: string;
  golferId: string;
}
