# Configuration

ASP.NET Core configuration. In Docker, prefer environment variables with `__` for nesting (`Section__Key`). JSON keys use `:`. Primary defaults: [`src/Server/Web/appsettings.json`](../../src/Server/Web/appsettings.json).

Install first: [install.md](install.md). Day-to-day features: [operating.md](operating.md).

## Server identity and HTTP

| Key | Env | Default | Description |
|---|---|---|---|
| `BaseUrl` | `BaseUrl` | `https://localhost:5001` | Public URL of this instance. Must match what browsers and federation peers use. Required for OIDC redirects and peering. |
| `Server:Name` | `Server__Name` | *(empty)* | Display name when initiating federation. Falls back to host of `BaseUrl`, then machine name. |
| `Cors:Origins` | `Cors__Origins` | `[]` | Allowed CORS origins. Non-empty: only those. Empty + Development: loopback. Empty + Production: deny all. Needed if WASM is hosted on another origin. |
| `AllowedHosts` | `AllowedHosts` | `*` | Standard ASP.NET host filtering. `*` disables host filtering; set explicit hostnames in production behind a known public URL. |

## Database

The sample [`docker-compose.yaml`](../../docker-compose.yaml) uses **Postgres** (recommended). Sqlite works for small trials but is less performant and not recommended for production. For Sqlite, set `Database__Provider=Sqlite` and `Database__Name` to a path under `/data` **without** a `.db` suffix (the server appends `.db`). See also [`docker-compose.federation-test.yaml`](../../docker-compose.federation-test.yaml) for a local multi-peer Sqlite example.

### Docker secrets (`__File`)

Any configuration value can be loaded from a file by setting a sibling `*:File` key (env form `__File`). The file contents become the parent value (trailing newlines stripped).

| Env | Effect |
|---|---|
| `Database__Password__File=/run/secrets/postgres_password` | Sets `Database:Password` from that file |
| `Authentication__Oidc__ClientSecret__File=/run/secrets/oidc_client_secret` | Sets `Authentication:Oidc:ClientSecret` |

Example Compose secrets (adjust the sample stack):

```yaml
secrets:
  postgres_password:
    file: ./secrets/postgres_password.txt

services:
  postgres:
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres_password
    secrets:
      - postgres_password
  k7-server:
    environment:
      Database__Password__File: /run/secrets/postgres_password
    secrets:
      - postgres_password
```

When using `POSTGRES_PASSWORD_FILE`, do not also set `POSTGRES_PASSWORD` on the Postgres service. The official Postgres image supports `_FILE` for its own variables; K7 uses `Database__Password__File` for the app.

| Key | Env | Default | Description |
|---|---|---|---|
| `Database:Provider` | `Database__Provider` | `Postgres` | `Postgres` or `Sqlite` (case-insensitive). |
| `Database:Server` | `Database__Server` | `localhost` | Postgres host. |
| `Database:Port` | `Database__Port` | `5432` | Postgres port. |
| `Database:UserID` | `Database__UserID` | `postgres` | Postgres user. |
| `Database:Password` | `Database__Password` | *(empty)* | Postgres password. |
| `Database:Name` | `Database__Name` | *(empty)* | Postgres DB name, or Sqlite path **without** `.db` (server appends `.db`). Example: `/data/k7` -> `/data/k7.db`. |
| `Database:MaxPoolSize` | `Database__MaxPoolSize` | `50` | Postgres pool size (code default). |

## Authentication

### Local accounts

| Key | Env | Default (appsettings) | Description |
|---|---|---|---|
| `Authentication:Local:SignInEnabled` | `Authentication__Local__SignInEnabled` | `true` | Allow email/password (and 2FA) sign-in. Set `false` for OIDC-only. |
| `Authentication:Local:RegistrationEnabled` | `Authentication__Local__RegistrationEnabled` | `false` | Allow self-service `/sign-up`. |

These flags are **config-only** (shown read-only under Admin -> Authentication). Changing them requires restart.

### OIDC / SSO

