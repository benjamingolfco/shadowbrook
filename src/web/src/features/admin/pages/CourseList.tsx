import { Link, useNavigate } from 'react-router';
import { useCourses } from '../hooks/useCourses';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { Course } from '@/types/course';

export default function CourseList() {
  const navigate = useNavigate();
  const { data: courses, isLoading, error } = useCourses();

  const topbar = (
    <PageTopbar
      middle={<h1 className="font-[family-name:var(--font-heading)] text-[18px] text-ink">All Registered Courses</h1>}
      right={
        <Button asChild>
          <Link to="/admin/courses/new">Register Course</Link>
        </Button>
      }
    />
  );

  if (isLoading) {
    return (
      <>
        {topbar}
        <p className="text-ink-muted">Loading courses...</p>
      </>
    );
  }

  if (error) {
    return (
      <>
        {topbar}
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load courses'}
        </p>
      </>
    );
  }

  return (
    <>
      {topbar}

      {!courses || courses.length === 0 ? (
        <p className="text-ink-muted text-sm py-12 text-center">No courses registered yet.</p>
      ) : (
        <div className="border border-border-strong rounded-md bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Organization</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Location</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Contact</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Registered</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {courses.map((course: Course) => (
                <TableRow
                  key={course.id}
                  className="cursor-pointer"
                  onClick={() => void navigate(`/admin/courses/${course.id}`)}
                >
                  <TableCell>
                    <div className="font-medium">{course.name}</div>
                    <div className="md:hidden text-sm text-ink-muted">
                      {course.tenantName || '—'}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    {course.tenantName || '—'}
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div className="space-y-0.5">
                      {course.streetAddress && (
                        <div className="text-sm">{course.streetAddress}</div>
                      )}
                      {(course.city || course.state || course.zipCode) && (
                        <div className="text-sm">
                          {course.city}
                          {course.city && course.state ? ', ' : ''}
                          {course.state} {course.zipCode}
                        </div>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div className="space-y-0.5">
                      {course.contactEmail && (
                        <div className="text-sm">{course.contactEmail}</div>
                      )}
                      {course.contactPhone && (
                        <div className="text-sm">{course.contactPhone}</div>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                    {new Date(course.createdAt).toLocaleDateString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
