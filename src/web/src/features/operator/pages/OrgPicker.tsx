import { useAuth } from '@/features/auth';
import { useOrgContext } from '../context/OrgContext';
import { Card, CardContent } from '@/components/ui/card';
import { PageTopbar } from '@/components/layout/PageTopbar';

export default function OrgPicker() {
  const { organizations } = useAuth();
  const { selectOrg } = useOrgContext();

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Select an Organization</h1>}
      />

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {organizations.map((org) => (
          <Card
            key={org.id}
            className="border-border-strong cursor-pointer hover:bg-canvas transition-colors"
            onClick={() => selectOrg({ id: org.id, name: org.name })}
          >
            <CardContent className="p-4">
              <span className="font-medium">{org.name}</span>
            </CardContent>
          </Card>
        ))}
      </div>
    </>
  );
}
