---
name: DWM LUT
description: Restrained neutral materials for a native Windows calibration utility.
colors:
  light-base: "#FAFAFB"
  light-surface: "#FFFFFFF5"
  light-navigation: "#ECECEEDD"
  light-stroke: "#DADADD"
  light-text: "#242426"
  light-secondary-text: "#646469"
  light-link: "#36363B"
  light-selection: "#DEDEE3"
  light-focus: "#4C4C54"
  light-accent: "#343437"
  light-chip: "#D8D8DC66"
  light-chip-stroke: "#B0B0B533"
  dark-base: "#242426"
  dark-surface: "#303033F5"
  dark-navigation: "#2B2B2EF5"
  dark-stroke: "#49494E"
  dark-text: "#F2F2F4"
  dark-secondary-text: "#B8B8BF"
  dark-link: "#DEDEE3"
  dark-selection: "#46464C"
  dark-selection-text: "#FFFFFF"
  dark-focus: "#C8C8D0"
  dark-accent: "#DEDEE3"
  dark-chip: "#4E4E5466"
  dark-chip-stroke: "#5D5D6566"
typography:
  page-title:
    fontFamily: "Segoe UI"
    fontSize: "25px"
    fontWeight: 600
  section-title:
    fontFamily: "Segoe UI"
    fontSize: "15px"
    fontWeight: 600
  subtitle:
    fontFamily: "Segoe UI"
    fontSize: "13px"
  help:
    fontFamily: "Segoe UI"
    fontSize: "12px"
rounded:
  chip: "7px"
  control: "8px"
  navigation-item: "9px"
  card: "12px"
  navigation: "16px"
spacing:
  small: "8px"
  control-horizontal: "12px"
  card: "16px"
  page: "20px"
components:
  button:
    rounded: "{rounded.control}"
    padding: "5px 12px"
  card:
    backgroundColor: "{colors.light-surface}"
    textColor: "{colors.light-text}"
    rounded: "{rounded.card}"
    padding: "{spacing.card}"
  card-dark:
    backgroundColor: "{colors.dark-surface}"
    textColor: "{colors.dark-text}"
    rounded: "{rounded.card}"
    padding: "{spacing.card}"
  navigation-item:
    rounded: "{rounded.navigation-item}"
    height: "42px"
    padding: "0 12px"
  shortcut-capture:
    backgroundColor: "{colors.light-surface}"
    textColor: "{colors.light-text}"
    padding: "14px"
  chip:
    backgroundColor: "{colors.light-chip}"
    rounded: "{rounded.chip}"
    padding: "5px 9px"
---

# Design System: DWM LUT

## Overview

**Creative North Star: "Neutral calibration utility"**

Apple-like liquid-glass restraint, expressed through native WPF and WPF-UI controls. White and dark gray keep calibration tasks legible; translucency and soft navigation depth stay subordinate to labels and state. Segoe UI is native interface typography, not a decorative display voice.

**Key Characteristics:**

- White or dark-gray ground with neutral semantic brushes.
- Compact labeled controls and short supporting explanations.
- Generated monitor/LUT identity with a quiet navigation icon family.

This is the finished code-led, user-pinned world; it has no catalog seed or approved comp. Source authority: `Themes/LiquidGlass.xaml`, runtime `Services/ThemeService.cs`, and `Views/` under `src/DwmLutModern.App`. Frontmatter uses portable CSS hex order (RRGGBBAA); WPF source uses AARRGGBB. All layout/type measurements denote WPF device-independent units (DIPs), represented as px in the portable token schema.

## Colors

### Primary

The charcoal/light-gray accent pair supports native control emphasis. The high-contrast text on accent is white in light mode and the light-text charcoal in dark mode. Underlined links use their dedicated opaque link brushes.

### Neutral

Each theme supplies base, translucent surface, navigation, stroke, primary text, secondary text, selection, focus and chip roles. Light selection text reuses light text; dark selection text is white. Runtime ThemeService values are normative after application startup, including selection and focus; initial dictionary defaults differ slightly. System mode follows Windows. High contrast replaces the main semantic brushes with Windows Window, WindowText, Highlight, HighlightText and HotTrack brushes; it is not a third fixed palette.

**The Neutral Material Rule.** Use neutral surfaces, edges and interaction accents. Reserve color for meaningful status and the monitor/LUT identity.

## Typography

Segoe UI provides native interface text through WPF-UI inheritance and the pinned Windows direction. The shared hierarchy uses page titles, section titles, subtitles and help from the frontmatter; setting labels are semibold at the inherited control size. Display status/About titles use 20 DIP semibold. Shortcut capture uses a 16-DIP semibold prompt and 18-DIP semibold value. Documentation paragraphs use 21-DIP line height. Keep native font fallback for localized scripts; do not introduce an editorial display face.

