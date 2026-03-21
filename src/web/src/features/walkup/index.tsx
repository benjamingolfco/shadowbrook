import { Routes, Route } from 'react-router';
import WalkupJoinPage from './pages/WalkupJoinPage';

export default function WalkupFeature() {
  return (
    <Routes>
      <Route index element={<WalkupJoinPage />} />
      <Route path=":shortCode" element={<WalkupJoinPage />} />
    </Routes>
  );
}
