# Approved WPF UI

Use `design/references/LivePing_APPROVED.png` and `Traceroute_APPROVED.png` as visual truth. Shared Segoe UI, off-white canvas, white compact cards, blue accent, thin gray borders, subtle rounded corners and minimal shadows. Status text accompanies color. Native WPF supports Thai text; no redistributed fonts.

Sidebar width about 182 logical pixels. Live Ping target card spans the full content height; Load/Save/settings above editor and the only probe controls below. Six small top-right KPI cards, dense upper results grid and lower selected-host history. Traceroute target/results left (about 70%), full-height log right; no KPI row. Settings are modal rather than permanent fields.

Data grids retain legible 12px fonts, virtualize rows/columns and scroll horizontally. Thin 6px scrollbar tracks, compact buttons, keyboard focus and AutomationProperties.Name for icons. Window is resizable, PerMonitorV2 DPI-aware. Readable table scrolling takes priority over fitting every column at 1280px. No Clear, global search, tips, duplicate actions or default chart. Chart is disabled pending a robust implementation.
