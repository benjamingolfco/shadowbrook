import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle, CardAction } from '@/components/ui/card';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { cn } from '@/lib/utils';

const schema = z.object({
  minPrice: z.number().positive('Min price must be greater than zero'),
  maxPrice: z.number().positive('Max price must be greater than zero'),
}).refine((data) => data.minPrice < data.maxPrice, {
  message: 'Min price must be less than max price',
  path: ['maxPrice'],
});

type FormData = z.infer<typeof schema>;

interface PriceBoundsCardProps {
  minPrice: number | null;
  maxPrice: number | null;
  onSave: (min: number, max: number) => void;
  isPending: boolean;
  initialEdit?: boolean;
}

export function PriceBoundsCard({ minPrice, maxPrice, onSave, isPending, initialEdit }: PriceBoundsCardProps) {
  const hasValue = minPrice !== null && maxPrice !== null;
  const [editing, setEditing] = useState(initialEdit ?? !hasValue);

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      minPrice: minPrice ?? undefined as unknown as number,
      maxPrice: maxPrice ?? undefined as unknown as number,
    },
  });

  function onSubmit(data: FormData) {
    onSave(data.minPrice, data.maxPrice);
    setEditing(false);
  }

  function onCancel() {
    form.reset({
      minPrice: minPrice ?? undefined as unknown as number,
      maxPrice: maxPrice ?? undefined as unknown as number,
    });
    setEditing(false);
  }

  return (
    <Card className={cn(editing && 'border-primary')}>
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          Price Bounds
        </CardTitle>
        {hasValue && !editing && (
          <CardAction>
            <Button variant="ghost" size="sm" onClick={() => setEditing(true)}>
              <Pencil className="h-3.5 w-3.5" />
              Edit
            </Button>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        {editing ? (
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <FormField
                  control={form.control}
                  name="minPrice"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Minimum ($)</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          step="0.01"
                          min={0.01}
                          placeholder="0.00"
                          {...field}
                          onChange={(e) => field.onChange(e.target.valueAsNumber)}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="maxPrice"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Maximum ($)</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          step="0.01"
                          min={0.01}
                          placeholder="0.00"
                          {...field}
                          onChange={(e) => field.onChange(e.target.valueAsNumber)}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
              <div className="flex gap-2">
                <Button type="submit" disabled={isPending}>
                  {isPending ? 'Saving...' : 'Save'}
                </Button>
                {hasValue && (
                  <Button type="button" variant="outline" onClick={onCancel} disabled={isPending}>
                    Cancel
                  </Button>
                )}
              </div>
            </form>
          </Form>
        ) : (
          <div className="flex gap-8">
            <div>
              <p className="text-xs text-muted-foreground">Minimum</p>
              <p className="text-2xl font-semibold text-ink">${minPrice?.toFixed(2)}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Maximum</p>
              <p className="text-2xl font-semibold text-ink">${maxPrice?.toFixed(2)}</p>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
