#!/bin/sh

# Set default UID/GID
PUID=${PUID:-911}
PGID=${PGID:-911}
echo "Starting with UID: $PUID, GID: $PGID"

# Update user and group if they differ
if [ "$(id -u appuser)" != "$PUID" ]; then
    usermod -o -u "$PUID" appuser
fi
if [ "$(id -g appuser)" != "$PGID" ]; then
    groupmod -o -g "$PGID" appgroup
fi

# Fix ownership on working directory and data volumes
chown -R appuser:appgroup /k7
for dir in /data /media; do
    if [ -d "$dir" ] && [ -w "$dir" ]; then
        chown -R appuser:appgroup "$dir" 2>/dev/null || true
    fi
done

# Run the application as appuser
exec gosu appuser dotnet K7.Server.Web.dll "$@"