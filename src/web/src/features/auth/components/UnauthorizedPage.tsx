import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '../hooks/useAuth';

export default function UnauthorizedPage() {
  const { logout } = useAuth();

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-md text-center">
        <CardHeader>
          <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full bg-destructive/10">
            <ShieldAlert className="h-6 w-6 text-destructive" />
          </div>
          <CardTitle>Access Denied</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            Your identity was verified, but you don't have an account in the
            system. Please contact your administrator to get access.
          </p>
        </CardContent>
        <CardFooter className="justify-center">
          <Button variant="outline" onClick={logout}>
            Sign out
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
