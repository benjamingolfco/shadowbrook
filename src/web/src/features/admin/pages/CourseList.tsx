import { Link } from 'react-router';
import { useCourses } from '../hooks/useCourses';
import { Button } from '@/components/ui/button';
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
  const { data: courses, isLoading, error } = useCourses();

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading courses...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load courses'}
        </p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">All Registered Courses</h1>
          <p className="text-sm text-muted-foreground">Platform Admin View</p>
        </div>
        <Button asChild>
          <Link to="/admin/courses/new">Register Course</Link>
        </Button>
      </div>

      {!courses || courses.length === 0 ? (
        <p className="text-muted-foreground">No courses registered yet.</p>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Tenant</TableHead>
                <TableHead className="hidden md:table-cell">Location</TableHead>
                <TableHead className="hidden md:table-cell">Contact</TableHead>
                <TableHead className="hidden md:table-cell">Registered</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {courses.map((course: Course) => (
                <TableRow key={course.id}>
                  <TableCell>
                    <div className="font-semibold">{course.name}</div>
                    <div className="md:hidden text-sm text-muted-foreground">
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
                  <TableCell className="hidden md:table-cell text-sm">
                    {new Date(course.createdAt).toLocaleDateString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
