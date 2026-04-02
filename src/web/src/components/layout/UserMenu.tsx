import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/features/auth';

function getInitials(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  const first = parts[0] ?? '';
  if (parts.length === 1) return first.slice(0, 2).toUpperCase();
  const last = parts[parts.length - 1] ?? '';
  return ((first[0] ?? '') + (last[0] ?? '')).toUpperCase();
}

interface UserMenuProps {
  onSwitchCourse?: () => void;
}

export default function UserMenu({ onSwitchCourse }: UserMenuProps = {}) {
  const { user, logout } = useAuth();

  if (!user) return null;

  const initials = getInitials(user.displayName || user.email);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          aria-label="User menu"
        >
          <Avatar size="sm">
            <AvatarFallback>{initials}</AvatarFallback>
          </Avatar>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <div className="flex flex-col gap-0.5">
            <span className="text-sm font-medium leading-none">{user.displayName}</span>
            <span className="text-xs text-muted-foreground leading-none mt-1">{user.email}</span>
            <span className="text-xs text-muted-foreground leading-none mt-0.5 capitalize">{user.role}</span>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        {onSwitchCourse && (
          <DropdownMenuItem onSelect={onSwitchCourse}>
            Switch course
          </DropdownMenuItem>
        )}
        <DropdownMenuItem
          variant="destructive"
          onSelect={logout}
        >
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
