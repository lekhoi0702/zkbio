---
trigger: always_on
---

# ZKBIO UI Design Rules

You are designing UI for the ZKBIO system.
The visual source of truth is the login page at `Pages/Login.cshtml`.
All new pages, components, and visual updates must inherit that same design language so the product feels like one coherent system, not a collection of unrelated screens.

## Design Intent

- The system should feel secure, modern, calm, and operationally reliable.
- The interface should look polished and high-trust, but still practical for factory and attendance workflows.
- Visual hierarchy must be clear at a glance because users are scanning data, filters, status, and exceptions quickly.
- Keep the tone professional and premium, not playful, overly colorful, or dashboard-noisy.

## Core Visual Language

- Prefer a dark slate foundation with green as the primary brand accent.
- Use soft glass-like surfaces, subtle gradients, and layered depth instead of flat default admin styling.
- Keep corners rounded and shadows soft, never harsh or overly heavy.
- Use clean, high-contrast typography with generous whitespace.
- Support density for data-heavy pages, but preserve elegance through spacing rhythm and consistent alignment.

## Brand Tokens

Use these values as the default visual system unless a page has a strong reason not to.

- Primary background:
  - `#0f172a`
  - `#1e293b`
- Brand green:
  - `#15803d`
  - `#166534`
  - soft accent wash: `rgba(34, 197, 94, 0.16)`
- Text:
  - primary: `#0f172a`
  - secondary: `#334155`
  - muted: `#64748b`
  - inverse text on dark surfaces: `#f8fafc`
- Border:
  - light border: `#dbe3ee`
  - soft glass border: `rgba(255, 255, 255, 0.16)` to `rgba(255, 255, 255, 0.28)`
- Surface:
  - primary surface: `rgba(255, 255, 255, 0.94)`
  - dark overlay surface: `rgba(15, 23, 42, 0.62)` to `rgba(15, 23, 42, 0.9)`
- Shadow:
  - large panel shadow: `0 30px 70px rgba(15, 23, 42, 0.38)`
  - button/card hover shadows should stay soft and green/slate-tinted, not pure black.

## Typography

- Primary typeface: `Manrope`.
- Use bold, compact headings with slightly negative letter spacing for premium feel.
- Prefer these weights:
  - 800 for page hero and major headings
  - 700 for section headings and labels
  - 500 to 600 for body emphasis
  - 400 for long-form support text
- Heading style:
  - large page titles should feel confident and concise
  - avoid generic oversized Bootstrap display styles
- Body text:
  - keep line-height comfortable, usually `1.6` to `1.75`
  - muted text should still remain readable

## Spacing and Shape

- Base radius language:
  - large panels: `24px` to `28px`
  - cards and modals: `18px` to `24px`
  - inputs and buttons: `14px` to `16px`
  - pills and badges: fully rounded or `999px`
- Prefer generous outer spacing and tighter inner data spacing.
- Maintain a consistent rhythm:
  - section gaps: `24px` to `32px`
  - card padding: `20px` to `28px`
  - form field gaps: `14px` to `18px`
  - compact data filters may be denser, but should still follow the same radius and color rules.

## Background and Depth

- Avoid plain flat white pages.
- Each major page should have a sense of depth through one or more of:
  - subtle gradient background
  - softly tinted section backdrop
  - elevated cards over a quieter canvas
- Use decorative glow, blur, or radial highlights sparingly.
- Decorative effects must never reduce legibility of tables, forms, or charts.

## Layout Rules

- Build pages in clearly separated layers:
  - app shell
  - page header
  - filter/action zone
  - main content surface
- Important screens should feel framed, not stretched edge to edge without structure.
- Data-heavy pages can remain efficient, but should sit inside refined containers instead of default bare AdminLTE cards.
- When using split layouts, ensure one side carries brand/context and the other side carries action/content, similar to the login composition.

## Navigation

- Sidebar and top bar must visually connect to the login page palette.
- Replace generic bright blue active states with the brand green system.
- Navigation should emphasize active context with:
  - a tinted active background
  - strong icon and text contrast
  - subtle inset or glow effect
- Keep nav chrome quiet; content should remain the focus.

## Cards and Panels

- Cards should feel like premium operational surfaces.
- Default card style:
  - light surface
  - soft border
  - rounded corners
  - restrained shadow
- Card headers should be simpler and more intentional than AdminLTE defaults.
- Avoid noisy header bars, excessive borders, and saturated title colors.
- If a panel is important, create emphasis with spacing, hierarchy, and surface contrast instead of adding more colors.

