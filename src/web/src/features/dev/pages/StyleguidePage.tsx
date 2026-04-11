import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import {
  AlertCircle,
  Bell,
  Check,
  ChevronDown,
  Copy,
  Edit,
  Info,
  Loader2,
  Mail,
  MoreHorizontal,
  Plus,
  Trash2,
  User,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Avatar, AvatarBadge, AvatarFallback, AvatarGroup, AvatarGroupCount, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardAction, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import {
  Drawer,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from '@/components/ui/drawer';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusBadge, type StatusBadgeStatus } from '@/components/ui/status-badge';
import { StatusChip } from '@/components/ui/status-chip';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { WarningAlert } from '@/components/ui/warning-alert';

const SECTIONS = [
  { id: 'palette', label: 'Palette' },
  { id: 'typography', label: 'Typography' },
  { id: 'buttons', label: 'Buttons' },
  { id: 'badges', label: 'Badges' },
  { id: 'status', label: 'Status' },
  { id: 'alerts', label: 'Alerts' },
  { id: 'cards', label: 'Cards' },
  { id: 'forms', label: 'Forms' },
  { id: 'tabs', label: 'Tabs' },
  { id: 'table', label: 'Table' },
  { id: 'avatar', label: 'Avatar' },
  { id: 'feedback', label: 'Feedback' },
  { id: 'overlays', label: 'Overlays' },
] as const;

function SectionHeading({ id, title, children }: { id: string; title: string; children?: React.ReactNode }) {
  return (
    <div className="mb-6">
      <h2 id={id} className="scroll-mt-24 text-2xl font-semibold tracking-tight text-foreground">
        {title}
      </h2>
      {children ? <p className="mt-1 text-sm text-muted-foreground">{children}</p> : null}
    </div>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="grid grid-cols-[8rem_1fr] items-center gap-4 border-b border-border/60 py-4 last:border-b-0">
      <div className="text-xs font-medium uppercase tracking-wider text-muted-foreground">{label}</div>
      <div className="flex flex-wrap items-center gap-3">{children}</div>
    </div>
  );
}

function Swatch({ name, hex, tw, textOn = 'light' }: { name: string; hex: string; tw?: string; textOn?: 'light' | 'dark' }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div
        className="h-16 w-full rounded-md border border-border shadow-sm"
        style={{ backgroundColor: hex }}
        aria-label={name}
      />
      <div className="flex flex-col gap-0.5">
        <span className="text-xs font-medium text-foreground">{name}</span>
        <span className="font-mono text-[10px] uppercase text-muted-foreground">{hex}</span>
        {tw ? <span className="font-mono text-[10px] text-muted-foreground">{tw}</span> : null}
      </div>
      <span className="sr-only">{textOn}</span>
    </div>
  );
}

function TokenSwatch({ name, token }: { name: string; token: string }) {
  return (
    <div className="flex flex-col gap-1.5">
      <div className="h-14 w-full rounded-md border border-border shadow-sm" style={{ backgroundColor: `var(${token})` }} />
      <div className="flex flex-col gap-0.5">
        <span className="text-xs font-medium text-foreground">{name}</span>
        <span className="font-mono text-[10px] text-muted-foreground">{token}</span>
      </div>
    </div>
  );
}

// ─── Palette ───
function PaletteSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="palette" title="Palette">
        Teeforce brand colors + semantic tokens.
      </SectionHeading>

      <h3 className="mb-3 text-sm font-semibold text-foreground">Brand</h3>
      <div className="mb-8 grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-5">
        <Swatch name="Deep Forest" hex="#102B1A" tw="bg-green-darkest" textOn="dark" />
        <Swatch name="Forest Mid" hex="#1A4D35" tw="bg-green-dark" textOn="dark" />
        <Swatch name="Evergreen" hex="#276749" tw="bg-green" textOn="dark" />
        <Swatch name="Fern" hex="#52B788" tw="bg-green-mid" />
        <Swatch name="Forest Pale" hex="#E2F0E5" tw="bg-green-light" />
        <Swatch name="Tangerine" hex="#F28C28" tw="bg-orange" />
        <Swatch name="Off White" hex="#F5FAF6" tw="bg-canvas" />
        <Swatch name="Text" hex="#101F14" tw="text-ink" textOn="dark" />
        <Swatch name="Text Light" hex="#6A856C" tw="text-ink-muted" />
      </div>

      <h3 className="mb-3 text-sm font-semibold text-foreground">Semantic tokens</h3>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-6">
        <TokenSwatch name="background" token="--background" />
        <TokenSwatch name="foreground" token="--foreground" />
        <TokenSwatch name="card" token="--card" />
        <TokenSwatch name="primary" token="--primary" />
        <TokenSwatch name="secondary" token="--secondary" />
        <TokenSwatch name="muted" token="--muted" />
        <TokenSwatch name="accent" token="--accent" />
        <TokenSwatch name="success" token="--success" />
        <TokenSwatch name="destructive" token="--destructive" />
        <TokenSwatch name="border" token="--border" />
        <TokenSwatch name="ring" token="--ring" />
        <TokenSwatch name="sidebar" token="--sidebar" />
      </div>
    </section>
  );
}

