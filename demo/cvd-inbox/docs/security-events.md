# Security events

Annex I, Part I, point 2(l) of the Cyber Resilience Act: record and monitor relevant internal activity, with a way to opt out.

CVD Inbox writes these events to standard output as JSON lines, each with a UTC `Timestamp`, under the log category `CvdInbox.Security`. Collect them with your container runtime or journal and forward them to your monitoring.

| ID | Level | Event | Fields |
|---|---|---|---|
| 1001 | Information | Operator signed in | operator, client address |
| 1002 | Warning | Failed sign-in | name if it belongs to an operator, otherwise `(unknown name)`; client address |
| 1003 | Warning | Sign-in refused, account locked; also logged for the failure that sets the lock | operator, client address |
| 1004 | Information | Operator signed out; their sessions on every device have ended | operator |
| 1005 | Information | Operator created from the command line | operator |
| 1006 | Warning | Operator removed from the command line; their sessions end at the next request | operator |
| 1101 | Information | Report received | report id |
| 1102 | Warning | Report form trap filled, report discarded | client address |
| 1103 | Warning | Rate limit reached; logged at most once a minute per client and policy | policy (`reports`, `lookups`, `sign-ins`), client address |
| 1104 | Warning | Request without a valid request token refused | path, client address |
| 1201 | Information | Report status changed | report id, from, to, operator |
| 1202 | Information | Internal note added | report id, operator |
| 1203 | Warning | Report deleted for good | report id, operator |
| 1204 | Warning | Write-ahead log not truncated because the database was in use; the next sweep tries again | none |
| 1301 | Information | Contacts erased after the retention period | number of reports |
| 1302 | Error | Erasing expired contacts failed; the next sweep tries again | the exception |
| 1303 | Error | Truncating the write-ahead log failed; the next sweep tries again | the exception |
| 1401 | Warning | security.txt expires within 30 days; logged at start and then every day | expiry date |
| 1402 | Error | security.txt has expired while the server was running; logged every day until a restart with a later date | expiry date |

Events 1005 and 1006 are written by the `create-operator` and `remove-operator` processes, not by the server. When you run a command with `docker exec` or from a shell, the event goes to the output of that command, not to the container log or the journal.

A sign-in over plain http to a public host is refused with a 400 before the password is checked, and produces no event. Event 1002 never carries what was typed in the name field unless it is the name of an existing operator, because people type passwords there.

## What the events never contain

Report text, contacts, receipt codes, notes and passwords. The app does not record the address of a normal submission. Events 1102 and 1103 do carry it, and the access log of your reverse proxy may too.

## Opting out

Set the level of the category to `None`, for example with `-e` in Docker or `Environment=` in a systemd unit:

```
Logging__LogLevel__CvdInbox.Security=None
```

The same works in `appsettings.json` under `Logging:LogLevel`. This silences every event in the table. Without these events there is no record of sign-ins, changes and deletions.
