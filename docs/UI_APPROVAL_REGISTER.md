# UI approval register

Snapshot: 2026-10-09. Source bytes and explicit human decisions govern approval; this planning task does not approve future screens.

| Screen/change | Design approval | Located evidence | Current implementation / remaining verification |
|---|---|---|---|
| Original Live Ping | Approved | `design/references/LivePing_APPROVED.png`, `docs/APPROVED_UI_SCOPE.md` | Implemented; actual WPF renders verified historically; full human acceptance open |
| Original Traceroute | Approved | `design/references/Traceroute_APPROVED.png`, same scope | Implemented IPv4 ICMP; remote multi-hop acceptance open |
| Compact probe dialogs, framed window, templates/history, tables, independent sessions/polling | Approved human revision | `docs/UI_REVISION_2026-10-08.md` | Implemented; older eight-session limit superseded by five in 0.4.0 |
| Port Test and bottom Settings/Updates group | Approved human extension | `docs/FEATURES_0.2.0.md` | Implemented; current TCP/UDP scope extends it |
| Selected-build Install/Reinstall/Recover | Approved explicit-click extension | `docs/FEATURES_0.3.0.md` | Implemented; machine installer scenarios NOT TESTED |
| Supplied logo, Light/Dark/System, resizable/folding panels, shared hop mapping, five trace sessions, multiple-target TCP/UDP scans | Approved human-reviewed mockup/revision | `docs/FEATURES_0.4.0.md`; actual WPF QA in `artifacts/github-release/0.4.0/final/windows-qa/` | Implemented/released; interactive source mockup is not checked into `design/references/` |
| Network Info / Dashboard / dedicated DNS-HTTP-Health pages / History-Reports | Concept; final UI pending | Planning brief; no final approved reference located | UI implementation BLOCKED until approved or functional-only prototype authorized |
| Wi-Fi Manager / Monitoring / Terminal / Topology | Mockup — Awaiting Approval, reported by planning brief | No matching mockup file located in tracked repository | Request/locate supplied mockup before review; do not reconstruct or assume approval |
| Config Collector v2 | Mockup — Awaiting Approval, reported by brief | No v2 mockup file located | Multiline targets requirement preserved; UI implementation BLOCKED |
| MA Inventory / Device Intelligence / CVE / Upgrade Planner | Concept | Planning brief only | Approval and implementation pending |
| Account/license/billing | Future Idea / On Hold | Explicitly postponed in planning brief | No current navigation or payment integration |

## Located branding

The supplied unchanged raster is `src/NetStucked.Desktop/Resources/Brand/NetStuckedLogo.png`, SHA-256 `ed3030618f1d49de647e621f6db57d965313e0950881af44b1edd8b6e9049938`. Current derived/distributed resources are `NetStucked.ico`, `NetStuckedBanner.png`, `WizardLogo.png`, `WizardMark.png` in the same folder and `docs/branding/NetStuckedBanner.png`.

The planning brief calls the selected concept “Concept 1” and describes N/orbit/server/Wi-Fi motifs and a brand pack. The exact concept label, complete brand-pack approval and SVG/full-horizontal vector variants cannot be established from located files. No SVG was found. Preserve supplied assets; do not claim missing artwork exists or replace it.

## Precedence and immutable baseline

Original scope and screenshots remain byte-for-byte unchanged. Their obsolete version/scope statements describe 0.1.0 only. Later explicit human extensions govern only the changes they name. Minimal Light remains the baseline; approved Dark/System are supported. Settings and Updates remain separate bottom-group navigation items under the approved current design; the proposed logical Settings > Updates grouping does not authorize moving them now.

| Protected file | SHA-256 verified in this audit |
|---|---|
| `docs/APPROVED_UI_SCOPE.md` | `0f722da815555532a330e1ae9c5121778aa184cc740c94da82cb7e65d3003776` |
| `design/references/LivePing_APPROVED.png` | `45f941795fa7347a824976b79f73cd61f029778f7afecc246df302e181d55772` |
| `design/references/Traceroute_APPROVED.png` | `e8f8f3ad08d9d9ca844bc2f2f3326910df74583afe52dbc4730c19085f2cb2f1` |
