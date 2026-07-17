#!/bin/sh
set -eu

# Set default UID/GID (must be integers)
PUID=${PUID:-911}
PGID=${PGID:-911}

case "$PUID" in
    ''|*[!0-9]*) echo "PUID must be a non-negative integer (got: $PUID)" >&2; exit 1 ;;
esac
case "$PGID" in
    ''|*[!0-9]*) echo "PGID must be a non-negative integer (got: $PGID)" >&2; exit 1 ;;
esac

echo "Starting with UID: $PUID, GID: $PGID"

# Update user and group if they differ
if [ "$(id -u appuser)" != "$PUID" ]; then
    usermod -o -u "$PUID" appuser
fi
if [ "$(id -g appuser)" != "$PGID" ]; then
    groupmod -o -g "$PGID" appgroup
fi

# Own app binaries and writable data dirs only. Never recurse into /media
# (host libraries can be multi-TB and should keep host ownership).
chown appuser:appgroup /k7
for dir in /data /data/config /data/metadatas /data/logs /data/transcoding; do
    if [ -d "$dir" ]; then
        chown -R appuser:appgroup "$dir"
    fi
done

# Run the application as appuser
exec gosu appuser dotnet K7.Server.Web.dll "$@"
