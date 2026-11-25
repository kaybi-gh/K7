#!/usr/bin/env bash
# Builds a single-file Flatpak bundle for the K7 Linux client from a self-contained publish dir.
#
# Usage:
#   tools/linux/build-flatpak.sh <publish-dir> <version> [output.flatpak]
#
# Prerequisites (Ubuntu 24.04+):
#   sudo apt-get install -y flatpak flatpak-builder
#   (the script adds the Flathub remote and installs org.gnome.Platform/Sdk//49 for the user)
# The manifest sets no-debuginfo / strip: false so flatpak-builder does not need eu-strip
# (Release publish tree, not sources built inside the Sdk). Install elfutils if you strip.
#
# The publish dir must contain K7.Clients.MAUI (dotnet publish -r linux-x64 --self-contained)
# and, for Direct Play, libvlc/linux-x64 (tools/linux/bundle-libvlc.sh).
#
# After install: flatpak run com.k7.maui
# k7:// is registered by the exported desktop file (MimeType=x-scheme-handler/k7).
#
# WSL: keep the publish dir and the output under /tmp (native FS); flatpak-builder wants its
# state, repo and build dir on one filesystem, which this script stages under $TMPDIR.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
FLATPAK_DIR="$REPO_ROOT/tools/linux/flatpak"
RUNTIME_VERSION="${K7_FLATPAK_RUNTIME_VERSION:-49}"
APP_ID="com.k7.maui"

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <publish-dir> <version> [output.flatpak]" >&2
  exit 2
fi

