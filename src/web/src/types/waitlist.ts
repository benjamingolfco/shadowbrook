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
