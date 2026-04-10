import { useParams } from 'react-router';

export function useCourseId(): string {
  const { courseId } = useParams<{ courseId: string }>();
  if (!courseId) {
    throw new Error('useCourseId must be used within a route that has :courseId param');
  }
  return courseId;
}
