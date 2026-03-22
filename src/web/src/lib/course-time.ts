/**
 * Returns today's date as "yyyy-MM-dd" in the given IANA timezone.
 */
export function getCourseToday(timeZoneId: string): string {
  return new Date().toLocaleDateString('en-CA', { timeZone: timeZoneId });
}

/**
 * Formats a UTC ISO timestamp to the course's local time (e.g., "2:30 PM").
 */
export function formatCourseTime(isoString: string, timeZoneId: string): string {
  return new Date(isoString).toLocaleTimeString('en-US', {
    timeZone: timeZoneId,
    hour: '2-digit',
    minute: '2-digit',
  });
}

/**
 * Returns the browser's IANA timezone identifier.
 */
export function getBrowserTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}
