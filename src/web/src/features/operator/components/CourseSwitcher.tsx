import { useState, useEffect, useRef } from 'react';
import { NavLink } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { FlagIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { useCourseContext } from '../context/CourseContext';
import { useTenantContext } from '../context/TenantContext';
import { useCourses } from '../hooks/useCourses';

export default function CourseSwitcher() {
  const { tenant } = useTenantContext();
  const { course, selectCourse, clearCourse, isDirty } = useCourseContext();
  const queryClient = useQueryClient();
  const coursesQuery = useCourses(tenant?.id);

  const [pendingCourse, setPendingCourse] = useState<{ id: string; name: string } | null>(null);

  const hasAutoSelected = useRef(false);

  // Auto-select when there is exactly one course and none is currently selected
  useEffect(() => {
    if (
      !coursesQuery.isLoading &&
      coursesQuery.data &&
      coursesQuery.data.length === 1 &&
      !course &&
      !hasAutoSelected.current
    ) {
      hasAutoSelected.current = true;
      const singleCourse = coursesQuery.data[0];
      if (singleCourse) {
        selectCourse({ id: singleCourse.id, name: singleCourse.name });
      }
    }
  }, [coursesQuery.isLoading, coursesQuery.data, course, selectCourse]);

  // Clear course if it's no longer in the list (access revoked)
  useEffect(() => {
    if (
      !coursesQuery.isLoading &&
      coursesQuery.data &&
      course &&
      !coursesQuery.data.some((c) => c.id === course.id)
    ) {
      clearCourse();
    }
  }, [coursesQuery.isLoading, coursesQuery.data, course, clearCourse]);

  const sortedCourses = [...(coursesQuery.data ?? [])].sort((a, b) =>
    a.name.localeCompare(b.name)
  );

  function handleCourseChange(courseId: string) {
    const target = coursesQuery.data?.find((c) => c.id === courseId);
    if (!target) return;
    if (target.id === course?.id) return;

    if (isDirty) {
      setPendingCourse({ id: target.id, name: target.name });
      return;
    }

    performSwitch({ id: target.id, name: target.name });
  }

  function performSwitch(newCourse: { id: string; name: string }) {
    queryClient.removeQueries({ queryKey: ['tee-sheets'] });
    queryClient.removeQueries({ queryKey: ['courses', course?.id] });

    selectCourse(newCourse);
    setPendingCourse(null);
  }

  function handleConfirmSwitch() {
    if (pendingCourse) {
      performSwitch(pendingCourse);
    }
  }

  function handleCancelSwitch() {
    setPendingCourse(null);
  }

  if (coursesQuery.isLoading) {
    return <Skeleton className="h-8 w-full" />;
  }

  if (coursesQuery.isError) {
    return (
      <div className="space-y-1">
        <p className="text-xs text-destructive">Failed to load courses</p>
        <Button variant="ghost" size="sm" onClick={() => void coursesQuery.refetch()}>
          Retry
        </Button>
      </div>
    );
  }

  if (coursesQuery.data?.length === 0) {
    return (
      <div className="space-y-1">
        <p className="text-sm text-muted-foreground">No courses available</p>
        <Button variant="link" size="sm" asChild>
          <NavLink to="/operator/register-course">Register Course</NavLink>
        </Button>
      </div>
    );
  }

  if (coursesQuery.data?.length === 1) {
    return (
      <p className="text-sm text-muted-foreground truncate" title={course?.name}>
        {course?.name}
      </p>
    );
  }

  return (
    <>
      <Select
        value={course?.id ?? ''}
        onValueChange={handleCourseChange}
      >
        <SelectTrigger
          className="w-full"
          aria-label="Switch course"
        >
          <SelectValue placeholder="Select a course">
            {course && (
              <span className="flex items-center gap-2">
                <FlagIcon className="size-4" />
                <span className="truncate">{course.name}</span>
              </span>
            )}
          </SelectValue>
        </SelectTrigger>
        <SelectContent>
          {sortedCourses.map((c) => (
            <SelectItem key={c.id} value={c.id}>
              {c.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <AlertDialog open={pendingCourse !== null} onOpenChange={(open) => { if (!open) handleCancelSwitch(); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Switch Course?</AlertDialogTitle>
            <AlertDialogDescription>
              You have unsaved changes. Switching courses will discard them.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel autoFocus>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmSwitch}
              className="bg-destructive text-white hover:bg-destructive/90"
            >
              Switch Anyway
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
