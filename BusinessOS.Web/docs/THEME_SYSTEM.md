# BusinessOS Theme Management System

Application-wide appearance personalization for the BusinessOS Angular frontend (`BusinessOS.Web`).

## Architecture

```
core/theme/
├── theme.service.ts      # Signal-based theme engine
├── theme.model.ts        # Types & ThemePreferences
├── theme.constants.ts    # Built-in palettes, fonts, scales
├── theme.tokens.ts       # CSS variable names
├── theme.storage.ts      # Local persistence
├── theme.guard.ts        # Route guard (ensure init)
└── index.ts              # Public exports
```

### Runtime DOM contract

`ThemeService` applies preferences by setting attributes on `document.documentElement`:

| Attribute | Purpose |
|-----------|---------|
| `data-theme-id` | Active palette (`light`, `dark`, `blue-professional`, …) |
| `data-theme` | Resolved appearance (`light` / `dark`) |
| `data-bs-theme` | Bootstrap 5 dark mode sync |
| `data-sidebar-style` | `expanded` \| `collapsed` \| `mini` \| `floating` |
| `data-navbar-style` | `fixed` \| `static` \| `compact` |
| `data-card-style` | `flat` \| `elevated` \| `glassmorphism` \| `modern` |
| `data-table-density` | `comfortable` \| `compact` \| `spacious` |
| `data-footer-visibility` | `visible` \| `hidden` |
| `data-compact` | Compact layout mode |
| `data-animations` | Enable transitions |
| `data-large-font` | Accessibility large text |
| `data-reduced-motion` | WCAG reduced motion |

Custom user themes inject a `<style id="bos-custom-theme">` block with token overrides.

## SCSS token layers

1. **`styles/_variables.scss`** — Base design tokens + legacy aliases (`--color-*`)
2. **`styles/_themes.scss`** — 8 built-in palette overrides via `[data-theme-id]`
3. **`styles/_layout-preferences.scss`** — Layout density, sidebar width, animations
4. **Component SCSS** — Must use `var(--primary-color)` or legacy `var(--color-primary)`

## Built-in themes

| ID | Name |
|----|------|
| `light` | Light |
| `dark` | Dark |
| `blue-professional` | Blue Professional |
| `green-business` | Green Business |
| `purple-executive` | Purple Executive |
| `orange-startup` | Orange Startup |
| `corporate-gray` | Corporate Gray |
| `high-contrast` | High Contrast (WCAG) |

## UI entry points

| Location | Component |
|----------|-----------|
| Settings → Appearance tab | `ThemeSelectorComponent` + link to full page |
| `/settings/appearance` | `AppearanceSettingsComponent` |
| Navbar | Dark mode quick toggle |
| Profile menu | Appearance link |

## Persistence

1. **Local storage** — Key: `businessos.ui.themePreferences` (JSON `ThemePreferences`)
2. **Backend** — Tenant `settings.theme` stores serialized JSON via `ThemeService.syncToBackend()`

On app boot and login, `syncFromBackend()` merges tenant preferences. Local changes apply instantly without route reload.

## Usage in components

```typescript
import { ThemeService } from '@app/core/theme';

private readonly themeService = inject(ThemeService);

// Read reactive state
activeTheme = this.themeService.activeTheme;
appearance = this.themeService.resolvedAppearance;

// Apply changes
this.themeService.setThemeId('green-business');
this.themeService.updatePreferences({ fontFamily: 'poppins' }, { syncBackend: true });
```

Charts should read CSS variables:

```typescript
getComputedStyle(document.documentElement).getPropertyValue('--chart-primary-color').trim();
```

## Import / export

- **Export**: Settings → Appearance → Export Theme (JSON file)
- **Import**: Upload JSON matching `ThemeExportPayload`
- **Reset**: Restores `DEFAULT_THEME_PREFERENCES`

## Custom theme builder

Users define primary, background, surface, and text colors. The service generates a `custom-{timestamp}` theme with dynamic CSS variables.

## Testing

Run theme unit tests:

```bash
cd BusinessOS.Web
npm test -- --include='**/theme/**/*.spec.ts' --include='**/theme-selector/**/*.spec.ts' --include='**/appearance/**/*.spec.ts'
```

## Accessibility

- **High Contrast** theme with enhanced borders and contrast ratios
- **Large Font Mode** via `data-large-font`
- **Reduced Motion** via `data-reduced-motion` (respects OS preference)
- Theme selector supports keyboard focus preview and ARIA roles
