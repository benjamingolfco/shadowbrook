import { useAuth } from '@/features/auth';
import { useOrgContext } from '../context/OrgContext';
import { Card, CardContent } from '@/components/ui/card';

export default function OrgPicker() {
  const { organizations } = useAuth();
  const { selectOrg } = useOrgContext();

  return (
    <div className="p-6">
      <h2 className="text-lg font-semibold mb-4">Select an organization</h2>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {organizations.map((org) => (
          <Card
            key={org.id}
            className="cursor-pointer transition-colors hover:bg-accent"
            onClick={() => selectOrg({ id: org.id, name: org.name })}
          >
            <CardContent className="p-4">
              <span className="font-medium">{org.name}</span>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