// ─── Typography ───
function TypographySection() {
  return (
    <section className="mb-16">
      <SectionHeading id="typography" title="Typography">
        Libre Baskerville for headings, IBM Plex Sans for body, IBM Plex Mono for code.
      </SectionHeading>
      <div className="space-y-4 rounded-lg border border-border bg-card p-6">
        <div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">H1 · text-4xl font-serif</div>
          <h1 className="font-[var(--font-heading)] text-4xl font-semibold tracking-tight text-foreground">
            The quick brown fox jumps
          </h1>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">H2 · text-2xl</div>
          <h2 className="text-2xl font-semibold tracking-tight text-foreground">Section heading</h2>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">H3 · text-lg</div>
          <h3 className="text-lg font-semibold text-foreground">Subsection heading</h3>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">Body · text-sm</div>
          <p className="text-sm text-foreground">
            Every course shares infrastructure but gets its own isolated world. Operators know their course best — ship with sensible
            defaults, but every operational parameter is configurable.
          </p>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">Muted · text-sm text-muted-foreground</div>
          <p className="text-sm text-muted-foreground">Secondary copy for helper text, descriptions, and captions.</p>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wider text-muted-foreground">Mono</div>
          <code className="font-mono text-sm text-foreground">const teeTime = getNext(course, now);</code>
        </div>
      </div>
    </section>
  );
}

// ─── Buttons ───
const BUTTON_VARIANTS = ['default', 'secondary', 'outline', 'ghost', 'link', 'destructive'] as const;
const BUTTON_SIZES = ['xs', 'sm', 'default', 'lg'] as const;

function ButtonsSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="buttons" title="Buttons">
        6 variants × 4 text sizes + icon sizes, disabled, loading.
      </SectionHeading>
      <div className="rounded-lg border border-border bg-card p-6">
        {BUTTON_VARIANTS.map((variant) => (
          <Row key={variant} label={variant}>
            {BUTTON_SIZES.map((size) => (
              <Button key={size} variant={variant} size={size}>
                {size === 'xs' ? 'xs' : size === 'sm' ? 'Small' : size === 'lg' ? 'Large' : 'Button'}
              </Button>
            ))}
            <Button variant={variant} disabled>
              Disabled
            </Button>
            <Button variant={variant}>
              <Plus />
              With icon
            </Button>
          </Row>
        ))}
        <Row label="icon">
          <Button size="icon-xs" variant="outline" aria-label="more">
            <MoreHorizontal />
          </Button>
          <Button size="icon-sm" variant="outline" aria-label="more">
            <MoreHorizontal />
          </Button>
          <Button size="icon" variant="outline" aria-label="more">
            <MoreHorizontal />
          </Button>
          <Button size="icon-lg" variant="outline" aria-label="more">
            <MoreHorizontal />
          </Button>
          <Button size="icon" aria-label="add">
            <Plus />
          </Button>
          <Button size="icon" variant="destructive" aria-label="delete">
            <Trash2 />
          </Button>
        </Row>
        <Row label="loading">
          <Button disabled>
            <Loader2 className="animate-spin" />
            Saving…
          </Button>
          <Button variant="secondary" disabled>
            <Loader2 className="animate-spin" />
            Loading
          </Button>
          <Button variant="outline" disabled>
            <Loader2 className="animate-spin" />
            Working
          </Button>
        </Row>
      </div>
    </section>
  );
}

