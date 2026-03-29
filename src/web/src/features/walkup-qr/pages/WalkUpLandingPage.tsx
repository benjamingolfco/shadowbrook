import { useParams, Navigate } from 'react-router';

export default function WalkUpLandingPage() {
  const { shortCode } = useParams<{ shortCode: string }>();
  return <Navigate to={`/join/${shortCode}`} replace />;
}
