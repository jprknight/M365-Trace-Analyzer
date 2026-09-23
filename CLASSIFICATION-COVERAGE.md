# Classification coverage

M365 Trace Analyzer application version **1.0.2** includes ruleset schema version **1**.

The application includes **151 session classifications**, implemented by **43 processing components**. A processing component can produce multiple user-facing classifications, so the component count is not expected to match the classification count.

## Coverage summary

- Standard HTTP response classifications and unknown-status handling
- Specialized HTTP 0, 200, 302, 307, 400, 401, 403, 404, 456, 500, 502, 503, and 504 analysis
- Hidden HTTP 200 failures, including Client Access Rules, MAPI protocol and culture errors, malformed JSON, Autodiscover validation, Free/Busy failures, and errors returned inside successful bodies
- Exchange Online and Exchange Server MAPI, EWS, Autodiscover, OWA, attachment, RPC, NSPI, REST People, suggestions, and Free/Busy classifications
- Bearer, basic, SAML, and missing-authentication-header classifications
- Response-server classification
- Apache Autodiscover, loopback, and NetLog broad checks
- Long-running session warning and severe thresholds

Rules execute in explicit phases and deterministic order. Specialized rules take precedence over generic fallbacks, while confidence values prevent less-specific classifications from replacing stronger results.

## Technical implementation inventory

| Implementation ID | Phase | Order | Implementation |
|---|---:|---:|---|
| `M365.Broad.ApacheAutodiscover` | BroadChecks | 100 | `ApacheAutodiscoverRule` |
| `M365.Broad.Loopback` | BroadChecks | 200 | `LoopbackRule` |
| `M365.Broad.NetLogMock` | BroadChecks | 300 | `NetLogMockSessionRule` |
| `M365.HTTP.SimpleStatus` | ResponseCode | 10 | `SimpleHttpStatusRule` |
| `M365.HTTP.0` | ResponseCode | 100 | `Http0Rules` |
| `M365.HTTP.200.ConnectTunnel` | ResponseCode | 100 | `Http200ConnectTunnelRule` |
| `M365.HTTP.302` | ResponseCode | 100 | `Http302Rules` |
| `M365.HTTP.307` | ResponseCode | 100 | `Http307Rules` |
| `M365.HTTP.400` | ResponseCode | 100 | `Http400Rules` |
| `M365.HTTP.401` | ResponseCode | 100 | `Http401Rules` |
| `M365.HTTP.403` | ResponseCode | 100 | `Http403Rules` |
| `M365.HTTP.404` | ResponseCode | 100 | `Http404Rule` |
| `M365.HTTP.456` | ResponseCode | 100 | `Http456Rules` |
| `M365.HTTP.500` | ResponseCode | 100 | `Http500Rules` |
| `M365.HTTP.502` | ResponseCode | 100 | `Http502Rules` |
| `M365.HTTP.503.FederatedStsUnavailable` | ResponseCode | 100 | `FederatedStsUnavailableRule` |
| `M365.HTTP.504` | ResponseCode | 100 | `Http504Rules` |
| `M365.HTTP.200.ClientAccessRule` | ResponseCode | 200 | `Http200ClientAccessRule` |
| `M365.HTTP.503.OwaCreateAttachment` | ResponseCode | 200 | `OwaCreateAttachmentUnavailableRule` |
| `M365.HTTP.200.CultureNotFound` | ResponseCode | 300 | `Http200CultureNotFoundRule` |
| `M365.HTTP.200.MapiProtocolDisabled` | ResponseCode | 400 | `Http200MapiProtocolDisabledRule` |
| `M365.HTTP.200.OwaAttachment` | ResponseCode | 500 | `Http200AttachmentRule` |
| `M365.HTTP.200.Autodiscover` | ResponseCode | 600 | `Http200AutodiscoverRule` |
| `M365.HTTP.200.UnifiedGroups` | ResponseCode | 700 | `Http200UnifiedGroupsRule` |
| `M365.HTTP.200.FreeBusy` | ResponseCode | 800 | `Http200FreeBusyRule` |
| `M365.HTTP.200.Mapi` | ResponseCode | 900 | `Http200MapiRule` |
| `M365.HTTP.200.OwaNotificationChannel` | ResponseCode | 1000 | `Http200OwaNotificationRule` |
| `M365.HTTP.503.ServiceUnavailable` | ResponseCode | 1000 | `GenericServiceUnavailableRule` |
| `M365.HTTP.200.Owa` | ResponseCode | 1100 | `Http200OwaRule` |
| `M365.HTTP.200.OutlookRpc` | ResponseCode | 1200 | `Http200RpcRule` |
| `M365.HTTP.200.OutlookNspi` | ResponseCode | 1300 | `Http200NspiRule` |
| `M365.HTTP.200.Suggestions` | ResponseCode | 1400 | `Http200SuggestionsRule` |
| `M365.HTTP.200.RestPeople` | ResponseCode | 1500 | `Http200RestPeopleRule` |
| `M365.HTTP.200.Ews` | ResponseCode | 1600 | `Http200EwsRule` |
| `M365.HTTP.200.Json` | ResponseCode | 1700 | `Http200JsonRule` |
| `M365.HTTP.200.Javascript` | ResponseCode | 1800 | `Http200JavascriptRule` |
| `M365.HTTP.200.LurkingErrors` | ResponseCode | 1900 | `Http200LurkingErrorsRule` |
| `M365.HTTP.200.Ok` | ResponseCode | 10000 | `Http200FallbackRule` |
| `M365.HTTP.UnknownStatus` | ResponseCode | 20000 | `UnknownHttpStatusRule` |
| `M365.Authentication` | Authentication | 100 | `AuthenticationRule` |
| `M365.SessionType` | SessionType | 100 | `SessionTypeRule` |
| `M365.ResponseServer` | ResponseServer | 100 | `ResponseServerRule` |
| `M365.Performance.Duration` | Performance | 100 | `LongRunningSessionRule` |

## Source of truth

Ruleset metadata is stored in:

- `src/M365Trace.Rules/Data/RulesetManifest.json`
- `src/M365Trace.Rules/Data/SessionClassification.json`
- `src/M365Trace.Rules/Data/RulesetStrings.json`

Executable rule components are discovered from concrete `ITraceRule` implementations in `src/M365Trace.Rules`.
