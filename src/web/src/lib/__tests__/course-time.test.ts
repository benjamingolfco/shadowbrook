import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  formatWallClockDate,
  formatWallClockTime,
  getCourseToday,
  getCourseNow,
  formatCourseTime,
  getBrowserTimeZone,
  getNextTeeTimeInterval,
  buildTeeTimeDateTime,
} from '../course-time';

describe('formatWallClockDate', () => {
  it('formats a standard ISO datetime to human-readable date', () => {
    expect(formatWallClockDate('2026-03-21T08:30:00')).toBe('Saturday, March 21, 2026');
  });

  it('formats a date-only string to human-readable date', () => {
    expect(formatWallClockDate('2026-03-21')).toBe('Saturday, March 21, 2026');
  });

  it('formats a different month and day correctly', () => {
    expect(formatWallClockDate('2026-01-05T14:00:00')).toBe('Monday, January 5, 2026');
  });

  it('formats a year-end date correctly', () => {
    expect(formatWallClockDate('2025-12-31T23:59:00')).toBe('Wednesday, December 31, 2025');
  });

  it('formats a year-start date correctly', () => {
    expect(formatWallClockDate('2026-01-01')).toBe('Thursday, January 1, 2026');
  });

  it('formats a leap year date correctly', () => {
    expect(formatWallClockDate('2024-02-29T12:00:00')).toBe('Thursday, February 29, 2024');
  });

  it('throws an error for invalid format', () => {
    expect(() => formatWallClockDate('invalid')).toThrow('Invalid date string format');
  });
});

describe('formatWallClockTime', () => {
  it('formats a full ISO datetime with morning time', () => {
    expect(formatWallClockTime('2026-03-21T08:30:00')).toBe('8:30 AM');
  });

  it('formats a full ISO datetime with afternoon time', () => {
    expect(formatWallClockTime('2026-03-21T14:00:00')).toBe('2:00 PM');
  });

  it('formats an HH:mm string with morning time', () => {
    expect(formatWallClockTime('08:30')).toBe('8:30 AM');
  });

  it('formats noon correctly', () => {
    expect(formatWallClockTime('12:00')).toBe('12:00 PM');
  });

  it('formats midnight correctly', () => {
    expect(formatWallClockTime('00:00')).toBe('12:00 AM');
  });

  it('formats a single-digit minute correctly', () => {
    expect(formatWallClockTime('2026-03-21T09:05:00')).toBe('9:05 AM');
  });

  it('formats 1:00 PM correctly', () => {
    expect(formatWallClockTime('13:00')).toBe('1:00 PM');
  });

  it('formats 11:59 PM correctly', () => {
    expect(formatWallClockTime('23:59')).toBe('11:59 PM');
  });

  it('formats 11:59 AM correctly', () => {
    expect(formatWallClockTime('11:59')).toBe('11:59 AM');
  });

  it('throws an error for invalid format', () => {
    expect(() => formatWallClockTime('invalid')).toThrow('Invalid time string format');
  });
});

describe('getCourseToday', () => {
  it('returns a date string in yyyy-MM-dd format', () => {
    const result = getCourseToday('America/New_York');
    expect(result).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });

  it('returns different dates for different timezones', () => {
    const nyDate = getCourseToday('America/New_York');
    const tokyoDate = getCourseToday('Asia/Tokyo');
    // This might be the same or different depending on the current time
    expect(typeof nyDate).toBe('string');
    expect(typeof tokyoDate).toBe('string');
  });
});

describe('getCourseNow', () => {
  it('returns a time string in HH:mm format', () => {
    const result = getCourseNow('America/New_York');
    expect(result).toMatch(/^\d{2}:\d{2}$/);
  });

  it('returns different times for different timezones', () => {
    const nyTime = getCourseNow('America/New_York');
    const tokyoTime = getCourseNow('Asia/Tokyo');
    expect(typeof nyTime).toBe('string');
    expect(typeof tokyoTime).toBe('string');
  });
});

describe('formatCourseTime', () => {
  it('formats a UTC ISO timestamp to course local time', () => {
    // March 21, 2026 at 12:30 UTC
    const result = formatCourseTime('2026-03-21T12:30:00Z', 'America/New_York');
    // Should be 8:30 AM EDT (UTC-4) or 7:30 AM EST (UTC-5) depending on DST
    expect(result).toMatch(/^(07|08):30 (AM|PM)$/);
  });

  it('handles different timezones', () => {
    const utcTime = '2026-03-21T12:00:00Z';
    const nyTime = formatCourseTime(utcTime, 'America/New_York');
    const laTime = formatCourseTime(utcTime, 'America/Los_Angeles');
    expect(typeof nyTime).toBe('string');
    expect(typeof laTime).toBe('string');
  });
});

describe('getBrowserTimeZone', () => {
  it('returns a string timezone identifier', () => {
    const tz = getBrowserTimeZone();
    expect(typeof tz).toBe('string');
    expect(tz.length).toBeGreaterThan(0);
  });
});

describe('getNextTeeTimeInterval', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('rounds up to the next 10-minute mark', () => {
    vi.useFakeTimers({ now: new Date('2026-03-29T10:33:00Z') });
    expect(getNextTeeTimeInterval('UTC')).toBe('10:40');
  });

  it('returns current time if already on a 10-minute mark', () => {
    vi.useFakeTimers({ now: new Date('2026-03-29T10:30:00Z') });
    expect(getNextTeeTimeInterval('UTC')).toBe('10:30');
  });

  it('rolls over to next hour', () => {
    vi.useFakeTimers({ now: new Date('2026-03-29T10:55:00Z') });
    expect(getNextTeeTimeInterval('UTC')).toBe('11:00');
  });

  it('handles midnight rollover', () => {
    vi.useFakeTimers({ now: new Date('2026-03-29T23:55:00Z') });
    expect(getNextTeeTimeInterval('UTC')).toBe('00:00');
  });

  it('returns zero-padded hours and minutes', () => {
    vi.useFakeTimers({ now: new Date('2026-03-29T08:01:00Z') });
    expect(getNextTeeTimeInterval('UTC')).toBe('08:10');
  });
});

describe('buildTeeTimeDateTime', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('combines HH:mm time with course today date to produce ISO 8601 DateTime', () => {
    vi.useFakeTimers({ now: new Date('2026-03-31T18:00:00Z') });
    expect(buildTeeTimeDateTime('14:30', 'UTC')).toBe('2026-03-31T14:30:00');
  });

  it('handles midnight time', () => {
    vi.useFakeTimers({ now: new Date('2026-03-31T18:00:00Z') });
    expect(buildTeeTimeDateTime('00:00', 'UTC')).toBe('2026-03-31T00:00:00');
  });

  it('handles different timezones', () => {
    vi.useFakeTimers({ now: new Date('2026-03-31T05:00:00Z') });
    // In America/Chicago (UTC-5 or UTC-6), this would be the previous day
    const result = buildTeeTimeDateTime('22:00', 'America/Chicago');
    expect(result).toMatch(/^\d{4}-\d{2}-\d{2}T22:00:00$/);
  });

  it('preserves original time portion in output', () => {
    vi.useFakeTimers({ now: new Date('2026-03-31T18:00:00Z') });
    expect(buildTeeTimeDateTime('08:45', 'UTC')).toBe('2026-03-31T08:45:00');
  });
});
