# CVE Checker | IPS — setup and use

The existing WPF application supports Excel/CSV imports without installing Microsoft Excel. All actions start explicitly. Found means an IPS signature exists in the checked source; it is not a claim that an asset is protected, affected/exploitable, or that a gateway is enforcing a policy.

## Import VA

Open **CVE Checker | IPS → 1 Import & mapping**, select or drop one `.xlsx` / `.csv`. Select the worksheet and header row; click Preview header if changing the header. Map CVE (required), IP Address, Hostname, Plugin/ID, Plugin Name and Severity by column names/indices, then Confirm mapping. Preview shows the first 100 records; all accepted rows are retained. Import issues show invalid/empty CVE and original row/record numbers. Multiple CVE separated by commas, semicolons or whitespace/newlines are normalized/deduplicated per row. No Plugin ID lookup occurs.

XLSX uses standard OpenXML ZIP/XML APIs already available in .NET, not Office automation or a new third-party dependency. CSV supports standard quoting, embedded newlines and comma/semicolon/tab delimiters. Input is value-only: formula expressions are retained as inert text rather than evaluated. Macros/external-link workbooks are rejected. Source files are opened read-only and are never overwritten.

Bounds: 20 MiB file, 10,100 records across workbook sheets (allows metadata/header rows), 256 columns, 32 worksheets, 32,767 characters per cell, 2,000 unique CVE and 200,000 row-CVE associations. XLSX expanded ZIP content is limited to 64 MiB. Split larger reports. A duplicate CVE across 3 assets produces one vendor lookup and three row links, not three requests.

## Palo Alto

On **2 Sources**, enable Palo Alto. Enter your Threat Vault API key in the masked field and explicitly click **Save key securely**. The view clears the entry, and the existing `ISecretVault` implementation stores it in Windows Credential Manager; it is never stored in settings, cache, exports or logs. Remove saved key deletes only this feature's credential reference. No key is required to use Check Point.

Click **Test connection** to request metadata for the first imported CVE (or the documentation's public example when no file is mapped). This is a real provider request when a real key is configured; it does not check gateway configuration. Subscription/organization permissions are determined by the provider. No key was available during development, so live acceptance remains NOT TESTED.

- [CVE coverage endpoint](https://pan.dev/threat-vault/api/get-cve-coverage/)
- [Examples and response shapes](https://pan.dev/threat-vault/docs/examples/get-cve-coverage-information/)
- [Authentication](https://pan.dev/cdss/docs/authentication/)
- [Access limits / headers](https://pan.dev/threat-vault/docs/access-limits/)

The fixed HTTPS endpoint receives only CVE IDs and `X-API-KEY`. Redirects are disabled in production. API responses are bounded at 2 MiB, each full response has a 15-second deadline, and there are at most three attempts. Calls are spaced at least 350 ms; remaining/reset headers and 429/Retry-After are honored. Reset waits exceeding 10 seconds return Unknown/RateLimit for explicit retry later. There is a 2,000-request per-run ceiling including retries. Authentication/authorization rejection disables further HTTP attempts by that adapter in the run. Generic `cve_covered` or Antivirus alone never establishes IPS Found; released/active Vulnerability Protection signatures are required. Development/testing, explicit cannot-cover, monitored/unknown and signature misses are distinct.

## Check Point

In SmartConsole, open **Security Policies → Threat Prevention → IPS Protections**. Make **Industry Reference** and **Protection / Protection Name** visible; optionally show Protection ID and the desired profile action. Use **Actions → Export view**. Only visible columns are exported; see the [official export guide](https://sc1.checkpoint.com/documents/R82/WebAdminGuides/EN/CP_R82_ThreatPrevention_AdminGuide/Content/Topics-TPG/IPS_Protections_for_Custom_Threat_Prevention.htm).

In **2 Sources**, import that CSV, set the header/mapping and optional source date/package/version, then **Apply snapshot**. Protection ID is optional. Dates and versions typed by the user are explicitly attributed as user supplied, not independently verified. Profile columns may have arbitrary names: map the intended column manually. Detect/Prevent/Inactive are kept only as snapshot metadata.

No Check Point Management Server or online catalog query occurs. No match means **Not found in the imported database**, never a claim that Check Point has no signature. Manual official advisory/catalog links are available below the selected CVE; opening them is not recorded as an automated lookup.

## Check / inspect / export

On **3 Coverage results**, click Check coverage. Ready providers work even when the other source is missing; unavailable sources are Unknown. Results include all unique CVE in each vendor's denominator. Use CVE/plugin/asset grouping, filters, selected-CVE evidence, Original source rows and Signatures tabs. Group summaries cover the whole group; the status filter narrows the CVE detail table. A partial match is labeled รองรับบางส่วน.

Cache is separate by vendor/method/account identity or CSV SHA-256 + mapping + user metadata. Cache age and original checked time are shown in evidence. Successful outcomes are reusable for 24 hours. Check Point snapshots older than 7 days or with unknown source date are labeled stale; reimporting an old CSV does not make the source fresh. Refresh bypasses cache. A failed refresh is Unknown for the current attempt and retains old evidence with its original time and an explicit stale/previous label. Unknown/auth failures are not cached as misses. Retry failed/unchecked targets only unresolved jobs. Cache is bounded to 4,096 entries / 128 MiB.

Cancel awaits owned work and preserves completed results. Importing another file cancels/drains the old operation; late results cannot replace the new input. On **4 Export**, choose a new filename. Existing files/source reports are never overwritten. Partial runs require an explicit acknowledgement and get PARTIAL labels plus unchecked counts.

The new XLSX contains Summary, CVE Details, Findings, Signatures, Row CVE Links and Import Issues, with source IDs/times/freshness, freeze headers, filters and wrapped text. Original columns are prefixed `Original.*`; calculated fields use `IPS.*`. Remarks/Status tracking are retained. All exported cells are explicit text, preventing formula interpretation. Export is bounded at 300,000 rows / 32 MiB text and Excel's cell limit; oversized reports fail without leaving a partial destination. No API key/auth data is exported.

The retired Monitoring, Topology Explorer, Inventory, URL Checker and old planner pages cannot be opened from navigation or Tray. Monitoring is not auto-resumed. Dashboard is Coming soon. Their old stores and protected secrets are retained for future use; no migration/deletion is performed and no downgrade compatibility beyond retained data is claimed.