PUBLISH_DIR="$(cd "$1" && pwd)"
VERSION="$2"
OUT="${3:-$PWD/K7-${VERSION}-linux-x64.flatpak}"
case "$OUT" in
  /*) ;;
  *) OUT="$PWD/$OUT" ;;
esac

for tool in flatpak flatpak-builder; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "build-flatpak: missing '$tool' (apt-get install flatpak flatpak-builder)" >&2
    exit 1
  fi
done

if [ ! -f "$PUBLISH_DIR/K7.Clients.MAUI" ]; then
  echo "build-flatpak: missing $PUBLISH_DIR/K7.Clients.MAUI (run dotnet publish first)" >&2
  exit 1
fi
if [ ! -f "$PUBLISH_DIR/wwwroot/index.html" ]; then
  echo "build-flatpak: missing $PUBLISH_DIR/wwwroot/index.html (static web assets not published)" >&2
  exit 1
fi

# LibMan assets (wwwroot/dist is gitignored): without them Video.js is missing and HLS / music
# never start. Re-run "dotnet tool run libman -- restore" in src/Clients/Shared/UI and publish again.
if [ ! -f "$PUBLISH_DIR/wwwroot/_content/K7.Clients.Shared.UI/dist/video.js/video.min.js" ]; then
  echo "build-flatpak: $PUBLISH_DIR/wwwroot/_content/K7.Clients.Shared.UI/dist/video.js/video.min.js is missing (LibMan restore not run before publish)" >&2
  exit 1
fi

# Release fingerprints the scoped bundle. index.html requests the stable styles.css name.
# Without the alias the nav and cards lose their layout while app.css (theme) still loads.
SCOPED_CSS="$PUBLISH_DIR/wwwroot/_content/K7.Clients.Shared.UI/K7.Clients.Shared.UI.styles.css"
if [ ! -f "$SCOPED_CSS" ]; then
  echo "build-flatpak: missing $SCOPED_CSS (scoped layout CSS was not aliased from the fingerprinted bundle)" >&2
  exit 1
fi
SCOPED_CSS_BYTES="$(wc -c < "$SCOPED_CSS" | tr -d ' ')"
if [ "$SCOPED_CSS_BYTES" -lt 50000 ]; then
  echo "build-flatpak: $SCOPED_CSS is only ${SCOPED_CSS_BYTES} bytes" >&2
  exit 1
fi

VLC_DIR="$PUBLISH_DIR/libvlc/linux-x64"
if [ ! -f "$VLC_DIR/libvlc.so.12" ]; then
  if [ "${K7_FLATPAK_ALLOW_NO_VLC:-0}" = "1" ]; then
    echo "build-flatpak: warning: no libvlc bundle at $VLC_DIR (Direct Play disabled in this build)" >&2
  else
    echo "build-flatpak: no libvlc bundle at $VLC_DIR. Run tools/linux/bundle-libvlc.sh (or point K7LinuxLibVlcDir at an existing bundle) and publish again; K7_FLATPAK_ALLOW_NO_VLC=1 builds without Direct Play" >&2
    exit 1
  fi
else
  # Sonames the GNOME runtime does not provide; bundle-libvlc.sh ships them, older bundles
  # did not.
  for soname in libbz2.so.1.0 libbsd.so.0 libmd.so.0; do
    if [ ! -f "$VLC_DIR/lib/$soname" ]; then
      echo "build-flatpak: $VLC_DIR/lib/$soname is missing; rebuild the bundle with the current tools/linux/bundle-libvlc.sh" >&2
      exit 1
    fi
  done
  for plugin in video_output/libwdummy_plugin.so access/libimem_plugin.so; do
    if [ ! -f "$VLC_DIR/plugins/$plugin" ]; then
      echo "build-flatpak: $VLC_DIR/plugins/$plugin is missing; rebuild the bundle with the current tools/linux/bundle-libvlc.sh" >&2
      exit 1
    fi
  done
fi

for required in com.k7.maui.yml k7-wrapper.sh proxy com.k7.maui.desktop com.k7.maui.metainfo.xml; do
  if [ ! -f "$FLATPAK_DIR/$required" ]; then
    echo "build-flatpak: missing $FLATPAK_DIR/$required" >&2
    exit 1
  fi
done

ICON="$REPO_ROOT/branding/icon.png"
if [ ! -f "$ICON" ]; then
  echo "build-flatpak: missing $ICON" >&2
  exit 1
fi

WORK="$(mktemp -d -p "${TMPDIR:-/tmp}" k7-flatpak.XXXXXX)"
trap 'rm -rf "$WORK"' EXIT

cp "$FLATPAK_DIR/com.k7.maui.yml" "$FLATPAK_DIR/k7-wrapper.sh" "$FLATPAK_DIR/proxy" "$FLATPAK_DIR/com.k7.maui.desktop" "$WORK/"
cp "$ICON" "$WORK/com.k7.maui.png"
chmod +x "$WORK/k7-wrapper.sh" "$WORK/proxy"

DATE="$(date -u +%Y-%m-%d)"
sed -e "s/@VERSION@/${VERSION}/g" -e "s/@DATE@/${DATE}/g" \
  "$FLATPAK_DIR/com.k7.maui.metainfo.xml" > "$WORK/com.k7.maui.metainfo.xml"

# flatpak-builder resolves "type: dir" sources relative to the manifest.
echo "build-flatpak: staging publish tree into $WORK/publish"
cp -a "$PUBLISH_DIR" "$WORK/publish"
chmod +x "$WORK/publish/K7.Clients.MAUI"

sed -i "s/runtime-version: '49'/runtime-version: '${RUNTIME_VERSION}'/" "$WORK/com.k7.maui.yml"

echo "build-flatpak: installing org.gnome.Platform//${RUNTIME_VERSION} (user) if needed"
flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak install -y --user "org.gnome.Platform//${RUNTIME_VERSION}" "org.gnome.Sdk//${RUNTIME_VERSION}"

echo "build-flatpak: flatpak-builder -> $OUT"
flatpak-builder \
  --user \
  --force-clean \
  --state-dir="$WORK/state" \
  --repo="$WORK/repo" \
  "$WORK/build" \
  "$WORK/com.k7.maui.yml"

mkdir -p "$(dirname "$OUT")"
flatpak build-bundle "$WORK/repo" "$OUT" "${APP_ID}" \
  --runtime-repo=https://dl.flathub.org/repo/flathub.flatpakrepo

echo "build-flatpak: wrote $OUT"
ls -lh "$OUT"