| Key | Env | Default | Description |
|---|---|---|---|
| `Authentication:Oidc:Enabled` | `Authentication__Oidc__Enabled` | `false` | Register the OIDC scheme. |
| `Authentication:Oidc:Authority` | `Authentication__Oidc__Authority` | *(empty)* | IdP issuer URL. |
| `Authentication:Oidc:ClientId` | `Authentication__Oidc__ClientId` | *(empty)* | Client id. |
| `Authentication:Oidc:ClientSecret` | `Authentication__Oidc__ClientSecret` | *(empty)* | Client secret. |
| `Authentication:Oidc:Scopes` | `Authentication__Oidc__Scopes` | `openid,profile` | Comma-separated scopes. |
| `Authentication:Oidc:DisplayName` | `Authentication__Oidc__DisplayName` | *(empty)* / class default `Oidc` | Sign-in button label. |
| `Authentication:Oidc:AutomaticAccountCreation` | `Authentication__Oidc__AutomaticAccountCreation` | `true` | Create User-role account on first login. If `false`, unknown users are rejected. |

**Lockout risk:** if both local sign-in and OIDC are disabled, nobody can sign in except via existing sessions / API keys until you fix config.

#### Enable OIDC

```yaml
Authentication__Oidc__Enabled: "true"
Authentication__Oidc__Authority: https://idp.example.com/application/o/k7/
Authentication__Oidc__ClientId: k7
Authentication__Oidc__ClientSecret: your-secret
Authentication__Oidc__Scopes: openid,profile
Authentication__Oidc__DisplayName: Authentik
Authentication__Oidc__AutomaticAccountCreation: "true"
```

Set `BaseUrl` to the public URL of K7, then restart.

#### Redirect URIs at the IdP

| Purpose | URI |
|---|---|
| Login callback | `{BaseUrl}/api/authentication/callback/login/oidc` |
| Post-logout | `{BaseUrl}/api/authentication/callback/logout/oidc` |

Challenge entry: `GET /api/authentication/login?returnUrl=...`.

#### Local + OIDC modes

| Goal | Config |
|---|---|
| Local + OIDC | Both enabled - sign-in page shows both |
| OIDC only | `Local:SignInEnabled=false`, OIDC on |
| Local only | `Oidc:Enabled=false` |

