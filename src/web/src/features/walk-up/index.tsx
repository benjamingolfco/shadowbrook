import { Routes, Route } from 'react-router';
import WalkUpOfferPage from './pages/WalkUpOfferPage';

export default function WalkUpFeature() {
  return (
    <Routes>
      <Route path=":token" element={<WalkUpOfferPage />} />
    </Routes>
  );
}
