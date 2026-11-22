---
name: wessl-winui-ux
description: Use when creating, changing, or reviewing Wessl WinUI 3 and Microsoft.UI.Reactor UI, including the tray panel, container lists, dialogs, images, logs, settings, responsive layout, themes, localization, and accessibility.
---

# Wessl WinUI UX

Use this skill before changing a Wessl UI component or reviewing a UI change. Apply the checklist to the real code paths and report what was verified; compiling is not visual validation.

## Project boundaries

- Keep UI declarative in C# with small `Microsoft.UI.Reactor` components. Do not add XAML or MVVM/ViewModels.
- Keep `Wessl.Domain` independent of infrastructure and UI. Keep WSLc process execution, JSON parsing, and Windows-specific launchers in `Wessl.Infrastructure`; keep app composition, lifecycle, polling, and container actions in `Wessl.App` services.
- Reuse existing components, services, localized resources, and theme tokens before creating another pattern.
- Resolve user-visible text through `AppStrings` and Windows App SDK `.resw` resources. Keep keys and placeholders consistent in every supported language.

## Panel and navigation

- Treat the main window as a compact tray panel, not a resizable desktop workspace. Keep its header and summary visible, place useful container content near the top, and let the list use the remaining height.
- Recalculate panel placement on every tray open using the cursor's monitor and the actual taskbar edge. Center it along that edge, account for auto-hide taskbars and monitor-specific pixel scaling, and clamp it to the monitor work area. If the cursor position is unavailable, use a safe primary-monitor placement.
- Preserve visible focus on opening, dismissal on deactivation, and independent tool windows. Do not make the panel always-on-top, draggable, or dependent on enlarging the window.
- Keep page headers and back navigation outside scrolling content. Return keyboard focus to a recognizable destination after navigation or dialog dismissal.
- Prefer native WinUI controls and Fluent styles; use semantic theme resources that adapt to light, dark, and high-contrast modes.

## Measure, overflow, and scrolling

- Give scrolling regions finite space: use a grid with a bounded `Star` content row and `Auto` actions/header rows. Do not put an unconstrained `ScrollViewer` inside a vertically infinite stack and assume it will scroll.
- Bound long content to the viewport. A disabled horizontal scroll mode does not constrain a child's measured width; use star columns with `MinSize(0)`, wrapping, and deliberate trimming where needed.
- Avoid nested scroll viewers unless each scroll region has a clear purpose and independent keyboard/automation behavior.
- Keep destructive-dialog actions outside long scrolling content. Test that the final field, buttons, and dialog close/cancel actions are reachable.
- For long identifiers, show a readable wrapped or ellipsized form without losing access to the full value; provide a copy action when wrapping is insufficient.

## Compact and responsive controls

- Keep the panel's primary task visible before secondary controls. Collapse advanced filters, grouping, and sorting behind a compact native action; show bulk actions only when selection exists.
- Do not use rigid horizontal rows for localized action labels. Prefer wrap or a native overflow command surface, and verify a 360-DIP logs window.
- Keep buttons, labels, and typography at their normal readable sizes. Do not shrink fonts or expand windows indiscriminately to hide overflow.
- Use intentional spacing on a 4-DIP rhythm. Omit absent messages and fields instead of rendering empty text elements that reserve height.

## State and feedback

- Represent first load, confirmed empty, loaded data, refresh-in-progress, stale last-successful data, and failure as distinct states. Never show an unconfirmed zero count or an empty result as if a failed request succeeded.
- If a refresh fails after success, keep the previous data visible, identify it as previous data, and provide retry. If the first request fails, show the error and a direct retry action.
- Use `InfoBar` with a concise message, correct semantic severity, and closability appropriate to whether the message needs dismissal. Keep long actionable error details readable; do not make an error look successful.
- Report success only after the operation succeeds. Clear a dismissed transient message from component state so a later render does not reopen it.
- Keep empty and unavailable states explicit and actionable. Do not repeat loading text or create placeholder rows just to fill space.

## Safe operations and selection

