import { Monitor, Moon, Sun } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useColorMode, type ColorMode } from '@/lib/color-mode';

/**
 * Radio items for the three color modes. Designed to be embedded inside
 * an already-open <DropdownMenu> (e.g. UserMenu's Theme submenu, or the
 * standalone <ThemeToggle> below).
 */
export function ThemeMenuItems() {
  const { mode, setMode } = useColorMode();
  return (
    <DropdownMenuRadioGroup value={mode} onValueChange={(value) => setMode(value as ColorMode)}>
      <DropdownMenuRadioItem value="light">
        <Sun />
        Light
      </DropdownMenuRadioItem>
      <DropdownMenuRadioItem value="dark">
        <Moon />
        Dark
      </DropdownMenuRadioItem>
      <DropdownMenuRadioItem value="system">
        <Monitor />
        System
      </DropdownMenuRadioItem>
    </DropdownMenuRadioGroup>
  );
}

/**
 * Standalone color-mode dropdown with an icon button trigger.
 * Use in places without a user menu (styleguide preview, public pages, etc).
 */
export function ThemeToggle() {
  const { resolved } = useColorMode();
  const Icon = resolved === 'dark' ? Moon : Sun;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="icon" aria-label="Toggle theme">
          <Icon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-40">
        <ThemeMenuItems />
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