## Forms

- Forms should follow the login field language.
- Input rules:
  - height around `48px` to `50px` for regular forms
  - rounded corners `14px` to `16px`
  - left/right padding generous enough to breathe
  - border color should be soft, not stark gray
  - focus state should use green-tinted ring and subtle lift
- Labels should be small, bold, and clear.
- Use icons only when they improve scanning; do not add icons to every field by default.
- Group related filters into visually coherent zones instead of long unstructured rows.

## Buttons

- Primary button:
  - green gradient or solid green from the login palette
  - white text
  - strong weight
  - soft shadow
- Secondary button:
  - white or lightly tinted surface
  - slate text
  - quiet border
- Tertiary/ghost button:
  - minimal chrome
  - only for low-priority actions
- Hover states should feel smooth and slightly elevated, never jumpy or flashy.
- Avoid default Bootstrap blue for primary actions.

## Tables

- Tables must feel integrated with the refined visual system, not like stock admin tables.
- Wrap tables in elevated surfaces with rounded corners.
- Sticky headers should use clean white or lightly tinted backgrounds with clear separation.
- Row states should be subtle:
  - hover with soft tint
  - selected with low-contrast brand wash
  - no aggressive zebra striping unless truly needed
- Status should be expressed with calm badges using semantic colors that still fit the palette.
- Dense data is acceptable, but preserve padding, alignment, and scannability.

## Filters and Search Areas

- Filter sections are not utility leftovers; they are first-class UI blocks.
- Use a structured filter card with:
  - clear title
  - grouped fields
  - consistent action area
  - obvious primary action and reset action
- Export, search, and reset must look like part of one action system.
- Avoid cramming controls into a single row if readability suffers.

## Modals

- Modals should inherit the same surface treatment as cards and forms.
- Use rounded shells, quiet headers, and generous spacing.
- The primary action button inside a modal should match the page primary action style.
- Avoid stock Bootstrap modal visuals if a small amount of custom styling can bring it back into the system.

## Alerts, Badges, and Status

- Feedback components should be clear but restrained.
- Error states:
  - use soft red background with readable red text
  - do not use harsh saturated red blocks
- Success states:
  - use green tones aligned with the brand
- Warning states:
  - use amber carefully and only when meaningfully distinct
- Badges should be rounded, compact, and readable without looking generic.

## Motion and Interaction

- Use motion only to support clarity:
  - hover lift
  - focus ring
  - smooth panel transitions
  - loading progress
- Keep transitions short and polished, generally around `0.2s` to `0.3s`.
- Avoid flashy animations, bouncing, or over-animated dashboards.

## Responsive Behavior

- Mobile layouts should stack cleanly and preserve hierarchy.
- On smaller screens:
  - reduce padding, not design quality
  - keep card radii and visual language intact
  - let multi-column areas collapse into single-column sections
- Do not allow dense filters or table controls to become visually chaotic on narrow screens.

## Consistency Rules

- When creating a new page, reuse the same:
  - color family
  - radius scale
  - shadow logic
  - heading weight
  - input/button behavior
  - spacing rhythm
- Every page should look like it belongs to the same product family as the login page within 2 seconds of opening it.
- If a component looks like untouched AdminLTE or default Bootstrap, it likely needs refinement.

## Implementation Guidance

- Centralize shared tokens in one place before expanding the redesign across pages.
- Prefer CSS custom properties for:
  - color tokens
  - radius tokens
  - shadow tokens
  - spacing tokens
- Create reusable system classes for:
  - app surfaces
  - page headers
  - filter cards
  - action buttons
  - status badges
  - table shells
- Extend AdminLTE carefully; do not fight the framework with random one-off overrides on every page.

## Avoid

- Default Bootstrap blue as the dominant accent
- Flat white pages with no depth
- Mixed radius styles across components
- Inconsistent heading weights and sizes
- Random icon usage
- Overcrowded filter bars
- Heavy borders around everything
- Bright, saturated status colors that clash with the login palette
- One-off page styling that does not map back to the login visual language

## Definition of Done for New UI

A page is considered visually complete only if:

- it clearly belongs to the same family as the login page
- primary actions use the brand accent system
- cards, tables, filters, and modals share the same radius/shadow language
- typography and spacing are deliberate and consistent
- the page remains readable and calm even when data-dense
- desktop and mobile views both feel designed, not merely shrunk