// ─── Badges ───
function BadgesSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="badges" title="Badges" />
      <div className="rounded-lg border border-border bg-card p-6">
        <Row label="default">
          <Badge>Default</Badge>
          <Badge>
            <Check className="mr-1 size-3" /> With icon
          </Badge>
        </Row>
        <Row label="secondary">
          <Badge variant="secondary">Secondary</Badge>
        </Row>
        <Row label="outline">
          <Badge variant="outline">Outline</Badge>
        </Row>
        <Row label="success">
          <Badge variant="success">Success</Badge>
        </Row>
        <Row label="muted">
          <Badge variant="muted">Muted</Badge>
        </Row>
        <Row label="destructive">
          <Badge variant="destructive">Destructive</Badge>
        </Row>
      </div>
    </section>
  );
}

// ─── Status badges / chips ───
const STATUSES: StatusBadgeStatus[] = [
  'booked',
  'open',
  'waitlist',
  'checkedin',
  'noshowed',
  'filled',
  'expired',
  'cancelled',
];

function StatusSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="status" title="Status Badges & Chips">
        Domain-specific status indicators — not shadcn primitives.
      </SectionHeading>
      <div className="rounded-lg border border-border bg-card p-6">
        <Row label="StatusBadge">
          {STATUSES.map((status) => (
            <StatusBadge key={status} status={status} />
          ))}
        </Row>
        <Row label="StatusChip">
          <StatusChip tone="green">3 confirmed</StatusChip>
          <StatusChip tone="orange">2 waiting</StatusChip>
          <StatusChip tone="gray">No activity</StatusChip>
        </Row>
      </div>
    </section>
  );
}

// ─── Alerts ───
function AlertsSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="alerts" title="Alerts" />
      <div className="space-y-4">
        <Alert>
          <Info />
          <AlertTitle>Heads up</AlertTitle>
          <AlertDescription>
            Tee sheet changes take effect immediately. Existing bookings are not affected.
          </AlertDescription>
        </Alert>
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>Unable to save</AlertTitle>
          <AlertDescription>
            The course has bookings that overlap the new interval. Resolve them before changing the schedule.
          </AlertDescription>
        </Alert>
        <WarningAlert>
          This action will cancel 4 existing bookings. Affected golfers will receive an SMS notification.
        </WarningAlert>
      </div>
    </section>
  );
}

// ─── Cards ───
function CardsSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="cards" title="Cards" />
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Tee sheet</CardTitle>
            <CardDescription>Saturday, April 12</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">42 of 60 slots booked. 3 walk-ups waiting.</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Weather</CardTitle>
            <CardDescription>Cloudy, 58°F</CardDescription>
            <CardAction>
              <Button size="sm" variant="ghost">
                <Edit />
              </Button>
            </CardAction>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">Wind 8 mph · 20% chance of rain by 3pm.</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Confirm booking</CardTitle>
            <CardDescription>Thursday at 8:20 AM</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">Twosome with cart. $84 total.</p>
          </CardContent>
          <CardFooter className="flex justify-end gap-2 border-t pt-4">
            <Button variant="ghost" size="sm">
              Cancel
            </Button>
            <Button size="sm">Confirm</Button>
          </CardFooter>
        </Card>
      </div>
    </section>
  );
}

// ─── Forms ───
const formSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  email: z.string().email('Enter a valid email'),
  course: z.string().min(1, 'Pick a course'),
  notifications: z.boolean(),
  terms: z.boolean().refine((v) => v, 'You must accept the terms'),
});

