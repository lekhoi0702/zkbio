# ZKBIO UI Design Rules (Refined - 2026)

You are designing UI for the ZKBIO system. 
The visual source of truth is the refined **All Transaction** page and the minimalist **Login** page.
All new layouts must follow this "Borderless & High-Density" aesthetic.

## Design Philosophy
- **Extreme Precision**: Maximize data visibility by removing all non-essential headers and decorative text.
- **Borderless Flat**: Avoid card borders; use subtle surface contrast and generous whitespace for separation.
- **Operational Speed**: Use smart components like Date Presets to reduce user friction.
- **Clean Sidebar**: No icons in navigations; use solid, full-width active indicators.

## Core Visual Language

### Colors & Surfaces
- **App Wrapper**: Content area should fill the viewport height perfectly (`flex: 1`).
- **Surface**: Pure white (`#ffffff`) for action cards.
- **Background**: Extremely subtle slate tint for even-numbered records (`#f5f8fb`) to prevent eye fatigue.
- **Active State**: Brand green gradient (`linear-gradient(135deg, #15803d, #22c55e)`).

### Typography
- **Page Titles**: Compact, bold (`0.95rem`, weight 700).
- **Data Table**: Small but readable (`0.8rem` or `0.85rem`).
- **Labels**: Bold, uppercase-capable but small (`0.75rem`).

## Component Specifications

### 1. Sidebar (Minimalist)
- **Icons**: No icons in the navigation list.
- **Expanded Active**: 
  - Full-width background (no horizontal margin/padding on the link).
  - No border-radius (sharp rectangular bar).
  - No box-shadow.
- **Collapsed (Mini) Active**:
  - Transparent background.
  - 3px solid green left border (`#22c55e`) as the only indicator.
  - Height reduced to `36px`.

### 2. Login Page (Minimalist & Narrative-free)
- **Content**: Strictly no marketing text or system descriptions.
- **Left Panel (Brand)**:
  - Central visual: 4 concentric pulsing rings (`animation: ring-pulse`).
  - Core Icon: `favicon_cv.png` in a circular glass container (`50% radius`).
  - Footer Strip: `ACCESS CONTROL · ATTENDANCE` in uppercase small text.
- **Right Panel (Form)**:
  - Input Icons: Must have `z-index: 1` and `pointer-events: none` to stay visible above focused inputs.
  - Input Height: `50px` with `16px` radius.

### 3. Data Tables & AllTransaction
- **Density**: Row padding fixed at `5px 10px`.
- **Alternating Rows**: `nth-child(even)` color `#f5f8fb`.
- **Borders**: No borders on the main card containers.
- **Alignment**: Page Title, Filters, and Table must share identical horizontal margins (`20px`).
- **Footer**: Place record total count inline with pagination for zero-waste spacing.

### 4. Smart Filters
- **Date Presets**: Use "Pill" style buttons (`Today`, `Yesterday`, etc.) above date inputs for 1-click filtering.
- **Compact View**: Labels above inputs, total height minimized to keep more records on screen.

## Execution Rules
1. **Remove Breadcrumbs**: Do not use AdminLTE breadcrumbs.
2. **Remove Card Headers**: Primary data cards should not have a "header bar" unless it contains complex actions.
3. **Icons**: Use icons only for buttons (Export, Search) or specific status badges, not for menu items.
4. **Spacing**: Every page must use `flex: 1 1 0` with `min-height: 0` to fill the remaining viewport precisely without bottom gaps.
