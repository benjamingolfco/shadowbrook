export interface TeeSheetSlot {
  time: string;
  status: string;
  golferName: string | null;
  playerCount: number;
}

export interface TeeSheetResponse {
  courseId: string;
  courseName: string;
  date: string;
  slots: TeeSheetSlot[];
}
