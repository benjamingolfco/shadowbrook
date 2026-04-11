import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import Schedule from '../manage/pages/Schedule';
import { useWeeklySchedule } from '../manage/hooks/useWeeklySchedule';
import { useBulkDraft } from '../manage/hooks/useBulkDraft';
import { useTeeTimeSettings } from '../manage/hooks/useTeeTimeSettings';

vi.mock('../hooks/useCourseId', () => ({
  useCourseId: () => 'course-1',
}));
vi.mock('../manage/hooks/useWeeklySchedule');
vi.mock('../manage/hooks/useBulkDraft');
vi.mock('../manage/hooks/useTeeTimeSettings');

const mockUseWeeklySchedule = vi.mocked(useWeeklySchedule);
const mockUseBulkDraft = vi.mocked(useBulkDraft);
const mockUseTeeTimeSettings = vi.mocked(useTeeTimeSettings);

const weekData = {
  weekStart: '2026-04-13',
  weekEnd: '2026-04-19',
  days: [
    { date: '2026-04-13', status: 'notStarted' as const },
    { date: '2026-04-14', status: 'draft' as const, teeSheetId: 'abc', intervalCount: 72 },
    { date: '2026-04-15', status: 'published' as const, teeSheetId: 'def', intervalCount: 72 },
    { date: '2026-04-16', status: 'notStarted' as const },
    { date: '2026-04-17', status: 'notStarted' as const },
    { date: '2026-04-18', status: 'notStarted' as const },
    { date: '2026-04-19', status: 'notStarted' as const },
  ],
};

const mockDraftMutate = vi.fn();

function defaultMocks() {
  mockUseWeeklySchedule.mockReturnValue({
    data: weekData,
    isLoading: false,
  } as unknown as ReturnType<typeof useWeeklySchedule>);

  mockUseBulkDraft.mockReturnValue({
    mutate: mockDraftMutate,
    isPending: false,
  } as unknown as ReturnType<typeof useBulkDraft>);

  mockUseTeeTimeSettings.mockReturnValue({
    data: { teeTimeIntervalMinutes: 10, firstTeeTime: '07:00', lastTeeTime: '18:00', defaultCapacity: 4 },
  } as unknown as ReturnType<typeof useTeeTimeSettings>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultMocks();
});

describe('Schedule', () => {
  it('renders 7 day cards with day labels', () => {
    render(<Schedule />);

    expect(screen.getByText('Mon, Apr 13')).toBeInTheDocument();
    expect(screen.getByText('Tue, Apr 14')).toBeInTheDocument();
    expect(screen.getByText('Wed, Apr 15')).toBeInTheDocument();
    expect(screen.getByText('Thu, Apr 16')).toBeInTheDocument();
    expect(screen.getByText('Fri, Apr 17')).toBeInTheDocument();
    expect(screen.getByText('Sat, Apr 18')).toBeInTheDocument();
    expect(screen.getByText('Sun, Apr 19')).toBeInTheDocument();
  });

  it('shows correct status badges', () => {
    render(<Schedule />);

    const notStartedBadges = screen.getAllByText('Not Started');
    expect(notStartedBadges).toHaveLength(5);

    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByText('Published')).toBeInTheDocument();
  });

  it('shows interval count for drafted and published days', () => {
    render(<Schedule />);

    const intervalLabels = screen.getAllByText('72 intervals');
    expect(intervalLabels).toHaveLength(2);
  });

  it('shows checkboxes only on Not Started cards', () => {
    render(<Schedule />);

    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes).toHaveLength(5);
  });

  it('enables Draft Selected button when a checkbox is checked', () => {
    render(<Schedule />);

    const draftBtn = screen.getByRole('button', { name: 'Draft Selected' });
    expect(draftBtn).toBeDisabled();

    const checkbox = screen.getByLabelText('Select Mon, Apr 13');
    fireEvent.click(checkbox);

    expect(draftBtn).toBeEnabled();
  });

  it('calls bulk draft mutation with selected dates', () => {
    render(<Schedule />);

    fireEvent.click(screen.getByLabelText('Select Mon, Apr 13'));
    fireEvent.click(screen.getByLabelText('Select Thu, Apr 16'));
    fireEvent.click(screen.getByRole('button', { name: 'Draft Selected' }));

    expect(mockDraftMutate).toHaveBeenCalledWith(
      { courseId: 'course-1', dates: expect.arrayContaining(['2026-04-13', '2026-04-16']) },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    );
  });

  it('shows not-configured message when settings are missing', () => {
    mockUseTeeTimeSettings.mockReturnValue({
      data: undefined,
    } as unknown as ReturnType<typeof useTeeTimeSettings>);

    render(<Schedule />);

    expect(screen.getByText(/Schedule defaults are not configured/)).toBeInTheDocument();
    expect(screen.getByText('Configure settings')).toBeInTheDocument();
  });
});
