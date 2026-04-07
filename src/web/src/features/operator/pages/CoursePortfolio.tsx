import { useEffect, useRef } from 'react';
import { useCourses } from '@/features/operator/hooks/useCourses';
import { useCourseContext } from '../context/CourseContext';
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { PageTopbar } from '@/components/layout/PageTopbar';
import type { Course } from '@/types/course';

function formatLocation(course: Course): string {
  const parts = [course.city, course.state].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : 'Location not set';
}

export default function CoursePortfolio() {
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
        selectCourse({ id: course.id, name: course.name, timeZoneId: course.timeZoneId });
      }
    }
  }, [coursesQuery.isLoading, coursesQuery.data, selectCourse]);

  function handleSelectCourse(course: Course) {
    selectCourse({ id: course.id, name: course.name, timeZoneId: course.timeZoneId });
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Select a Course</h1>}
      />

      <div className="max-w-3xl">
        {coursesQuery.isLoading && (
          <div className="space-y-3" aria-busy="true" aria-label="Loading courses">
            {[1, 2, 3].map((i) => (
              <Card key={i} className="border-border-strong">
                <CardHeader>
                  <Skeleton className="h-5 w-48" />
                  <Skeleton className="h-4 w-32" />
                </CardHeader>
              </Card>
            ))}
          </div>
        )}

        {coursesQuery.isError && (
          <div className="space-y-4">
            <p className="text-destructive text-sm">
              Error loading courses: {coursesQuery.error.message}
            </p>
            <Button variant="outline" onClick={() => void coursesQuery.refetch()}>
              Retry
            </Button>
          </div>
        )}

        {!coursesQuery.isLoading && !coursesQuery.isError && coursesQuery.data?.length === 0 && (
          <p className="text-ink-muted text-sm py-12 text-center">
            No courses available. Contact your administrator to add a course.
          </p>
        )}

        {coursesQuery.data && coursesQuery.data.length > 0 && (
          <div className="space-y-3">
            {coursesQuery.data.map((course) => (
              <Card
                key={course.id}
                role="button"
                tabIndex={0}
                aria-label={`Manage ${course.name}, ${formatLocation(course)}`}
                className="border-border-strong cursor-pointer hover:bg-canvas transition-colors"
                onClick={() => handleSelectCourse(course)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    handleSelectCourse(course);
                  }
                }}
              >
                <CardHeader>
                  <CardTitle>{course.name}</CardTitle>
                  <CardDescription>{formatLocation(course)}</CardDescription>
                  <Badge variant="success" className="w-fit">
                    Active
                  </Badge>
                </CardHeader>
              </Card>
            ))}
          </div>
        )}
      </div>
    </>
  );
}
