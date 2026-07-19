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

# When /dev/dri is mounted for VAAPI/QSV, grant appuser the device GIDs
# (host render/video groups) so encode probes can open renderD*.
if [ -d /dev/dri ]; then
    for device in /dev/dri/renderD* /dev/dri/card*; do
        [ -e "$device" ] || continue
        gid=$(stat -c '%g' "$device" 2>/dev/null || true)
        [ -n "${gid:-}" ] || continue
        [ "$gid" != "0" ] || continue
        if ! getent group "$gid" >/dev/null 2>&1; then
            groupadd -g "$gid" "host-gid-$gid" 2>/dev/null || true
        fi
        gname=$(getent group "$gid" | cut -d: -f1)
        if [ -n "$gname" ]; then
            usermod -aG "$gname" appuser 2>/dev/null || true
            echo "Granted appuser access to $device via group $gname ($gid)"
        fi
    done
fi

# Run the application as appuser
exec gosu appuser dotnet K7.Server.Web.dll "$@"
