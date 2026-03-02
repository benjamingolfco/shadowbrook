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
