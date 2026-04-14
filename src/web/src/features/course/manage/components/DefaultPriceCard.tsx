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
  defaultPrice: z.number().positive('Price must be greater than zero'),
});

type FormData = z.infer<typeof schema>;

interface DefaultPriceCardProps {
  defaultPrice: number | null;
  minPrice: number | null;
  maxPrice: number | null;
  onSave: (price: number) => void;
  isPending: boolean;
}

export function DefaultPriceCard({ defaultPrice, minPrice, maxPrice, onSave, isPending }: DefaultPriceCardProps) {
  const hasValue = defaultPrice !== null;
  const [editing, setEditing] = useState(!hasValue);

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { defaultPrice: defaultPrice ?? undefined as unknown as number },
  });

  function onSubmit(data: FormData) {
    onSave(data.defaultPrice);
    setEditing(false);
  }

  function onCancel() {
    form.reset({ defaultPrice: defaultPrice ?? undefined as unknown as number });
    setEditing(false);
  }

  const boundsHint = minPrice !== null && maxPrice !== null
    ? `Must be between $${minPrice.toFixed(2)} and $${maxPrice.toFixed(2)}`
    : null;

  return (
    <Card className={cn(editing && 'border-primary')}>
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          Default Price
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
              <FormField
                control={form.control}
                name="defaultPrice"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Price per player ($)</FormLabel>
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
                    {boundsHint && (
                      <p className="text-xs text-muted-foreground">{boundsHint}</p>
                    )}
                    <FormMessage />
                  </FormItem>
                )}
              />
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
          <p className="text-2xl font-semibold text-ink">
            ${defaultPrice?.toFixed(2)}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
