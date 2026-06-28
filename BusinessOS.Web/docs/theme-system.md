# Theme Management System

BusinessOS uses a design-token-based theme system that applies globally via CSS custom properties and Angular signals.

## Architecture

```
src/app/core/theme/
├── theme.model.ts       # Types and default preferences
├── theme.constants.ts   # Built-in themes, fonts, scales
├── theme.tokens.ts      # CSS variable name registry
├── theme.storage.ts     # localStorage persistence
├── theme.service.ts     # Signal-based theme engine
├── theme.guard.ts       # Route guard for initialization
└── index.ts             # Public API

src/styles/
├── _variables.scss      # Base tokens + layout preference hooks
├── _themes.scss         # 8 built-in theme palettes
└── _layout-preferences.scss
```

## Built-in Themes

| ID | Name | Category |
|----|------|----------|
| `light` | Light | Light |
| `dark` | Dark | Dark |
| `blue-professional` | Blue Professional | Light |
| `green-business` | Green Business | Light |
| `purple-executive` | Purple Executive | Light |
| `orange-startup` | Orange Startup | Light |
| `corporate-gray` | Corporate Gray | Light |
| `high-contrast` | High Contrast | Accessibility |

## Usage

### Inject ThemeService

```typescript
import { ThemeService } from '@app/core/theme';

private readonly theme = inject(ThemeService);

// Read active theme
this.theme.themeId();
this.theme.resolvedAppearance(); // 'light' | 'dark'

// Change theme instantly
this.theme.setThemeId('blue-professional');
this.theme.updatePreferences({ fontFamily: 'poppins', compactMode: true });
```

### CSS Variables

Components must use tokens — never hardcode colors:

```scss
.my-component {
  background: var(--surface-color);
  color: var(--text-color);
  border: 1px solid var(--border-color);
}
```

Legacy aliases (`--color-primary`, etc.) remain supported.

## Settings UI

- **Settings → Appearance** (`/settings/appearance`)
- **Theme Selector** component (`app-theme-selector`)

Features: preview cards, live preview, search, favorites, recent themes, import/export, custom builder.

## Persistence

1. **localStorage** — `businessos.ui.themePreferences` (primary, survives logout)
2. **Backend** — serialized JSON in tenant `settings.theme` via Save & Sync

On login, `AuthService` calls `ThemeService.syncFromBackend()` to merge server preferences.

## Accessibility

- High Contrast theme (WCAG-friendly)
- Large Font Mode (`data-large-font`)
- Reduced Motion (`data-reduced-motion`, respects `prefers-reduced-motion`)
- Keyboard navigable theme cards with ARIA roles

## Testing

```bash
npm test -- --include='**/theme/**/*.spec.ts' --include='**/theme-selector/**/*.spec.ts' --include='**/appearance/**/*.spec.ts'
```

Coverage targets: ThemeService, ThemeStorage, ThemeSelectorComponent, AppearanceSettingsComponent.