function FormsSection() {
  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: { name: '', email: '', course: '', notifications: true, terms: false },
    mode: 'onTouched',
  });

  return (
    <section className="mb-16">
      <SectionHeading id="forms" title="Forms">
        React Hook Form + Zod. Submit with empty fields to see error states.
      </SectionHeading>
      <div className="grid gap-6 md:grid-cols-2">
        <div className="rounded-lg border border-border bg-card p-6">
          <h3 className="mb-4 text-sm font-semibold">Controls</h3>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="preview-input">Text input</Label>
              <Input id="preview-input" placeholder="example@course.com" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="preview-disabled">Disabled</Label>
              <Input id="preview-disabled" disabled value="Read-only" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="preview-invalid">Invalid</Label>
              <Input id="preview-invalid" aria-invalid defaultValue="not-an-email" />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox id="preview-cb" defaultChecked />
              <Label htmlFor="preview-cb">Checkbox</Label>
            </div>
            <div className="flex items-center gap-2">
              <Switch id="preview-switch" defaultChecked />
              <Label htmlFor="preview-switch">Switch</Label>
            </div>
            <div className="space-y-2">
              <Label>Select</Label>
              <Select>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Pick a course" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="pebble">Pebble Beach</SelectItem>
                  <SelectItem value="augusta">Augusta National</SelectItem>
                  <SelectItem value="sawgrass">TPC Sawgrass</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </div>

        <div className="rounded-lg border border-border bg-card p-6">
          <h3 className="mb-4 text-sm font-semibold">Validated form</h3>
          <Form {...form}>
            <form
              onSubmit={form.handleSubmit(() => undefined)}
              className="space-y-4"
              noValidate
            >
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Full name</FormLabel>
                    <FormControl>
                      <Input placeholder="Jane Golfer" {...field} />
                    </FormControl>
                    <FormDescription>Shown on the scorecard.</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email</FormLabel>
                    <FormControl>
                      <Input type="email" placeholder="jane@example.com" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="course"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Home course</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Pick a course" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="pebble">Pebble Beach</SelectItem>
                        <SelectItem value="augusta">Augusta National</SelectItem>
                        <SelectItem value="sawgrass">TPC Sawgrass</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="notifications"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-center gap-3">
                    <FormControl>
                      <Switch checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                    <FormLabel className="!mt-0">SMS notifications</FormLabel>
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="terms"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-start gap-2">
                    <FormControl>
                      <Checkbox checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                    <div className="grid gap-1 leading-none">
                      <FormLabel className="!mt-0">Accept terms</FormLabel>
                      <FormMessage />
                    </div>
                  </FormItem>
                )}
              />
              <Button type="submit">Submit</Button>
            </form>
          </Form>
        </div>
      </div>
    </section>
  );
}

// ─── Tabs ───
function TabsSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="tabs" title="Tabs" />
      <div className="space-y-6 rounded-lg border border-border bg-card p-6">
        <div>
          <div className="mb-2 text-xs uppercase tracking-wider text-muted-foreground">default</div>
          <Tabs defaultValue="tee-sheet">
            <TabsList>
              <TabsTrigger value="tee-sheet">Tee sheet</TabsTrigger>
              <TabsTrigger value="waitlist">Waitlist</TabsTrigger>
              <TabsTrigger value="openings">Openings</TabsTrigger>
            </TabsList>
            <TabsContent value="tee-sheet" className="p-4 text-sm text-muted-foreground">
              Tee sheet grid goes here.
            </TabsContent>
            <TabsContent value="waitlist" className="p-4 text-sm text-muted-foreground">
              Walk-up waitlist goes here.
            </TabsContent>
            <TabsContent value="openings" className="p-4 text-sm text-muted-foreground">
              Available slots go here.
            </TabsContent>
          </Tabs>
        </div>
        <div>
          <div className="mb-2 text-xs uppercase tracking-wider text-muted-foreground">line variant</div>
          <Tabs defaultValue="day">
            <TabsList variant="line">
              <TabsTrigger value="day">Day</TabsTrigger>
              <TabsTrigger value="week">Week</TabsTrigger>
              <TabsTrigger value="month">Month</TabsTrigger>
            </TabsList>
            <TabsContent value="day" className="p-4 text-sm text-muted-foreground">
              Day view
            </TabsContent>
            <TabsContent value="week" className="p-4 text-sm text-muted-foreground">
              Week view
            </TabsContent>
            <TabsContent value="month" className="p-4 text-sm text-muted-foreground">
              Month view
            </TabsContent>
          </Tabs>
        </div>
      </div>
    </section>
  );
}

