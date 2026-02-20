import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';
import { useCourses } from '@/features/operator/hooks/useTeeTimeSettings';
import { useCourseContext } from '../context/CourseContext';
import { useTenantContext } from '../context/TenantContext';
import { Card, CardHeader, CardTitle, CardDescription, CardAction } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import type { Course } from '@/types/course';

function formatLocation(course: Course): string {
  const parts = [course.city, course.state].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : 'Location not set';
}

export default function CoursePortfolio() {
  const navigate = useNavigate();
  const { tenant } = useTenantContext();
  const { selectCourse } = useCourseContext();
  const coursesQuery = useCourses();
  const hasAutoSelected = useRef(false);

  useEffect(() => {
    if (
      !coursesQuery.isLoading &&
      coursesQuery.data &&
      coursesQuery.data.length === 1 &&
      !hasAutoSelected.current
    ) {
      hasAutoSelected.current = true;
      const course = coursesQuery.data[0];
      if (course) {
        selectCourse({ id: course.id, name: course.name });
      }
    }
  }, [coursesQuery.isLoading, coursesQuery.data, selectCourse]);

  function handleSelectCourse(course: Course) {
    selectCourse({ id: course.id, name: course.name });
  }

  if (coursesQuery.isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="w-full max-w-3xl space-y-6 p-8">
          <h1 className="text-2xl font-bold">Select a Course</h1>
          <p className="text-muted-foreground">Showing courses for {tenant?.organizationName}.</p>
          <div className="space-y-3" aria-busy="true" aria-label="Loading courses">
            {[1, 2, 3].map((i) => (
              <Card key={i}>
                <CardHeader>
                  <Skeleton className="h-5 w-48" />
                  <Skeleton className="h-4 w-32" />
                </CardHeader>
              </Card>
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (coursesQuery.isError) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="w-full max-w-3xl space-y-6 p-8">
          <h1 className="text-2xl font-bold">Select a Course</h1>
          <p className="text-destructive">Error loading courses: {coursesQuery.error.message}</p>
          <Button variant="outline" onClick={() => void coursesQuery.refetch()}>
            Retry
          </Button>
        </div>
      </div>
    );
  }

  if (!coursesQuery.data || coursesQuery.data.length === 0) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="w-full max-w-3xl space-y-6 p-8 text-center">
          <h1 className="text-2xl font-bold">Select a Course</h1>
          <p className="text-base font-medium">Get started by adding your first course</p>
          <p className="text-sm text-muted-foreground">
            Once a course is registered, you can manage tee sheets, settings, and bookings.
          </p>
          <Button autoFocus onClick={() => navigate('/operator/register-course')}>
            Register a Course
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen items-center justify-center">
      <div className="w-full max-w-3xl space-y-6 p-8">
        <h1 className="text-2xl font-bold">Select a Course</h1>
        <p className="text-muted-foreground">Showing courses for {tenant?.organizationName}.</p>
        <div className="space-y-3">
          {coursesQuery.data.map((course) => (
            <Card
              key={course.id}
              role="button"
              tabIndex={0}
              aria-label={`Manage ${course.name}, ${formatLocation(course)}`}
              className="cursor-pointer hover:bg-muted/50 transition-colors"
              onClick={() => handleSelectCourse(course)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  handleSelectCourse(course);
                }
              }}
            >
              <CardHeader>
                <CardTitle className="text-base">{course.name}</CardTitle>
                <CardDescription>{formatLocation(course)}</CardDescription>
                {/* TODO: Derive status from settings completeness in future story */}
                <Badge variant="success">Active</Badge>
                <CardAction>
                  <Button variant="outline" tabIndex={-1} aria-hidden={true}>
                    Manage
                  </Button>
                </CardAction>
              </CardHeader>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
