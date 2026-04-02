import { useState } from 'react';
import { useOrganizations } from '../hooks/useOrganizations';
import { useSetOrgFeatures } from '../hooks/useFeatureFlags';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { Organization } from '@/types/organization';

const FEATURE_KEYS = ['sms-notifications', 'dynamic-pricing', 'full-operator-app'] as const;
type FeatureKey = (typeof FEATURE_KEYS)[number];

type OrgFlags = Record<string, Record<FeatureKey, boolean>>;

export default function FeatureFlags() {
  const { data: organizations, isLoading, error } = useOrganizations();
  const setOrgFeatures = useSetOrgFeatures();

  // Local state: orgId -> featureKey -> boolean
  const [flags, setFlags] = useState<OrgFlags>({});

  function getFlag(orgId: string, key: FeatureKey): boolean {
    return flags[orgId]?.[key] ?? false;
  }

  function handleToggle(orgId: string, key: FeatureKey, value: boolean) {
    const current = flags[orgId] ?? ({} as Record<FeatureKey, boolean>);
    const updated = { ...current, [key]: value };
    setFlags((prev) => ({ ...prev, [orgId]: updated }));
    setOrgFeatures.mutate({ orgId, flags: updated });
  }

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading organizations...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load organizations'}
        </p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Feature Flags</h1>
        <p className="text-sm text-muted-foreground">Manage feature availability per organization</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Organization Features</CardTitle>
        </CardHeader>
        <CardContent>
          {!organizations || organizations.length === 0 ? (
            <p className="text-muted-foreground">No organizations found.</p>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Organization</TableHead>
                    {FEATURE_KEYS.map((key) => (
                      <TableHead key={key} className="whitespace-nowrap">
                        {key}
                      </TableHead>
                    ))}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {organizations.map((org: Organization) => (
                    <TableRow key={org.id}>
                      <TableCell className="font-medium">{org.name}</TableCell>
                      {FEATURE_KEYS.map((key) => (
                        <TableCell key={key}>
                          <Switch
                            checked={getFlag(org.id, key)}
                            onCheckedChange={(checked) => handleToggle(org.id, key, checked)}
                            aria-label={`${key} for ${org.name}`}
                          />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
