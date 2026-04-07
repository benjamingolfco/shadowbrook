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
import { PageTopbar } from '@/components/layout/PageTopbar';
import type { Organization } from '@/types/organization';

const FEATURE_KEYS = ['sms-notifications', 'dynamic-pricing', 'full-operator-app'] as const;
type FeatureKey = (typeof FEATURE_KEYS)[number];

const FEATURE_LABELS: Record<FeatureKey, string> = {
  'sms-notifications': 'SMS Notifications',
  'dynamic-pricing': 'Dynamic Pricing',
  'full-operator-app': 'Full Operator App',
};

type OrgFlags = Record<string, Record<FeatureKey, boolean>>;

export default function FeatureFlags() {
  const { data: organizations, isLoading, error } = useOrganizations();
  const setOrgFeatures = useSetOrgFeatures();

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

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Feature Flags</h1>}
      />

      <Card className="border-border-strong">
        <CardHeader>
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Organization Features
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <p className="text-ink-muted text-sm py-12 text-center">Loading organizations...</p>
          ) : error ? (
            <p className="text-destructive text-sm py-12 text-center">
              Error: {error instanceof Error ? error.message : 'Failed to load organizations'}
            </p>
          ) : !organizations || organizations.length === 0 ? (
            <p className="text-ink-muted text-sm py-12 text-center">No organizations found.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="bg-canvas">
                  <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">
                    Organization
                  </TableHead>
                  {FEATURE_KEYS.map((key) => (
                    <TableHead
                      key={key}
                      className="text-[10px] uppercase tracking-wider text-ink-muted whitespace-nowrap"
                    >
                      {FEATURE_LABELS[key]}
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
          )}
        </CardContent>
      </Card>
    </>
  );
}
