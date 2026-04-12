import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import ScheduleDay from '../manage/pages/ScheduleDay';
import { useTeeSheet } from '../pos/hooks/useTeeSheet';
import { useUnpublishTeeSheet } from '../manage/hooks/useUnpublishTeeSheet';
import { useBookingCount } from '../manage/hooks/useBookingCount';

vi.mock('../hooks/useCourseId', () => ({
  useCourseId: () => 'course-1',
}));
vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useParams: () => ({ date: '2026-04-14' }),
  };
});
vi.mock('../pos/hooks/useTeeSheet');
vi.mock('../manage/hooks/useUnpublishTeeSheet');
vi.mock('../manage/hooks/useBookingCount');

const mockUseTeeSheet = vi.mocked(useTeeSheet);
const mockUseUnpublishTeeSheet = vi.mocked(useUnpublishTeeSheet);
const mockUseBookingCount = vi.mocked(useBookingCount);

const teeSheetData = {
  courseId: 'course-1',
  courseName: 'Test Course',
  status: 'published',
  slots: [
    { teeTime: '2026-04-14T07:00:00', status: 'open', golferName: null, playerCount: 4 },
    { teeTime: '2026-04-14T07:10:00', status: 'booked', golferName: 'John Doe', playerCount: 2 },
  ],
};

const mockUnpublishMutate = vi.fn();

function defaultMocks() {
  mockUseTeeSheet.mockReturnValue({
    data: teeSheetData,
    isLoading: false,
    isError: false,
  } as unknown as ReturnType<typeof useTeeSheet>);

  mockUseUnpublishTeeSheet.mockReturnValue({
    mutate: mockUnpublishMutate,
    isPending: false,
  } as unknown as ReturnType<typeof useUnpublishTeeSheet>);

  mockUseBookingCount.mockReturnValue({
    data: undefined,
    refetch: vi.fn().mockResolvedValue({ data: { count: 0 } }),
  } as unknown as ReturnType<typeof useBookingCount>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultMocks();
});

describe('ScheduleDay', () => {
  it('shows Published status badge when status is published', () => {
    render(<ScheduleDay />);

    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('shows Draft status badge when status is draft', () => {
    mockUseTeeSheet.mockReturnValue({
      data: { ...teeSheetData, status: 'draft' },
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useTeeSheet>);

    render(<ScheduleDay />);

    expect(screen.getByText('Draft')).toBeInTheDocument();
  });

  it('shows Unpublish button only when status is published', () => {
    render(<ScheduleDay />);

    expect(screen.getByRole('button', { name: 'Unpublish' })).toBeInTheDocument();
  });

  it('hides Unpublish button when status is draft', () => {
    mockUseTeeSheet.mockReturnValue({
      data: { ...teeSheetData, status: 'draft' },
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useTeeSheet>);

    render(<ScheduleDay />);

    expect(screen.queryByRole('button', { name: 'Unpublish' })).not.toBeInTheDocument();
  });

  it('calls unpublish immediately when booking count is 0', async () => {
    mockUseBookingCount.mockReturnValue({
      data: undefined,
      refetch: vi.fn().mockResolvedValue({ data: { count: 0 } }),
    } as unknown as ReturnType<typeof useBookingCount>);

    render(<ScheduleDay />);

    fireEvent.click(screen.getByRole('button', { name: 'Unpublish' }));

    await waitFor(() => {
      expect(mockUnpublishMutate).toHaveBeenCalledWith(
        { courseId: 'course-1', date: '2026-04-14' },
        expect.any(Object),
      );
    });
  });

  it('shows dialog when booking count is greater than 0', async () => {
    mockUseBookingCount.mockReturnValue({
      data: undefined,
      refetch: vi.fn().mockResolvedValue({ data: { count: 5 } }),
    } as unknown as ReturnType<typeof useBookingCount>);

    render(<ScheduleDay />);

    fireEvent.click(screen.getByRole('button', { name: 'Unpublish' }));

    await waitFor(() => {
      expect(screen.getByText(/5 booking\(s\) will be cancelled/)).toBeInTheDocument();
    });
  });

  it('displays dynamic player count instead of hardcoded value', () => {
    render(<ScheduleDay />);

    expect(screen.getByText('4 players')).toBeInTheDocument();
    expect(screen.getByText('2 players')).toBeInTheDocument();
  });
});
