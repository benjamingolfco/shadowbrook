import type { StatusBadgeStatus } from '@/components/ui/status-badge';

/**
 * Maps the openings API's status enum to the visual StatusBadge variants.
 * The default fallback is 'expired' (faded/muted) so an unknown server-side
 * status surfaces visually rather than silently rendering as Open.
 */
export function mapOpeningStatus(status: string): StatusBadgeStatus {
  switch (status) {
    case 'Open':
      return 'open';
    case 'Filled':
      return 'filled';
    case 'Expired':
      return 'expired';
    case 'Cancelled':
      return 'cancelled';
    default:
      return 'expired';
  }
}

/**
 * Renders a list of filled golfers as a single comma-separated string,
 * appending the group size in parentheses for groups larger than one.
 *
 * Moved out of the deleted OpeningsList component.
 */
export function formatGolferNames(
  golfers: { golferName: string; groupSize: number }[],
): string {
  if (golfers.length === 0) return '';
  return golfers
    .map((g) => (g.groupSize > 1 ? `${g.golferName} (×${g.groupSize})` : g.golferName))
    .join(', ');
}
