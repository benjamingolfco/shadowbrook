/**
 * Returns today's date as "yyyy-MM-dd" in the given IANA timezone.
 */
export function getCourseToday(timeZoneId: string): string {
  return new Date().toLocaleDateString('en-CA', { timeZone: timeZoneId });
}

/**
 * Returns the current time as "HH:mm" (24-hour, zero-padded) in the given IANA timezone.
 * Uses hourCycle 'h23' to ensure midnight returns "00:00" rather than "24:00".
 */
export function getCourseNow(timeZoneId: string): string {
  const now = new Date();
  const timeString = now.toLocaleTimeString('en-US', {
    timeZone: timeZoneId,
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  });
  return timeString;
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

/**
 * Formats a wall-clock ISO date/datetime string to a human-readable date (e.g., "Saturday, March 21, 2026").
 * Wall-clock values represent "8:30 AM at the course" and should be formatted without timezone conversion.
 * Accepts either "yyyy-MM-dd" or "yyyy-MM-ddTHH:mm:ss" format.
 */
export function formatWallClockDate(dateTimeString: string): string {
  // Extract date parts from the ISO string
  const datePart = dateTimeString.split('T')[0];
  if (!datePart) {
    throw new Error('Invalid date string format');
  }
  const parts = datePart.split('-').map(Number);

  if (parts.length !== 3 || parts.some(isNaN)) {
    throw new Error('Invalid date string format');
  }

  const [year, month, day] = parts;

  // Create a Date using UTC to prevent browser timezone shifting
  const date = new Date(Date.UTC(year!, month! - 1, day));

  return date.toLocaleDateString('en-US', {
    timeZone: 'UTC',
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

/**
 * Returns the next 10-minute interval as "HH:mm" in the given IANA timezone.
 * If the current time is already on a 10-minute mark, returns the current time.
 * Used as the default value for the Post Tee Time form.
 */
export function getNextTeeTimeInterval(timeZoneId: string): string {
  const now = new Date();
  const parts = now.toLocaleTimeString('en-US', {
    timeZone: timeZoneId,
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).split(':').map(Number);

  const hours = parts[0] ?? 0;
  const minutes = parts[1] ?? 0;

  const remainder = minutes % 10;
  if (remainder === 0) {
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`;
  }

  const totalMinutes = hours * 60 + minutes + (10 - remainder);
  const newHours = Math.floor(totalMinutes / 60) % 24;
  const newMinutes = totalMinutes % 60;

  return `${String(newHours).padStart(2, '0')}:${String(newMinutes).padStart(2, '0')}`;
}

/**
 * Formats a wall-clock time string to 12-hour format (e.g., "8:30 AM").
 * Accepts either "HH:mm" or full ISO datetime "yyyy-MM-ddTHH:mm:ss" format.
 * Uses manual conversion to avoid Date object timezone issues.
 */
export function formatWallClockTime(timeString: string): string {
  // Extract time portion if it's a full ISO datetime
  let timePart = timeString;
  if (timeString.includes('T')) {
    const parts = timeString.split('T');
    timePart = parts[1] ?? '';
  }

  // Parse hours and minutes
  const [hoursStr, minutesStr] = timePart.split(':');
  const hours = Number(hoursStr);
  const minutes = Number(minutesStr);

  if (isNaN(hours) || isNaN(minutes)) {
    throw new Error('Invalid time string format');
  }

  // Convert to 12-hour format
  const period = hours >= 12 ? 'PM' : 'AM';
  const hours12 = hours === 0 ? 12 : hours > 12 ? hours - 12 : hours;
  const minutesPadded = minutes.toString().padStart(2, '0');

  return `${hours12}:${minutesPadded} ${period}`;
}
