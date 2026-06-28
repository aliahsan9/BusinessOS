# BusinessOS Responsive Design Guide

> Layout, breakpoint, and component behavior rules for Angular 19 + Bootstrap 5.

---

## 1. Design Philosophy

BusinessOS users operate on:

- **Desktop** — back office, reports, bulk data entry
- **Tablet** — shop floor, inventory counts
- **Mobile** — quick sales lookup, customer phone numbers, dashboard glance

**Priority:** Mobile-readable, desktop-efficient. Never hide critical actions on mobile — relocate, don't remove.

**Reference aesthetics:** Clean SaaS (Shopify admin, Zoho Books) — white surfaces, subtle borders, primary accent, generous whitespace.

---

## 2. Breakpoints (Bootstrap 5)

| Name | Min width | Typical device | Layout |
|------|-----------|----------------|--------|
| `xs` | 0 | Phone portrait | Single column, drawer nav |
| `sm` | 576px | Phone landscape | Single column |
| `md` | 768px | Tablet portrait | 2-column forms possible |
| `lg` | 992px | Tablet landscape / small laptop | Sidebar visible |
| `xl` | 1200px | Desktop | Full dashboard grid |
| `xxl` | 1400px | Large desktop | Max-width container optional |

**SCSS variables** (extend `src/styles/_variables.scss`):

```scss
$breakpoint-sm: 576px;
$breakpoint-md: 768px;
$breakpoint-lg: 992px;
$breakpoint-xl: 1200px;
$breakpoint-xxl: 1400px;

$content-max-width: 1440px;
$sidebar-width: 260px;
$sidebar-collapsed-width: 72px;
$navbar-height: 56px;
```

---

## 3. App Shell Responsive Behavior

### Desktop (≥992px)

```
┌──────────┬────────────────────────────────────┐
│          │ Navbar                             │
│ Sidebar  ├────────────────────────────────────┤
│ 260px    │ Breadcrumb                         │
│          ├────────────────────────────────────┤
│          │ Page content                       │
│          │                                    │
└──────────┴────────────────────────────────────┘
```

- Sidebar fixed left, scrollable nav items
- Collapse toggle → icon-only 72px mode
- Content area: `margin-left: sidebar-width`

### Tablet (768px–991px)

- Sidebar off-canvas (hidden by default)
- Hamburger opens overlay drawer
- Tables → horizontal scroll wrapper

### Mobile (<768px)

```
┌────────────────────────────────────┐
│ ☰  BusinessOS          👤         │
├────────────────────────────────────┤
│ Page title            [+ Action]   │
├────────────────────────────────────┤
│ Content (single column)            │
│                                    │
└────────────────────────────────────┘
```

- Full-width content
- Primary action: floating action button (FAB) for "New order" on orders module
- Bottom padding for safe area (iOS)

---

## 4. Grid Patterns

### Dashboard stat cards

| Breakpoint | Columns |
|------------|---------|
| xs | 1 (stacked) |
| sm | 2 |
| lg | 4 |

```html
<div class="row g-3">
  <div class="col-12 col-sm-6 col-xl-3">...</div>
</div>
```

### List + detail (master-detail)

| Breakpoint | Pattern |
|------------|---------|
| ≥992px | Optional split pane (future) |
| <992px | Separate routes (`/customers` → `/customers/:id`) |

### Form layouts

| Breakpoint | Pattern |
|------------|---------|
| ≥768px | Two-column: labels left or 6+6 fields |
| <768px | Single column, full-width inputs |

---

## 5. Component Responsive Rules

### Data tables

| Breakpoint | Behavior |
|------------|----------|
| ≥992px | Full table all columns |
| 768–991px | Hide low-priority columns (`d-none d-lg-table-cell`) |
| <768px | **Card list mode** — each row becomes a card |

**Mobile card row pattern:**
```
┌─────────────────────────────┐
│ Product Name        [Active]│
│ SKU: ABC123    Stock: 42    │
│ $29.99          [Edit] [···] │
└─────────────────────────────┘
```

### Charts

| Breakpoint | Height | Notes |
|------------|--------|-------|
| Mobile | 220px | Simplified legend below |
| Desktop | 320px | Legend right |

Use `maintainAspectRatio: false` in Chart.js config.

### Modals

| Breakpoint | Style |
|------------|-------|
| ≥576px | Centered modal, max-width 500px (forms) / 800px (order builder) |
| <576px | Full-screen modal (`modal-fullscreen-sm-down`) |

### Pagination

| Breakpoint | Show |
|------------|------|
| Mobile | Prev / Page X of Y / Next |
| Desktop | Full page numbers |

### Breadcrumb

| Breakpoint | Show |
|------------|------|
| Mobile | Last segment only + back chevron |
| Desktop | Full trail |

---

## 6. Typography Scale

| Element | Mobile | Desktop |
|---------|--------|---------|
| Page title (h1) | 1.5rem | 1.75rem |
| Section title (h2) | 1.25rem | 1.5rem |
| Body | 0.9375rem (15px) | 1rem (16px) |
| Table text | 0.875rem | 0.9375rem |
| Stat card value | 1.5rem | 2rem |

