# Operations

CVD Inbox is an example application, not fit for production use. These notes describe what it does today.

## Install

**Container.** The image is based on the ASP.NET Core 10 chiseled image: no shell, no package manager, user `app` (UID 1654). It listens on port 8080 and writes only to `/home/app`, where the data folder `data/` lives. `TMPDIR` points to `/home/app` as well, so temporary files go to the volume. Mount a volume on `/home/app` and run the container read-only:

```bash
docker run -d --name cvd-inbox --read-only --cap-drop=ALL --security-opt=no-new-privileges \
  -v cvd-inbox-data:/home/app -p 127.0.0.1:8080:8080 \
  -e Disclosure__PublicOrigin=https://inbox.example.org \
  -e Disclosure__Expires=2027-06-30T00:00:00Z \
  cvd-inbox:0.1.0
```

To use a host folder instead of a named volume, give it to UID 1654 first: `chown 1654:1654 /srv/cvd-inbox`.

`scripts/container-smoke.sh` starts the image with exactly these options and checks it.

**Zip.** Install the ASP.NET Core Runtime 10, unzip, set the configuration and run `./cvd-inbox` as a dedicated user that owns only the data folder, for example from a systemd unit.

## TLS and reverse proxy

CVD Inbox speaks plain HTTP and expects a reverse proxy to terminate TLS. Operator pages need HTTPS: `POST /api/auth/sign-in` answers 400 when the request is not https and the host is not `localhost`, `127.0.0.1` or `[::1]`. The check runs before the password is looked at, so a password sent over plain http is never verified and never counted as a failed attempt.

The app learns that a request was https from `X-Forwarded-Proto`, and the real client address from `X-Forwarded-For`. It reads both only from the proxies listed in `Inbox__TrustedProxies__0`, `__1`, and so on, which must be IP addresses. One proxy hop is supported: list parallel proxies, not a chain. The proxy must send both headers.

Without a trusted proxy the headers are ignored: operators cannot sign in over https, and every client behind the proxy shares one rate limit window. Use the client address in the security events to check that the app sees the right address; in Docker it is the address of the proxy as the container sees it, which is not always the one you configured on the host.

Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. It adds a second forwarded-headers middleware that trusts these headers from any sender and bypasses the list.

## First operator

There is no default account and no setup page. Create operators on the server:

```bash
./cvd-inbox create-operator <name> [--password-stdin]
```

Names have 2 to 32 lower-case letters, digits, `.`, `_` or `-`. Passwords need 15 to 128 characters. With `--password-stdin` the command reads the first line of standard input. Without it, the command generates a 24-character password and prints it once. Exit codes: 0 created, 1 refused, 2 wrong usage.

## Upgrade

Stop the old version, back up the data folder, start the new one. The database schema is migrated at start.

## Back up and restore

Stop the service and copy the data folder: `cvd-inbox.db`, `cvd-inbox.db-wal` if present, and `keys/`. Without `keys/`, open sessions end; nothing else is lost. Backups hold report text and contacts: store them encrypted and delete them on the same schedule as the live data.

## Remove everything

Stop the service and delete the data folder, or the volume (`docker volume rm cvd-inbox-data`). This removes every report, contact, note, operator and key.

## Secure defaults

Annex I, Part I, point 2(b) of the Cyber Resilience Act asks for a secure default configuration. In CVD Inbox:

- There is no default account and no setup page. The first operator is created from the server's command line.
- The app does not start without an https public origin and a security.txt expiry date in the future.
- Every cookie the app sets carries `Secure`, through a cookie policy that runs before everything else. Over plain http to a host other than `localhost`, browsers drop them, and the sign-in is refused anyway.
- The session cookie `__Host-cvd-session` is `Secure`, `HttpOnly` and `SameSite=Strict`, with no persistent expiry and a one-hour sliding timeout. The request token cookie `XSRF-TOKEN` is readable by the client script, as it has to be, and is `Secure` and `SameSite=Strict`.
- Every change made by an operator, and the sign-in itself, needs the request token in the `X-XSRF-TOKEN` header.
- Passwords are stored as PBKDF2-HMAC-SHA512 hashes with 100,000 iterations.
- A wrong password, an unknown name and a locked account get the same answer: 401 with "Wrong name or password." Five wrong passwords lock the account for 15 minutes. Failures are counted in the database in one statement, so parallel guesses cannot slip past the count.
- Reports, status lookups and sign-ins are rate limited per client address.
- The Content Security Policy allows scripts from the app only and sets `frame-ancestors 'none'`; responses also send `nosniff`, `X-Frame-Options: DENY` and no referrer, and API responses are not cached.
- Request bodies over 128 KiB are refused.
- The report form has a hidden trap field instead of a third-party CAPTCHA.

## Attack surface

Annex I, Part I, point 2(j):

- One HTTP port (8080 in the image). No other listener, no outbound connections, no email.
- A non-root user. The image has no shell, and the app needs no capabilities and no writable root file system.
- `DOTNET_EnableDiagnostics=0` in the image: no debugger, profiler or EventPipe endpoints.
- Writes only to the data folder, which is created with mode 0700 on Linux, and to `TMPDIR`, which is on the same volume.
- No runtime dependency on other services: SQLite runs inside the process.

`scripts/container-smoke.sh` checks the image for these points.
