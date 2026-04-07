import { useCallback } from 'react';
import type { ComponentProps } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/features/auth';
import { useCourseContext } from '../context/CourseContext';
import { OperatorBrand, WaitlistBrand, operatorNav } from '../navigation';
import { AppShell } from '@/components/layout/AppShell';

type Variant = 'full' | 'minimal';

type ShellProps = Pick<
  ComponentProps<typeof AppShell>,
  'variant' | 'brand' | 'onSwitchCourse' | 'navConfig'
>;

/**
 * Returns the prop shape that the operator feature passes to <AppShell>.
 *
 * Used by all five operator route branches in `features/operator/index.tsx`.
 * The hook centralizes the brand selection (full vs minimal variant get
 * different brand components) and the switch-course callback, both of which
 * used to live in the deleted `OperatorLayout.tsx` and `WaitlistShellLayout.tsx`
 * shims.
 */
export function useOperatorShellProps(variant: Variant): ShellProps {
  const { user } = useAuth();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  return {
    variant,
    brand: variant === 'full' ? <OperatorBrand /> : <WaitlistBrand />,
    navConfig: variant === 'full' ? operatorNav : undefined,
    onSwitchCourse: showSwitchCourse ? handleSwitchCourse : undefined,
  };
}
