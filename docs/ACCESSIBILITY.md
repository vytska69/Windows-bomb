# Accessibility (NVDA / JAWS / Narrator)

The GUI was built with full screen-reader usability as a hard requirement, not an afterthought. Design
choices and where to look in the code:

## Native controls only

Every interactive element is a stock WPF control (`Button`, `CheckBox`, `TextBox`, `ListBox`,
`ItemsControl`, `TabControl`, `GroupBox`, `ProgressBar`, `PasswordBox`) — none of them are custom-drawn.
Stock controls come with correct UI Automation peers out of the box; custom-drawn controls generally
don't, and get that wrong in ways that are easy to miss without an actual screen reader running.

## Every control has an accessible name

Labels are associated two ways, redundantly, because different screen readers pick up different things
most reliably:

- A visible WPF `Label` with `Target="{Binding ElementName=...}"` (also gives the label an access-key
  underline and lets clicking the label focus the control).
- An explicit `AutomationProperties.Name` on the control itself with the same text, so the accessible
  name doesn't depend on label association resolving correctly.

Checklist items (the removable-apps list) set `AutomationProperties.Name` to a string that includes
both the friendly name and the real package name (`AppPackageOption.AccessibleDescription`), so two
apps that might share a friendly label are still distinguishable by ear.

## Keyboard access keys, checked for collisions

Every tab and primary action button has an `Alt+`-reachable access key (the underscored letter in its
`Content`/`Header`). These were checked by hand for collisions within the same visible scope — e.g. the
"Kūrimas" tab (access key **K**) and the "Kurti ISO" button inside it originally both wanted **K**; the
button was changed to **U** (`K_urti`) instead. Secondary "Browse..." buttons that repeat across tabs
were left without a mnemonic on purpose (there are up to five of them) rather than risk accidental
collisions — they're still fully reachable via Tab or a screen reader's button list.

## No focus traps

`KeyboardNavigation.TabNavigation="Local"` was deliberately **not** used anywhere in the tree. Early in
development one tab's root panel had it set, which would have trapped Tab-key focus inside that panel,
making the always-visible progress bar / status line / log below the `TabControl` unreachable by
keyboard while that tab was open. The default `Continue` behavior is used everywhere so Tab always
eventually reaches every control on the window.

## The Cancel button stays reachable while a job is running

`RunCommand`, `LoadSourceCommand`, and `ExportDriversCommand` each disable themselves via their own
`CanExecute` while a job is running. The `TabControl` itself is deliberately **not** disabled as a
whole during a run — the Cancel button lives inside the "Kūrimas" tab's content, so disabling the
whole `TabControl` while busy would have made Cancel unreachable exactly when someone needs it.

## Live status announcements

`StatusMessage` (one line, updated on every progress callback from `IsoOptimizationJob`) is bound to a
`TextBlock` with `AutomationProperties.LiveSetting="Polite"`. Setting `LiveSetting` alone only marks the
region as live — WPF does not automatically raise the UI Automation event when the bound text changes.
`MainWindow.xaml.cs` subscribes to the view model's `PropertyChanged` and explicitly calls
`UIElementAutomationPeer.FromElement(StatusLiveRegion).RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)`
whenever `StatusMessage` changes, so NVDA/JAWS/Narrator actually announce each new status line as the
job progresses, instead of only reading it if the user happens to navigate to that element.

The full history of every status line is also kept in a plain `ListBox` (`LogMessages`) below the live
region, so a screen reader user can review everything that happened after the fact, not just the most
recent line.

The update-available banner's own `TextBlock` also carries `AutomationProperties.LiveSetting="Polite"`,
but — per the same caveat above — that alone doesn't make WPF raise the UIA event, and this one isn't
individually wired up the way `StatusLiveRegion` is. It doesn't need to be: `CheckForUpdatesAsync` calls
`Log(...)` when it finds a newer release, which sets `StatusMessage` too, so the announcement happens
once through the same already-wired pipe. The banner stays on screen and keyboard-reachable afterward
for anyone who wants to revisit it, it just isn't a second independent announcement source.

## Respecting system settings

- `app.manifest` declares per-monitor v2 DPI awareness, so text/control sizing stays correct on
  mixed-DPI multi-monitor setups (relevant for low-vision users using a high-DPI display alongside a
  standard one).
- No hardcoded colors are used anywhere in the XAML; all visuals come from default WPF/system theme
  brushes, so Windows High Contrast themes apply correctly.

## What still needs manual verification

This was all built and reasoned about without a Windows machine or an actual screen reader in this
environment (see the top-level README for why — WPF only builds on Windows). Before shipping, run
through the whole workflow with NVDA, JAWS, and Narrator on real Windows and confirm:

- Tab order matches visual/logical order on every tab.
- The live region is actually announced by all three screen readers during a long-running job (timing
  and verbosity of live-region announcements is one of the areas where NVDA/JAWS/Narrator differ the
  most in practice).
- The checklist of removable apps reads sensibly item-by-item when the list is long.
