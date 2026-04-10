export interface TeeSheetSlot {
  teeTime: string; // ISO 8601 DateTime
  status: string;
  golferName: string | null;
  playerCount: number;
}

export interface TeeSheetResponse {
  courseId: string;
  courseName: string;
  slots: TeeSheetSlot[];
}

export interface DayStatus {
  date: string;
  status: 'notStarted' | 'draft' | 'published';
  teeSheetId?: string;
  intervalCount?: number;
}

export interface WeeklyStatusResponse {
  weekStart: string;
  weekEnd: string;
  days: DayStatus[];
}

export interface BulkDraftResponse {
  teeSheets: Array<{ date: string; teeSheetId: string }>;
}