- Make the current operation's target scope explicit, including selected rows hidden by search or filters. Preserve selections across filtering only when the UI discloses hidden selections and supports reviewing or clearing them.
- Identify concrete target names in destructive confirmations; cap the visible list and state when more targets are included.
- Make cancellation unambiguous and the safe action the dialog's default. Do not run destructive work when Enter is pressed from an unsafe default focus.
- Disable conflicting actions while work is pending and revalidate target state immediately before destructive operations.
- Keep details copy limited to non-secret fields. Never display or copy environment-variable values.

## Accessibility and input

- Give every interactive control an appropriate accessible name, role, and state. A tooltip alone is not an accessible name or state.
- Use a native `CheckBox` or native list selection for selection; keep the checkbox name associated with the container row and expose its checked state. Verify Space toggles it.
- Verify Tab order, Enter, Space, Escape, initial focus, focus restoration, and keyboard access to full diagnostics. Do not rely on hover-only tooltips for important error content.
- Retain semantic text and icons for state; never communicate health or action outcome by color alone.
- For dialogs, use native `ContentDialog` buttons, a safe default, and content that remains readable at enlarged text sizes.

## Theme, language, and diagnostics

- Apply the selected or automatic theme to every app-owned window when created and whenever the preference changes. Do not hardcode colors that bypass theme or high-contrast resources.
- Localize all new visible strings in every supported `.resw` language. Keep placeholders aligned and check long translations, singular/plural wording, and local date/time presentation.
- Format timestamps for people in local time while preserving original technical values in copied diagnostics.
- Keep execution state separate from health. Highlight unhealthy/starting states with a semantic icon and text; omit unavailable metadata instead of repeating several “unavailable” lines.
- Keep full diagnostics readable with keyboard and assistive technology; do not expose a truncated fixed-height error whose only complete form is a hover tooltip.

## Safe verification workflow

1. Identify the main task, window size, relevant controls, data lengths, request states, and dependencies before editing.
2. Search the existing component/service patterns and reuse the correct one.
3. Implement a focused change with bounded layout, explicit keyboard behavior, and updated localized resources.
4. Validate normal and long content, first load, confirmed empty, refresh, prior data plus error, and safe cancellation using demo data or isolated doubles.
5. Inspect the final viewport and bottom-most actions; use UI Automation for accessible name, role, state, and off-screen status when available. Use Narrator for selection, dialogs, and navigation when available.
6. Exercise narrow sizes, 100/125/150/200% scaling, enlarged text, Spanish and another supported language, light/dark, and high contrast where the environment permits.
7. Report visual confirmation, code-only evidence, and unvalidated cases separately. Distinguish effective DIPs from physical pixels. Never use real containers or perform destructive verification; capture only the app surface, not private desktop or file-picker content.

### Avoid / prefer

| Avoid | Prefer |
| --- | --- |
| `ScrollViewer(VStack(...))` with no bounded height | `Grid` with `Star` content and `Auto` fixed actions |
| Disabling horizontal input while long text still measures wide | A constrained star column, wrapping/trimming, and full-value access |
| Showing bulk actions for no selection or silently hiding filtered targets | A conditional selection surface with hidden-count review and named confirmations |
| Rendering an error through `.Success()` or showing empty after a failed first load | Explicit error severity, retained-data notice, and retry |
| An icon-only selection `Button` with no toggle state | A native named `CheckBox` with automation state |
| Fixed action rows, empty `TextBlock`s, and tooltip-only diagnostics | Wrapped commands, omitted absent rows, and readable full error text |

## Official references

- [Fluent 2 layout](https://fluent2.microsoft.design/layout)
- [WinUI dialogs and flyouts](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs)
- [Responsive layout and breakpoints](https://learn.microsoft.com/en-us/windows/apps/design/layout/screen-sizes-and-breakpoints-for-responsive-design)
- [Accessibility name, role, and value](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/basic-accessibility-information)
- [WinUI CheckBox](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/checkbox)
- [WinUI InfoBar](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/infobar)
- [Keyboard interactions](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-interactions)
- [Windows app best practices](https://learn.microsoft.com/en-us/windows/apps/get-started/best-practices)
- [Windows typography](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/typography)

Use WinUI and Fluent guidance in context: do not copy web, iOS, or Android touch-target sizes into a compact Windows panel without checking the native control and keyboard experience.
