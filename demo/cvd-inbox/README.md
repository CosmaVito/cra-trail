# CVD Inbox

CVD Inbox receives vulnerability reports for the software you publish. Researchers send a report through a form and follow it with a receipt code; your team triages it in an operator area. It runs as one self-hosted service with its own SQLite database.

It is the example product of cra-trail: the cra-trail pipeline builds it, scans it and ships it with an evidence pack. The app is small on purpose. It is an example, not fit for production use.

## What it does

- Report form with product, version, description and an optional contact. The researcher gets a receipt code to check the status later.
- `/.well-known/security.txt` (RFC 9116) and a `/security-policy` page, both built from the configuration.
- Operator area: sign-in, inbox, status changes (new, triage, accepted, rejected, fixed, disclosed), internal notes, permanent deletion.
- The researcher's contact is erased a set number of days after the report is closed.
- Security events go to the log as JSON lines.

Limits: one tenant, no email, no sign-up page, no password reset. Operators are created and removed on the server with `create-operator` and `remove-operator`. To change a password, remove the operator and create it again. Every request checks the session against the database, so signing out or removing an operator ends their sessions on every device.

## Run the container

```bash
docker run -d --name cvd-inbox --read-only --cap-drop=ALL --security-opt=no-new-privileges \
  -v cvd-inbox-data:/home/app -p 127.0.0.1:8080:8080 \
  -e Disclosure__PublicOrigin=https://inbox.example.org \
  -e Disclosure__Expires=2027-06-30T00:00:00Z \
  cvd-inbox:0.1.0
```

Operator pages need HTTPS: the sign-in is refused with a 400 over plain http, unless the host is `localhost`, `127.0.0.1` or `[::1]`. Put a reverse proxy with TLS in front of the app and list it in `Inbox__TrustedProxies__0`, so the app accepts its `X-Forwarded-Proto` header. The proxy must also pass the original `Host` header (nginx: `proxy_set_header Host $host;`, Apache: `ProxyPreserveHost On`). If it sends `Host: 127.0.0.1:8080` instead, the app takes every request for a local one and its own check does nothing; only the browser check in the sign-in page then stops a plain-http sign-in. Every cookie the app sets is `Secure`.

A host folder mounted on `/home/app` must be owned by UID 1654, the user of the image.

Create the first operator. The password is read from standard input, or generated and shown once if you leave out `--password-stdin`:

```bash
printf '%s\n' "$OPERATOR_PASSWORD" | docker exec -i cvd-inbox ./cvd-inbox create-operator alice --password-stdin
```

`docker exec cvd-inbox ./cvd-inbox remove-operator alice` removes an operator and ends their sessions.

## Run from the zip

The zip needs the ASP.NET Core Runtime 10 on a Linux x64 host. The app reads `appsettings.json` and the client files in `wwwroot/` from its own folder, and a relative `Inbox__DataPath`, such as the default `data`, starts from that folder too, whatever the current directory.

```bash
unzip cvd-inbox-0.1.0-linux-x64.zip -d cvd-inbox && cd cvd-inbox
export Disclosure__PublicOrigin=https://inbox.example.org Disclosure__Expires=2027-06-30T00:00:00Z
export Inbox__DataPath=/var/lib/cvd-inbox ASPNETCORE_URLS=http://127.0.0.1:8080
./cvd-inbox create-operator alice
./cvd-inbox
```

Run `create-operator` as the user of the service and with the same environment, or it can create a separate database that the service never reads. The command prints the path of the database file it wrote to.

## Configuration

Settings come from environment variables (`Section__Key`) or `appsettings.json`.

| Setting | Default | Meaning |
|---|---|---|
| `Disclosure__PublicOrigin` | none, required | https origin where people reach the inbox. security.txt is built from it. |
| `Disclosure__Expires` | none, required | Expiry of security.txt, a date in the future. RFC 9116 asks for less than a year ahead. The app does not start once it has passed. |
| `Disclosure__Contact__0`, `__1`, ... | none | More contacts after the report form: `mailto:`, `tel:` or `https:` URIs. |
| `Disclosure__PreferredLanguages` | `en` | Languages you read reports in. |
| `Disclosure__AcknowledgeWithinDays` | `5` | Shown on the policy page. |
| `Disclosure__DisclosureAfterDays` | `90` | Shown on the policy page. |
| `Inbox__DataPath` | `data`; `/home/app/data` in the image | Database and key folder. |
| `Inbox__ContactRetentionDays` | `30` | Days a contact is kept after the report closes. |
| `Inbox__RetentionSweepInterval` | `01:00:00` | How often expired contacts are erased and the write-ahead log is truncated, from one minute to one day. |
| `Inbox__TrustedProxies__0`, ... | none | IP addresses of the reverse proxies allowed to set `X-Forwarded-For` and `X-Forwarded-Proto`. One proxy hop is supported: list parallel proxies, not a chain. The proxy must send both headers. |
| `RateLimits__Reports__PermitLimit`, `__Window` | `5`, `01:00:00` | Reports per client address, or per /64 network for IPv6. |
| `RateLimits__Lookups__PermitLimit`, `__Window` | `30`, `00:10:00` | Status lookups per client address, or per /64 network for IPv6. |
| `RateLimits__SignIns__PermitLimit`, `__Window` | `10`, `00:15:00` | Sign-in attempts per client address, or per /64 network for IPv6. |

## Develop

```bash
dotnet run --project src/CvdInbox --launch-profile http   # API on http://localhost:5080
cd client && npx ng serve                                 # app on http://localhost:4200, API through a proxy
```

The development database goes to `src/CvdInbox/bin/Debug/net10.0/data`: a relative data path starts from the app's folder.

Tests:

```bash
dotnet test --project tests/CvdInbox.Tests
(cd client && npx ng test --no-watch)
(cd client && npx ng build && npm run e2e)
bash scripts/publish.sh && bash scripts/container-smoke.sh
```

## Documents

- [Operations](docs/operations.md): install, upgrade, back up, secure defaults, attack surface.
- [Data handling](docs/data-handling.md): what is stored, for how long, and how it is erased.
- [Security events](docs/security-events.md): what the log records.
- [Security policy](SECURITY.md) for vulnerabilities in CVD Inbox itself.
