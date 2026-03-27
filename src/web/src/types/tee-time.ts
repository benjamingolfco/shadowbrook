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
