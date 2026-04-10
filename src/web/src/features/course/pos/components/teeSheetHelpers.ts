import type { StatusBadgeStatus } from '@/components/ui/status-badge';

/**
 * Maps the tee sheet API's slot.status enum to the visual StatusBadge variants.
 * The API currently only emits 'booked' and 'open'; other variants are defined
 * in StatusBadge for future use but unreachable from this mapper today.
 */
export function mapTeeTimeStatus(status: string): StatusBadgeStatus {
  switch (status) {
    case 'booked':
      return 'booked';
    case 'open':
    default:
      return 'open';
  }
}

/**
 * Derives initials from a name. Returns up to two uppercase characters.
 * Returns '?' if the name is empty.
 */
export function getInitials(name: string | null | undefined): string {
  if (!name || !name.trim()) return '?';
  return name
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}
