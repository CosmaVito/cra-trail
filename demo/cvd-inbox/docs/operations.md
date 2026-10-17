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

**Zip.** Install the ASP.NET Core Runtime 10, unzip, set the configuration and run `./cvd-inbox` as a dedicated user that owns only the data folder, for example from a systemd unit. The app reads `appsettings.json` and the client files in `wwwroot/` from its own folder, and a relative `Inbox__DataPath`, such as the default `data`, starts from that folder too, whatever the current directory. Run `create-operator` as the same user and with the same environment, or it can create a separate database that the service never reads.

## TLS and reverse proxy

CVD Inbox speaks plain HTTP and expects a reverse proxy to terminate TLS. Operator pages need HTTPS: `POST /api/auth/sign-in` answers 400 when the request is not https and the host is not `localhost`, `127.0.0.1` or `[::1]`. The check runs before the password is looked at, so a password sent over plain http is never verified and never counted as a failed attempt.

The check trusts the `Host` header it receives, and the trusted-proxy setting forwards only `X-Forwarded-For` and `X-Forwarded-Proto`, not `Host`. The proxy must pass the original `Host` header (nginx: `proxy_set_header Host $host;`, Apache: `ProxyPreserveHost On`) and send `X-Forwarded-Proto`. A proxy that sends `Host: 127.0.0.1:8080` (the nginx default with `proxy_pass`, or Apache with `ProxyPreserveHost Off`) makes every request look local. Then the app's check does nothing, and only the browser's secure-context check in the sign-in page stops a plain-http sign-in.

The app learns that a request was https from `X-Forwarded-Proto`, and the real client address from `X-Forwarded-For`. It reads both only from the proxies listed in `Inbox__TrustedProxies__0`, `__1`, and so on, which must be IP addresses. One proxy hop is supported: list parallel proxies, not a chain. The proxy must send both headers.

Without a trusted proxy the headers are ignored: the app sees plain http, so it refuses sign-in for any host other than `localhost`, `127.0.0.1` and `[::1]`, and every client behind the proxy shares one rate limit window. Until the proxy is trusted, the client address in events such as 1103 is the proxy's address as the app sees it: list that one. In Docker it is usually the network's gateway, not 127.0.0.1.

Do not set `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. It adds a second forwarded-headers middleware that trusts these headers from any sender and bypasses the list.

## Operators

There is no default account and no setup page. Create and remove operators on the server:

```bash
./cvd-inbox create-operator <name> [--password-stdin]
./cvd-inbox remove-operator <name>
```

Names have 2 to 32 ASCII characters: lower-case letters, digits, `.`, `_` or `-`, starting with a letter or a digit. Input is trimmed and lower-cased. Passwords need 15 to 128 characters, counted as Unicode code points, so an emoji counts as one. Passwords are compared as typed, without Unicode normalization, because the app runs without ICU: an accented letter typed as one code point does not match the same letter typed as a base letter and a combining accent. With `--password-stdin` the command reads the first line of standard input. Without it, the command generates a 24-character password and prints it once. On success it prints the path of the database file it wrote to.

`remove-operator` deletes the operator and prints the same path. Their sessions end at their next request. Notes they wrote keep their name. To change a password, remove the operator and create it again.

Exit codes of both commands: 0 done, 1 refused or failed, 2 wrong usage.

## Upgrade

Stop the old version, back up the data folder, start the new one. The database schema is migrated at start.

## Back up and restore

Stop the service and copy the data folder: `cvd-inbox.db`, `cvd-inbox.db-wal` if present, and `keys/`. Without `keys/`, open sessions end; nothing else is lost. Backups hold report text and contacts: store them encrypted and delete them on the same schedule as the live data.

## Remove everything

Stop the service and delete the data folder, or the volume (`docker volume rm cvd-inbox-data`). This removes every report, contact, note, operator and key. Logs already collected by the container runtime or the journal keep operator names and client addresses: rotate or delete them on the same schedule.

## Secure defaults

Annex I, Part I, point 2(b) of the Cyber Resilience Act asks for a secure default configuration. In CVD Inbox:

- There is no default account and no setup page. The first operator is created from the server's command line.
- The app does not start without an https public origin and a security.txt expiry date in the future.
- Every cookie the app sets carries `Secure`, through a cookie policy that runs early in the pipeline, after the trusted-proxy middleware. Over plain http to a host other than `localhost`, browsers drop them, and the sign-in is refused anyway.
- The session cookie `__Host-cvd-session` is `Secure`, `HttpOnly` and `SameSite=Strict`, with no persistent expiry and a one-hour sliding timeout. The request token cookie `XSRF-TOKEN` is readable by the client script, as it has to be, and is `Secure` and `SameSite=Strict`. The antiforgery cookie `__Host-cvd-antiforgery` is `Secure`, `HttpOnly` and `SameSite=Strict`.
- Every change made by an operator, and the sign-in itself, needs the request token in the `X-XSRF-TOKEN` header.
- Every request checks the session against the database. Signing out ends the operator's sessions on every device, and `remove-operator` ends them at the next request.
- Passwords are stored as PBKDF2-HMAC-SHA512 hashes with 100,000 iterations.
- A wrong password, an unknown name and a locked account get the same answer: 401 with "Wrong name or password." Five wrong passwords lock the account for 15 minutes. Every failure is counted in the database in one statement, so none is lost under parallel attempts; the sign-in rate limit bounds the guesses already in flight.
- Reports, status lookups and sign-ins are rate limited per client address, or per /64 network for IPv6, where one subscriber usually holds a whole /64.
- The Content Security Policy allows scripts from the app only and sets `frame-ancestors 'none'`; responses also send `nosniff`, `X-Frame-Options: DENY` and no referrer, and API responses are not cached.
- Request bodies over 128 KiB are refused.
- The report form has a hidden trap field instead of a third-party CAPTCHA.

## Attack surface

Annex I, Part I, point 2(j):

- One HTTP port (8080 in the image). No other listener, no outbound connections, no email.
- A non-root user. The image has no shell, and the app needs no capabilities and no writable root file system.
- `DOTNET_EnableDiagnostics=0` in the image: no debugger, profiler or EventPipe endpoints.
- Writes only to the data folder, which is created with mode 0700 on Linux, and to `TMPDIR`. In the image that is on the same volume; the zip install uses the system temp directory.
- No runtime dependency on other services: SQLite runs inside the process.

`scripts/container-smoke.sh` checks part of this: a numeric non-root user, only port 8080 exposed, no shell, a run with `--read-only --cap-drop=ALL`, a chunked request body over 128 KiB refused with 413, database, keys and temp files landing in the volume, the operator surviving a container recreate, and `remove-operator` in the image. It does not check `DOTNET_EnableDiagnostics`, outbound connections, the 0700 mode or other listeners.