When `AutomaticAccountCreation` is `false`, unknown users hit `/sign-in?error=auto_provisioning_disabled`. First-run can still complete via OIDC on `/setup` (setup token may apply) - see [install.md](install.md#first-run-setup).

## Security

| Key | Env | Default | Description |
|---|---|---|---|
| `Security:ForceHttps` | `Security__ForceHttps` | `true` | Prefer secure cookies and OpenIddict transport security. Behind TLS-terminating proxy, keep `true` and configure forwarded headers. |
| `Security:KnownProxies` | `Security__KnownProxies` | `[]` | Proxy IPs trusted for `X-Forwarded-*`. Empty outside Development disables forwarded-header processing. |
| `Security:Federation:AllowInsecurePeerHttp` | `Security__Federation__AllowInsecurePeerHttp` | `false` | Allow HTTP peer URLs (also allowed in Development). |
| `Security:Federation:BlockPrivatePeerUrls` | `Security__Federation__BlockPrivatePeerUrls` | `false` | Reject private/link-local peer URLs. |

### Hardening checklist

- Run the published image (non-root via `gosu`); set `PUID` / `PGID`; keep `no-new-privileges`.
- Mount media **read-only** unless you intentionally write tags back to disk.
- Do not publish Postgres to the internet. The sample compose maps host `5432` for local convenience - remove it (or bind `127.0.0.1` only) on public hosts.
- Leave registration disabled unless you want open sign-up; enable Guest only when needed.
- Encourage 2FA for password accounts; prefer OIDC with MFA when available.
- API keys: header `X-Api-Key`; Admin CRUD at `/api/admin/api-keys`; least privilege; rotate unused keys.
- Enable federation only when needed; treat peering as trust.
- Prefer VPN (Tailscale, WireGuard) over wide public exposure for friends.

There is no comprehensive in-app rate limiting documented; put abusive-traffic controls at the reverse proxy if you expose K7 broadly.

## Paths

Resolved relative to the process working directory (`/k7` in the container) unless absolute.

| Key | Env | Default | Description |
|---|---|---|---|
| `Paths:Config` | `Paths__Config` | `config` | DataProtection keys and OpenIddict certificates. **Must be on a persistent volume.** |
| `Paths:Metadatas` | `Paths__Metadatas` | `metadatas` | Artwork and processed metadata files. |
| `Paths:Logs` | `Paths__Logs` | `logs` | Serilog file sink (`log-.log`). |
| `Paths:Transcoding` | `Paths__Transcoding` | `transcoding` | HLS / transcode working files. |
| `Paths:FFMpegBinaryFolder` | `Paths__FFMpegBinaryFolder` | *(empty)* | Directory containing ffmpeg/ffprobe. Empty = `PATH`. |

Created at startup under Config: `dataprotection-keys`, `openiddict-keys`.

## Logging (Serilog)

| Key | Env | Notes |
|---|---|---|
| `Serilog:*` | `Serilog__*` | Standard Serilog config. Always writes Console + File under `Paths:Logs`. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | same | If set, OpenTelemetry sink is added. |

Example: `Serilog__MinimumLevel__Default=Debug`.

## Bootstrap / setup (environment only)

| Env | Description |
|---|---|
| `K7_ADMIN_EMAIL` + `K7_ADMIN_PASSWORD` | Create first admin and complete setup on first boot. |
| `K7_SETUP_TOKEN` | Required setup token for the wizard (or auto-generated and logged). |
| `PUID` / `PGID` | Container UID/GID (default `911`). |
| `SmokeTest:SkipFfmpegVerification` | Skip ffmpeg check at startup (tests). |

## Settings stored in the database (not env)

Configured in the admin UI (or APIs), not via `appsettings.json`:

- Feature flags (including federation)
- Transcode settings, background task limits
- Server-wide default playback / home / track-selection preferences
- Notification (webhook) rules
- AudioMuse connection
- Library definitions and access rules

See [operating.md](operating.md).

## Reverse proxy

Run K7 behind a reverse proxy for TLS. The app listens on **8080** inside the container.

| Setting | Guidance |
|---|---|
| `BaseUrl` | Public URL users and peers use |
| `Security:ForceHttps` | Keep `true` for public HTTPS; pair with trusted forwarded headers |
| `Security:KnownProxies` | Proxy IP(s) as seen by the container |
| `Cors:Origins` | Only if WASM is on another origin |

When `KnownProxies` is non-empty, K7 trusts `X-Forwarded-Proto` from those addresses. Misconfiguration causes cookie/HTTPS mismatches or redirect loops.

SignalR (remote control, Sync Play) needs WebSocket upgrade, reasonable timeouts, and the same host/proto headers.

### Sample: Caddy

```caddy
k7.example.com {
    reverse_proxy k7-server:8080
}
```

### Sample: Traefik

Docker Compose labels on the `k7-server` service (Traefik v2/v3 on the same Docker network). Adjust the entrypoint / cert resolver names to match your Traefik stack:

```yaml
services:
  k7-server:
    # ... image, volumes, environment as in docker-compose.yaml
    labels:
      - traefik.enable=true
      - traefik.docker.network=proxy
      - traefik.http.routers.k7.rule=Host(`k7.example.com`)
      - traefik.http.routers.k7.entrypoints=websecure
      - traefik.http.routers.k7.tls=true
      - traefik.http.routers.k7.tls.certresolver=letsencrypt
      - traefik.http.services.k7.loadbalancer.server.port=8080
```

Set on the K7 container:

```yaml
environment:
  BaseUrl: https://k7.example.com
  Security__ForceHttps: "true"
  # Traefik container IP or Docker gateway as seen by k7-server
  Security__KnownProxies__0: 172.18.0.1
```

If Traefik and K7 share a user-defined network, put Traefik's IP (or the network gateway) in `KnownProxies`. Without that, forwarded `X-Forwarded-Proto` may be ignored outside Development.

### Sample: nginx

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    ''      close;
}

server {
    listen 443 ssl http2;
    server_name k7.example.com;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade           $http_upgrade;
        proxy_set_header Connection        $connection_upgrade;
        proxy_read_timeout 86400;
    }
}
```

Health probe: `GET /health` (allowed during first-run as well).
