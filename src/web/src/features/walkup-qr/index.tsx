import { Routes, Route } from 'react-router';
import WalkUpLandingPage from './pages/WalkUpLandingPage';

export default function WalkUpQrFeature() {
  return (
    <Routes>
      <Route path=":shortCode" element={<WalkUpLandingPage />} />
    </Routes>
  );
}