**Font stack:** System UI stack via Bootstrap default, or Inter if loaded.

---

## 7. Touch Targets

Minimum **44×44px** tap targets for:

- Sidebar nav items
- Table row actions
- FAB buttons
- Toggle switches
- Pagination controls

Spacing between tappable icons: minimum 8px.

---

## 8. Module-Specific Layouts

### Dashboard

- KPI cards: 4-across desktop, 2-across tablet, 1-across phone
- Charts: stack vertically on mobile
- Recent orders table: card mode on mobile

### Order create (complex form)

| Section | Mobile | Desktop |
|---------|--------|---------|
| Customer picker | Full width | Left 40% |
| Line items | Stacked cards | Table |
| Totals | Sticky bottom bar | Right sidebar panel |

**Sticky totals bar (mobile):**
```
┌────────────────────────────────────┐
│ Total: $142.50     [Create Order]  │
└────────────────────────────────────┘
```

### Inventory adjust

- Large numeric input centered on mobile
- Big +/- stepper buttons

### Onboarding wizard

- Full viewport height
- Progress stepper: dots on mobile, labeled steps on desktop
- Form max-width 480px centered

### Print / Invoice view

- `@media print` hides sidebar, navbar, buttons
- A4 width (~210mm) centered content

---

## 9. Spacing System

Use Bootstrap spacing utilities consistently:

| Use | Class |
|-----|-------|
| Page padding | `p-3 p-md-4` |
| Card internal | `p-3` |
| Section gap | `mb-4` |
| Form field gap | `mb-3` |
| Button groups | `gap-2` |

---

## 10. Color & State (Bootstrap theme)

Extend existing `_variables.scss`:

| Token | Usage |
|-------|-------|
| `$primary` | Primary actions, links |
| `$success` | Completed orders, success toasts |
| `$warning` | Low stock, pending status |
| `$danger` | Delete, cancelled, out of stock |
| `$secondary` | Muted text, inactive badges |

**Status badges:** Always pair color + text label (not color alone) for accessibility.

---

## 11. Accessibility (Responsive + a11y)

| Requirement | Implementation |
|-------------|----------------|
| Focus visible | Bootstrap `:focus-visible` ring |
| Skip link | "Skip to main content" hidden until focus |
| Modal focus trap | ng-bootstrap or Bootstrap 5 modal |
| Table semantics | `<th scope="col">` always |
| Chart alt | Summary table below chart or `aria-label` |
| Reduced motion | `@media (prefers-reduced-motion: reduce)` disable animations |
| Contrast | WCAG AA minimum 4.5:1 body text |

---

## 12. Images & Media

| Asset | Rule |
|-------|------|
| Product thumbnail | 48×48 list, 120×120 detail; `object-fit: cover` |
| Logo | Max height 40px navbar, 120px settings |
| Empty state illustrations | SVG, max 200px width mobile |

Use `loading="lazy"` on non-critical images.

---

## 13. Performance on Mobile

- Lazy load all feature modules (already planned)
- Skeleton loaders instead of blocking spinners
- Debounce search inputs 300ms
- Virtual scroll for lists >100 rows (Phase 2, CDK virtual scroll)
- Chart data: don't fetch all 5 charts until tab visible

---

## 14. Testing Matrix

| Viewport | Width | Test |
|----------|-------|------|
| iPhone SE | 375px | Login, create order, sidebar drawer |
| iPhone 14 | 390px | Dashboard scroll |
| iPad | 768px | Tables, forms |
| Laptop | 1280px | Full layout |
| Desktop | 1920px | No excessive stretch |

**Tools:** Chrome DevTools device mode, Playwright viewport configs in `playwright.config.ts`.

---

## 15. SCSS File Organization

```
src/styles/
├── _variables.scss      # Breakpoints, colors, spacing
├── _mixins.scss         # respond-to(), truncate()
├── _tables.scss         # Responsive table → card
├── _print.scss          # Invoice print styles
└── styles.scss          # Global imports
```

**Mixin example:**

```scss
@mixin respond-to($breakpoint) {
  @if $breakpoint == md {
    @media (min-width: $breakpoint-md) { @content; }
  }
  // ...
}
```

---

## 16. Do's and Don'ts

| Do | Don't |
|----|-------|
| Stack columns on mobile | Shrink text below 14px body |
| Use card mode for wide tables | Horizontal scroll as only mobile strategy |
| Sticky primary action on mobile forms | Hide delete without confirmation |
| Test with real device touch | Rely on hover-only interactions |
| Collapse secondary filters into sheet | Show 10 filter fields inline on phone |

---

## 17. Related Documents

- [application-pages.md](./application-pages.md) — page layouts
- [navigation-structure.md](./navigation-structure.md) — sidebar behavior
- Existing styles: `BusinessOS.Web/src/styles/_variables.scss`, component SCSS in `shared/components/`
