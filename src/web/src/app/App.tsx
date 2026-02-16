import { RouterProvider } from 'react-router';
import { Providers } from './providers';
import { router } from './router';
import { DevRoleSwitcher } from '@/features/auth';

export default function App() {
  return (
    <Providers>
      <RouterProvider router={router} />
      <DevRoleSwitcher />
    </Providers>
  );
}
