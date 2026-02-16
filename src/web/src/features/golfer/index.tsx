import { Routes, Route, Navigate } from 'react-router';
import GolferLayout from '@/components/layout/GolferLayout';
import BrowseTeeTimes from './pages/BrowseTeeTimes';
import MyBookings from './pages/MyBookings';
import Profile from './pages/Profile';

export default function GolferFeature() {
  return (
    <Routes>
      <Route element={<GolferLayout />}>
        <Route path="tee-times" element={<BrowseTeeTimes />} />
        <Route path="bookings" element={<MyBookings />} />
        <Route path="profile" element={<Profile />} />
        <Route path="*" element={<Navigate to="tee-times" replace />} />
      </Route>
    </Routes>
  );
}
