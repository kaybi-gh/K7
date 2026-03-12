#!/bin/sh

# D�finir des valeurs par d�faut
PUID=${PUID:-911}
PGID=${PGID:-911}
echo "Starting with UID: $PUID, GID: $PGID"

# Modifier l�utilisateur et le groupe s�ils existent d�j�
if [ "$(id -u appuser)" != "$PUID" ]; then
    usermod -o -u "$PUID" appuser
fi
if [ "$(id -g appuser)" != "$PGID" ]; then
    groupmod -o -g "$PGID" appgroup
fi

# Changer les permissions sur le dossier de travail
chown -R appuser:appgroup /k7

# Ex�cuter l�application sous appuser
exec gosu appuser dotnet K7.Server.Web.dll "$@"