**The Shared Control Rule.** Reuse WPF-UI control behavior and the shared local styles; preserve visible keyboard focus and disabled states.

## Layout

The default shell is 1120 × 820 DIPs, minimum 960 × 680, with a 36-DIP title bar and fixed 210-DIP navigation column. Pages scroll vertically with 20/16/20/20-DIP left/top/right/bottom padding. Settings use flexible labels on the left and content-sized controls on the right; help wraps below. Selectors are 180 DIPs wide, with 310 DIPs for dithering. Related rows separate by 4–8 DIPs; section titles use 20 DIPs above and 8 below. This is a resizable desktop layout with a minimum width, not a mobile breakpoint system.

Documentation uses expandable topics and left-aligned body text with a 560-DIP maximum width and 12-DIP padding. Shortcut values trim beyond 160 DIPs and retain a tooltip; matched actions use a 74-DIP minimum width and 8-DIP separation. The capture window is 480 DIPs wide, sizes to its content height, and centers on its owner.

## Elevation & Depth

Translucent neutral fills, one-DIP edges and restrained navigation depth convey glass. The shell and shortcut window explicitly use no backdrop effect: this is not live blurred wallpaper or optical refraction. Shared navigation has a black WPF DropShadowEffect: blur radius 12, depth 3, opacity 0.07. Content cards have no authored shadow.

**The Quiet Depth Rule.** Keep content cards on thin neutral edges; the navigation alone carries the shared soft offset shadow.

## Shapes

Use the frontmatter corner roles: small chips, rounded controls, navigation items, cards, and the larger navigation/hero container. GlassHero extends the card to 20-DIP padding and 16-DIP corners. Borders are one DIP, with layout rounding and device-pixel snapping where supplied. Keep the existing native WPF-UI outlines on selectors and expanders.

## Components

- **Buttons and selectors:** WPF-UI/native templates own hover, pressed, disabled and focus behavior. Shared buttons have a 34-DIP minimum height, control corners and compact padding; ComboBox shares the minimum height. Shortcut dialog actions use 36-DIP minimum height and 88-DIP minimum width.
- **Navigation:** Five labeled radio destinations, generated 22-DIP images, 42-DIP rows, neutral hover fill and selected semibold text. Keyboard focus adds a one-DIP semantic focus edge independently of selection.
- **Cards and chips:** Use shared surface/stroke resources and padding, not independent page palettes. Chips are small mode/status containers, not general page scaffolding.
- **Shortcut capture:** Read-only centered capture field, 64-DIP minimum height, explicit instructions, validation and Save/Cancel. Preserve cyclic Tab navigation, default/cancel actions and registration feedback.
- **Settings and documentation:** Mode changes update explanatory text immediately. Documentation opens the relevant expandable topic; it keeps the same shell and native controls.
- **Motion:** Mouse-initiated page swaps fade content from 0.94 to 1 opacity over 100 ms with quadratic ease-out only when Windows ClientAreaAnimation is enabled. Keyboard/programmatic swaps are immediate; there is no slide or scale choreography.
- **Identity:** Runtime PNGs live in `Assets/Icons/`; `Assets/app.ico` packages the app mark. Use `provenance.json`, `app-edit-provenance.json`, prompts, originals and `conversion.json` for generation/conversion history. App mark appears at 20/38 DIPs in shell chrome/sidebar. The selected app edit is accepted at runtime sizes in the parent review and independent Windows finish review. Its larger source retains a documented perimeter-cleanup limitation; no such noise is a reusable design rule.

Evidence: actual Windows captures in `../Research/2026-10-29-ui/proof-3752cba/artifacts/ui-captures`, sampled directly in light/dark Settings, narrow Settings and dark Documentation; source sampled across shell, settings, documentation and capture dialog. The independent finish review and follow-up verdict live in `.workflow/ui-refresh/results/`. The follow-up ships its two scored fixes only; this system record does not certify GPU playback, performance or every interaction. Sidecar HTML/CSS is a portable visual preview of native primitives, not runtime WPF code.

## Do's and Don'ts

### Do:
- **Do** use semantic DynamicResource brushes for both themes and Windows high contrast.
- **Do** keep settings labels beside their controls, with wrapping help beneath.
- **Do** preserve literal destinations, visible shortcut values, and matched Edit/Remove actions.
- **Do** keep technical reading at the implemented 560-DIP maximum measure.

### Don't:
- **Don't** add purple/blue ambient glows, slogans, or a replacement visual identity.
- **Don't** turn the generated icon's remaining perimeter artifacts into a material treatment.
- **Don't** replace native keyboard behavior with animated navigation or icon-only actions.