// ─── Table ───
function TableSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="table" title="Table" />
      <div className="rounded-lg border border-border bg-card p-6">
        <Table>
          <TableCaption>Bookings for Saturday, April 12</TableCaption>
          <TableHeader>
            <TableRow>
              <TableHead>Time</TableHead>
              <TableHead>Golfer</TableHead>
              <TableHead>Players</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Total</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell className="font-mono">7:40 AM</TableCell>
              <TableCell>Jane Golfer</TableCell>
              <TableCell>4</TableCell>
              <TableCell>
                <StatusBadge status="booked" />
              </TableCell>
              <TableCell className="text-right">$168</TableCell>
            </TableRow>
            <TableRow>
              <TableCell className="font-mono">7:50 AM</TableCell>
              <TableCell>Tom Swinger</TableCell>
              <TableCell>2</TableCell>
              <TableCell>
                <StatusBadge status="checkedin" />
              </TableCell>
              <TableCell className="text-right">$84</TableCell>
            </TableRow>
            <TableRow>
              <TableCell className="font-mono">8:00 AM</TableCell>
              <TableCell className="text-muted-foreground">—</TableCell>
              <TableCell className="text-muted-foreground">—</TableCell>
              <TableCell>
                <StatusBadge status="open" />
              </TableCell>
              <TableCell className="text-right text-muted-foreground">—</TableCell>
            </TableRow>
            <TableRow>
              <TableCell className="font-mono">8:10 AM</TableCell>
              <TableCell>Ben Putter</TableCell>
              <TableCell>3</TableCell>
              <TableCell>
                <StatusBadge status="cancelled" />
              </TableCell>
              <TableCell className="text-right">$126</TableCell>
            </TableRow>
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell colSpan={4}>Total</TableCell>
              <TableCell className="text-right">$378</TableCell>
            </TableRow>
          </TableFooter>
        </Table>
      </div>
    </section>
  );
}

// ─── Avatar ───
function AvatarSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="avatar" title="Avatar" />
      <div className="rounded-lg border border-border bg-card p-6">
        <Row label="sizes">
          <Avatar size="sm">
            <AvatarFallback>JG</AvatarFallback>
          </Avatar>
          <Avatar>
            <AvatarFallback>JG</AvatarFallback>
          </Avatar>
          <Avatar size="lg">
            <AvatarFallback>JG</AvatarFallback>
          </Avatar>
        </Row>
        <Row label="image">
          <Avatar>
            <AvatarImage src="https://i.pravatar.cc/80?img=12" alt="" />
            <AvatarFallback>AB</AvatarFallback>
          </Avatar>
          <Avatar size="lg">
            <AvatarImage src="https://i.pravatar.cc/80?img=32" alt="" />
            <AvatarFallback>CD</AvatarFallback>
          </Avatar>
        </Row>
        <Row label="badge">
          <Avatar>
            <AvatarFallback>JG</AvatarFallback>
            <AvatarBadge className="bg-success">
              <Check />
            </AvatarBadge>
          </Avatar>
          <Avatar size="lg">
            <AvatarFallback>JG</AvatarFallback>
            <AvatarBadge>
              <Bell />
            </AvatarBadge>
          </Avatar>
        </Row>
        <Row label="group">
          <AvatarGroup>
            <Avatar>
              <AvatarFallback>A</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback>B</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback>C</AvatarFallback>
            </Avatar>
            <AvatarGroupCount>+3</AvatarGroupCount>
          </AvatarGroup>
        </Row>
      </div>
    </section>
  );
}

// ─── Feedback (separator, skeleton) ───
function FeedbackSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="feedback" title="Feedback" />
      <div className="space-y-6 rounded-lg border border-border bg-card p-6">
        <div>
          <div className="mb-2 text-xs uppercase tracking-wider text-muted-foreground">Separator</div>
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            <span>Left</span>
            <Separator orientation="vertical" className="h-4" />
            <span>Middle</span>
            <Separator orientation="vertical" className="h-4" />
            <span>Right</span>
          </div>
          <Separator className="my-4" />
          <p className="text-sm text-muted-foreground">Horizontal separator above.</p>
        </div>
        <div>
          <div className="mb-2 text-xs uppercase tracking-wider text-muted-foreground">Skeleton</div>
          <div className="flex items-center gap-3">
            <Skeleton className="size-10 rounded-full" />
            <div className="space-y-2">
              <Skeleton className="h-3 w-48" />
              <Skeleton className="h-3 w-32" />
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

