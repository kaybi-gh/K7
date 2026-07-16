# Backup and troubleshooting

## Backup and restore

K7 has no built-in backup feature. Back up the database and persistent paths yourself.

### What to back up

| Item | Location | Required? |
|---|---|---|
| Database | Postgres volume (`k7-postgres-data`) or Sqlite file (`{Database:Name}.db`) | **Yes** |
| `Paths:Config` | Default `/data/config` in the sample compose | **Yes** |
| Media libraries | Your host paths under `/media/...` | **Yes** (your files) |
| `Paths:Metadatas` | Default `/data/metadatas` | **Recommended** - artwork and processed images; can be rebuilt by refreshing metadata, but that is slow |
| `Paths:Logs` | `/data/logs` | Optional |
| `Paths:Transcoding` | `/data/transcoding` | **No** - cache only |

### Why Config matters

Under `Paths:Config`:

- `dataprotection-keys` - ASP.NET Data Protection key ring (cookies, antiforgery)
- `openiddict-keys` - OpenIddict encryption and signing certificates

Losing these while keeping the database typically **invalidates sessions and tokens** and can break federation client credentials until peers are re-established. Always restore Config together with the database from the same backup set.

### Postgres backup example

```bash
docker compose exec -T postgres \
  pg_dump -U postgres -d K7 -Fc > k7-$(date +%Y%m%d).dump
```

Restore (destructive - replace carefully):

```bash
docker compose exec -T postgres \
  pg_restore -U postgres -d K7 --clean --if-exists < k7-YYYYMMDD.dump
```

Also archive the `k7-data` volume (or bind-mounted `/data` directory).

### Sqlite backup example

Stop the server (or ensure no writers), then copy the database file and the `/data` tree (`config`, `metadatas`, ...).

### Restore checklist

1. Stop `k7-server`.
2. Restore the database.
3. Restore `Paths:Config` (and Metadatas if you backed it up) to the same absolute paths.
4. Confirm `BaseUrl`, database, and path env vars match the old instance.
5. Start the server; migrations apply automatically.
6. Sign in and verify libraries still point at mounted media paths.

Keep `.env` / OIDC secrets out of git and back them up separately.

## Troubleshooting

### Where are the logs?

| Source | Location |
|---|---|
| Container stdout | `docker compose logs -f k7-server` |
| File sink | `Paths:Logs` (sample: `/data/logs/log-.log`) |

Raise levels via Serilog env vars - see [configuration.md](configuration.md#logging-serilog).

### Cannot connect to the database

- Check `POSTGRES_PASSWORD` matches `Database__Password`
- On Compose, `Database__Server` must be the service name (`postgres`), not `localhost`
- Wait for the Postgres healthcheck (`depends_on: service_healthy`)

### Media files not visible / permission denied

- Confirm the bind mount matches the library `RootPath`
- Align `PUID`/`PGID` with host ownership of the media tree
- Prefer `:ro` mounts

### Lost settings / users after recreate

- `Paths__*` were relative or not mounted - use a `/data` volume and absolute paths ([install.md](install.md))
- Restored DB without restoring `Paths:Config` - sessions and OpenIddict keys no longer match

### ffmpeg missing (non-Docker)

Install ffmpeg or set `Paths__FFMpegBinaryFolder`. The official image already includes ffmpeg.

### HTTPS redirect loops

- Behind a proxy: set `Security__KnownProxies` and ensure `X-Forwarded-Proto` is `https`
- Or temporarily set `Security__ForceHttps=false` on a trusted LAN while debugging

### OIDC login fails

- `BaseUrl` must match the public URL registered at the IdP
- Redirect URI: `{BaseUrl}/api/authentication/callback/login/oidc`
- Clock skew, wrong client secret, or `AutomaticAccountCreation=false` for a new user

### Federation peer unreachable

- Both `BaseUrl` values must be reachable from the other host
- Feature flag enabled on both sides
- TLS / private URL guards - [operating.md](operating.md#federation)

### Setup page every time

- Setup never completed, or database volume was wiped
- Complete `/setup` or set `K7_ADMIN_EMAIL` / `K7_ADMIN_PASSWORD`

### Playback buffers or fails

- Check Admin active streams / transcoder errors in logs
- Disk full on `Paths:Transcoding`
- Client quality too high - lower quality in the player
- See also [Using K7 - When something goes wrong](../user/guide.md#when-something-goes-wrong)

### Health endpoint

`GET /health` should return success when the process is up (also allowed during first-run).
