# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - Unreleased

### Added
- Virtualized row rendering via Blazor `<Virtualize>` for high-performance display of large datasets
- Multi-cell rectangular selection with mouse (click, click-drag) and keyboard (Shift+Arrow)
- Full keyboard navigation: Arrow keys, Home/End, Page Up/Down, Tab, F2, printable character to begin editing
- Ctrl/⌘ modifier support: Ctrl+Home/End for edge-jumping, Ctrl+C/V for copy/paste
- Inline cell editing with text input and optional combo-box dropdown
- Combo-box dropdowns with live filtering and keyboard navigation
- Copy/paste as TSV (Excel-compatible clipboard format)
- Formula stripping on paste (leading `=` removed for safety)
- Column sorting: ascending, descending, and unsorted states
- Column filtering with value-list checkboxes and live search
- Date range filtering supporting multiple formats (YYYY, MM-DD, MM-DD-YY, MM-DD-YYYY)
- Column resizing by drag on the resize grip in the header
- Custom cell templates via `RenderFragment`
- Per-row editability control via `CellEditableGetter` callback
- Row numbering gutter (opt-in via `ShowRowNumbers`)
- Row banding / alternating row colors (enabled by default, opt-out via `RowBanding`)
- CSS custom property theming for all colors and borders
- `OnSelectionChanged` event with row, column, and range details
- `OnKeyPressed` event for intercepting unhandled key presses
- `OnColumnResized` callback with column index and new width
- Public methods: `ForceRerender()`, `ScrollToEnd()`, `SelectRow()`
- Right-click context menu with sort and filter shortcuts
- Blazor Server and WebAssembly demo projects