// ─── Overlays ───
function OverlaysSection() {
  return (
    <section className="mb-16">
      <SectionHeading id="overlays" title="Overlays">
        Dialog, alert-dialog, sheet, drawer, dropdown, tooltip, select.
      </SectionHeading>
      <div className="rounded-lg border border-border bg-card p-6">
        <Row label="Dialog">
          <Dialog>
            <DialogTrigger asChild>
              <Button variant="outline">Open dialog</Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Edit profile</DialogTitle>
                <DialogDescription>Changes save on submit.</DialogDescription>
              </DialogHeader>
              <div className="grid gap-3 py-2">
                <Label htmlFor="dlg-name">Name</Label>
                <Input id="dlg-name" defaultValue="Jane Golfer" />
              </div>
              <DialogFooter>
                <Button variant="ghost">Cancel</Button>
                <Button>Save</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </Row>
        <Row label="AlertDialog">
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="destructive">Delete</Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete this booking?</AlertDialogTitle>
                <AlertDialogDescription>
                  This cannot be undone. The golfer will be notified via SMS.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction>Delete</AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </Row>
        <Row label="Sheet">
          <Sheet>
            <SheetTrigger asChild>
              <Button variant="outline">Open sheet</Button>
            </SheetTrigger>
            <SheetContent>
              <SheetHeader>
                <SheetTitle>Course settings</SheetTitle>
                <SheetDescription>Adjust defaults for this course.</SheetDescription>
              </SheetHeader>
              <div className="p-4 text-sm text-muted-foreground">Sheet body content.</div>
            </SheetContent>
          </Sheet>
        </Row>
        <Row label="Drawer">
          <Drawer>
            <DrawerTrigger asChild>
              <Button variant="outline">Open drawer</Button>
            </DrawerTrigger>
            <DrawerContent>
              <DrawerHeader>
                <DrawerTitle>Filters</DrawerTitle>
                <DrawerDescription>Narrow the tee sheet.</DrawerDescription>
              </DrawerHeader>
              <div className="p-4 text-sm text-muted-foreground">Drawer body content.</div>
              <DrawerFooter>
                <Button>Apply</Button>
              </DrawerFooter>
            </DrawerContent>
          </Drawer>
        </Row>
        <Row label="DropdownMenu">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline">
                Actions
                <ChevronDown />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="w-56">
              <DropdownMenuLabel>Booking</DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem>
                <User />
                View golfer
              </DropdownMenuItem>
              <DropdownMenuItem>
                <Mail />
                Resend confirmation
              </DropdownMenuItem>
              <DropdownMenuItem>
                <Copy />
                Copy link
                <DropdownMenuShortcut>⌘C</DropdownMenuShortcut>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuCheckboxItem checked>Email on check-in</DropdownMenuCheckboxItem>
              <DropdownMenuSeparator />
              <DropdownMenuRadioGroup value="sms">
                <DropdownMenuLabel>Channel</DropdownMenuLabel>
                <DropdownMenuRadioItem value="sms">SMS</DropdownMenuRadioItem>
                <DropdownMenuRadioItem value="email">Email</DropdownMenuRadioItem>
              </DropdownMenuRadioGroup>
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive">
                <Trash2 />
                Cancel booking
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </Row>
        <Row label="Tooltip">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button variant="outline" size="icon" aria-label="info">
                  <Info />
                </Button>
              </TooltipTrigger>
              <TooltipContent>This appears on hover.</TooltipContent>
            </Tooltip>
          </TooltipProvider>
        </Row>
        <Row label="Select">
          <Select defaultValue="pebble">
            <SelectTrigger className="w-64">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="pebble">Pebble Beach</SelectItem>
              <SelectItem value="augusta">Augusta National</SelectItem>
              <SelectItem value="sawgrass">TPC Sawgrass</SelectItem>
            </SelectContent>
          </Select>
        </Row>
      </div>
    </section>
  );
}

// ─── Page ───
export default function StyleguidePage() {
  return (
    <div className="min-h-screen bg-background">
      <header className="sticky top-0 z-20 border-b border-border bg-background/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div>
            <h1 className="text-xl font-semibold tracking-tight text-foreground">Teeforce Styleguide</h1>
            <p className="text-xs text-muted-foreground">Every component, every variant.</p>
          </div>
          <nav className="hidden flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground md:flex">
            {SECTIONS.map((s) => (
              <a key={s.id} href={`#${s.id}`} className="hover:text-foreground">
                {s.label}
              </a>
            ))}
          </nav>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-6 py-10">
        <PaletteSection />
        <TypographySection />
        <ButtonsSection />
        <BadgesSection />
        <StatusSection />
        <AlertsSection />
        <CardsSection />
        <FormsSection />
        <TabsSection />
        <TableSection />
        <AvatarSection />
        <FeedbackSection />
        <OverlaysSection />
      </main>
    </div>
  );
}
