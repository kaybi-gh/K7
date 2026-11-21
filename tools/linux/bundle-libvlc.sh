#!/usr/bin/env bash
# Bundles a VLC 4 libvlc next to the K7 Linux client (libvlc/linux-x64), the counterpart of the
# VideoLAN.LibVLC.Windows natives copied to libvlc/win-x64 on Windows.
#
# Source: the VideoLAN "vlc" snap, "edge" channel (nightly VLC 4, base core24). The snap is
# built against Ubuntu 24.04 libraries (ffmpeg 6.1, dav1d, matroska, ...) that it does not
# contain itself (they come from the KDE content snap at snap run time), so this script copies
# them from the build host through ldd. Run it on Ubuntu 24.04 with vlc-plugin-base and
# vlc-plugin-video-output installed: the distro VLC 3 packages only serve as a dependency
# magnet, their shared libraries are the ones the VLC 4 plugins link against.
#
# Usage: tools/linux/bundle-libvlc.sh [output-dir]
#   output-dir   default src/Clients/MAUI/libvlc/linux-x64 (gitignored, picked up by the build)
# Environment:
#   VLC_SNAP_REVISION / VLC_SNAP_SHA256   pinned snap revision and its checksum
#   K7_VLC_SNAP_CACHE                      download cache (default ~/.cache/k7/vlc-snap)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="${1:-$REPO_ROOT/src/Clients/MAUI/libvlc/linux-x64}"
VLC_SNAP_REVISION="${VLC_SNAP_REVISION:-4504}"
VLC_SNAP_SHA256="${VLC_SNAP_SHA256:-1e7fa24146d6cceb9ccf5bd7ef5f74e51b7ef397916d9801affe05eb727687e0}"
VLC_SNAP_URL="${VLC_SNAP_URL:-https://api.snapcraft.io/api/v1/snaps/download/RT9mcUhVsRYrDLG8qnvGiy26NKvv6Qkd_${VLC_SNAP_REVISION}.snap}"
CACHE_DIR="${K7_VLC_SNAP_CACHE:-${XDG_CACHE_HOME:-$HOME/.cache}/k7/vlc-snap}"

for tool in curl unsquashfs ldd sha256sum; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "bundle-libvlc: missing '$tool' (apt-get install squashfs-tools curl)" >&2
    exit 1
  fi
done

# Plugin categories kept as a whole (minus DROP_PLUGINS); the other categories list the plugins
# K7 needs explicitly (file / HTTP input, Pulse output, vmem video output, ...).
KEEP_CATEGORIES="audio_filter audio_mixer codec demux packetizer video_chroma x86"
declare -A KEEP_ONLY=(
  # imem: libvlc media callbacks (LinuxHttpRangeMediaInput feeds Direct Play from .NET).
  [access]="libfilesystem_plugin.so libhttp_plugin.so libhttps_plugin.so libimem_plugin.so"
  [audio_output]="libpulse_plugin.so libadummy_plugin.so"
  [misc]="libxml_plugin.so libgnutls_plugin.so libdbus_screensaver_plugin.so libxdg_screensaver_plugin.so"
  [stream_filter]="libcache_read_plugin.so libprefetch_plugin.so libskiptags_plugin.so"
  [text_renderer]="libtdummy_plugin.so"
  [video_filter]="libdeinterlace_plugin.so libscale_plugin.so libblend_plugin.so libcanvas_plugin.so libcroppadd_plugin.so libtransform_plugin.so libfps_plugin.so libformatcrop_plugin.so"
  # libwdummy: libvlc_video_set_callbacks() sets window=dummy (lib/media_player.c), which is
  # the "vout window" shortcut of wdummy. Without it the vout never opens and vmem stays black.
  [video_output]="libvmem_plugin.so libvdummy_plugin.so libwdummy_plugin.so"
)
# Dropped inside the kept categories: encoders, hardware decoders without a vmem path, the
# S/PDIF pass-through decoder (the Pulse output rejects it after a 10s timeout), subtitle
# renderers (K7 draws text subtitles itself), inputs K7 never plays through libvlc (HLS, TS).
DROP_PLUGINS="libvaapi_plugin.so libx264_plugin.so libx26410b_plugin.so libx265_plugin.so libglinterop_nvdec_plugin.so libspdif_plugin.so libtospdif_plugin.so libzvbi_plugin.so libaribsub_plugin.so libaribcaption_plugin.so libkate_plugin.so libsvgdec_plugin.so liblibass_plugin.so libfluidsynth_plugin.so libfaad_plugin.so libmad_plugin.so libmpg123_plugin.so libtwolame_plugin.so libadaptive_plugin.so libmod_plugin.so libts_plugin.so libytdl_plugin.so libdemux_chromecast_plugin.so libspatialaudio_plugin.so"

# Shared libraries every GTK4 / WebKitGTK desktop and the GNOME Flatpak runtime already
# provide: never copied, resolved from the host at run time. Everything else that
# the kept plugins pull in (ffmpeg, dav1d, matroska, libva, ...) is copied to lib/.
# libbz2 / libbsd / libmd are bundled: the GNOME Flatpak runtime ships libbz2.so.1 -> 1.0.8
# (no libbz2.so.1.0 soname Ubuntu ffmpeg needs) and no libbsd at all.
HOST_LIBS='^(ld-linux-x86-64|libc|libm|libdl|libpthread|librt|libresolv|libutil|libnsl|libanl|libstdc\+\+|libgcc_s|libatomic|libz|libpulse|libpulsecommon-[0-9.]+|libdbus-1|libsystemd|libgnutls|libnettle|libhogweed|libgmp|libtasn1|libidn2|libunistring|libp11-kit|libffi|libglib-2\.0|libgobject-2\.0|libgio-2\.0|libgmodule-2\.0|libgthread-2\.0|libpcre2-8|libselinux|libmount|libblkid|libuuid|libcap|libgcrypt|libgpg-error|liblzma|libzstd|liblz4|libX11|libXext|libXau|libXdmcp|libXfixes|libXrender|libxcb[-a-z0-9]*|libdrm|libgbm|libEGL|libGL|libGLX|libGLdispatch|libOpenGL|libwayland-[a-z]+|libxkbcommon|libfontconfig|libfreetype|libharfbuzz|libfribidi|libpng16|libexpat|libbrotli[a-z]*|libgraphite2|libcairo|libcairo-gobject|libpango-1\.0|libpangocairo-1\.0|libpangoft2-1\.0|libgdk_pixbuf-2\.0|librsvg-2|libjpeg|libtiff|libwebp[a-z]*|libsharpyuv|libudev|libusb-1\.0|libcom_err|libkrb5[a-z]*|libk5crypto|libgssapi_krb5|libkeyutils|libcrypto|libssl|libsasl2|libldap[-_0-9a-z.]*|liblber[-0-9.]*|libcrypt|libtinfo|libncursesw|libpixman-1|libthai|libdatrie|libdeflate|libjbig|libLerc|libdw|libelf)\.so'

mkdir -p "$CACHE_DIR"
SNAP="$CACHE_DIR/vlc_${VLC_SNAP_REVISION}.snap"
if [ ! -f "$SNAP" ] || ! echo "$VLC_SNAP_SHA256  $SNAP" | sha256sum -c --quiet - >/dev/null 2>&1; then
  echo "bundle-libvlc: downloading VLC snap revision $VLC_SNAP_REVISION"
  # --retry alone skips TLS / connection failures (curl exit 35), which the snap store throws now and then.
  curl -fSL --retry 5 --retry-delay 5 --retry-all-errors --connect-timeout 30 -o "$SNAP.part" "$VLC_SNAP_URL"
  mv "$SNAP.part" "$SNAP"
  if ! echo "$VLC_SNAP_SHA256  $SNAP" | sha256sum -c --quiet -; then
    echo "bundle-libvlc: checksum mismatch for $SNAP" >&2
    rm -f "$SNAP"
    exit 1
  fi
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
unsquashfs -q -n -d "$WORK/snap" "$SNAP" meta/snap.yaml usr/lib/libvlc.so.12.0.0 usr/lib/libvlccore.so.9.0.0 usr/lib/vlc/libvlc_pulse.so.0.0.0 usr/lib/vlc/plugins >/dev/null
SNAP_VERSION="$(sed -n 's/^version: *//p' "$WORK/snap/meta/snap.yaml" | head -1)"
echo "bundle-libvlc: VLC $SNAP_VERSION (snap revision $VLC_SNAP_REVISION)"

SRC_LIB="$WORK/snap/usr/lib"
SRC_PLUGINS="$SRC_LIB/vlc/plugins"
rm -rf "$OUT"
mkdir -p "$OUT/lib" "$OUT/plugins"
cp -L "$SRC_LIB/libvlc.so.12.0.0" "$OUT/libvlc.so.12"
cp -L "$SRC_LIB/libvlccore.so.9.0.0" "$OUT/libvlccore.so.9"
cp -L "$SRC_LIB/vlc/libvlc_pulse.so.0.0.0" "$OUT/lib/libvlc_pulse.so.0"
# unsquashfs was given the real files only. DT_NEEDED is the soname (libvlc_pulse.so.0),
# and a distro VLC 3 does not ship that library, so ldd reports it missing and the Pulse
# plugin is dropped. The same gap would let a distro libvlccore.so.9 satisfy the other plugins.
ln -s libvlc.so.12.0.0 "$SRC_LIB/libvlc.so.12"
ln -s libvlccore.so.9.0.0 "$SRC_LIB/libvlccore.so.9"
ln -s libvlc_pulse.so.0.0.0 "$SRC_LIB/vlc/libvlc_pulse.so.0"

is_dropped() {
  case " $DROP_PLUGINS " in
    *" $1 "*) return 0 ;;
  esac
  return 1
}

selected=()
for category in $KEEP_CATEGORIES "${!KEEP_ONLY[@]}"; do
  dir="$SRC_PLUGINS/$category"
  if [ ! -d "$dir" ]; then
    echo "bundle-libvlc: warning, no plugin category $category in this VLC build"
    continue
  fi
  if [ -n "${KEEP_ONLY[$category]:-}" ]; then
    for name in ${KEEP_ONLY[$category]}; do
      if [ -f "$dir/$name" ]; then
        selected+=("$category/$name")
      else
        echo "bundle-libvlc: warning, missing plugin $category/$name"
      fi
    done
  else
    for f in "$dir"/*.so; do
      name="$(basename "$f")"
      if ! is_dropped "$name"; then
        selected+=("$category/$name")
      fi
    done
  fi
done

# ldd resolves against the bundle sonames first, then the snap tree (symlinks above), then the
# host. A distro VLC 3 libvlccore.so.9 must not be copied in their place.
export LD_LIBRARY_PATH="$OUT:$OUT/lib:$SRC_LIB:$SRC_LIB/vlc${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
declare -A bundled=()
declare -A host=()

# Prints "soname path" for resolved dependencies and "MISSING soname" for unresolved ones.
resolve() {
  ldd "$1" | awk '/=> not found/ { print "MISSING", $1 } /=> \// { print $1, $3 }'
}

# Records the dependencies of a file in the global bundled / host maps.
# Must not run inside $(...) : that subshell would drop the maps and ship plugins
# with no ffmpeg / dav1d / matroska next to them.
missing_deps=""
collect() {
  local file="$1"
  missing_deps=""
  local soname path
  while read -r soname path; do
    if [ "$soname" = "MISSING" ]; then
      missing_deps="$missing_deps $path"
      continue
    fi
    # Already placed under their soname. Anything else ldd finds, including libraries
    # that live inside the snap tree, has to be copied or the bundle only works on the
    # build machine.
    case "$soname" in
      libvlc.so.12|libvlccore.so.9|libvlc_pulse.so.0) continue ;;
    esac
    case "$path" in
      "$OUT"/*) continue ;;
    esac
    if [[ "$soname" =~ $HOST_LIBS ]]; then
      host["$soname"]=1
    else
      bundled["$soname"]="$path"
    fi
  done < <(resolve "$file")
}

for core in "$OUT/libvlc.so.12" "$OUT/libvlccore.so.9" "$OUT/lib/libvlc_pulse.so.0"; do
  collect "$core"
  if [ -n "$missing_deps" ]; then
    echo "bundle-libvlc: $(basename "$core") needs$missing_deps (install the VLC 3 plugin packages so ldd can find them)" >&2
    exit 1
  fi
done

kept=()
for rel in "${selected[@]}"; do
  src="$SRC_PLUGINS/$rel"
  collect "$src"
  if [ -n "$missing_deps" ]; then
    echo "bundle-libvlc: dropping $rel (unresolved:$missing_deps)"
    continue
  fi
  mkdir -p "$OUT/plugins/$(dirname "$rel")"
  cp -L "$src" "$OUT/plugins/$rel"
  kept+=("$rel")
done

# Without these the player cannot decode, output audio or paint frames: a host whose ffmpeg
# ABI differs from the snap's (Ubuntu 26.04 ships libavcodec 61, the snap links 60) drops them
# above, which must fail the build rather than ship a bundle that plays nothing.
for required in codec/libavcodec_plugin.so audio_output/libpulse_plugin.so video_output/libvmem_plugin.so video_output/libwdummy_plugin.so access/libimem_plugin.so access/libhttp_plugin.so access/libhttps_plugin.so access/libfilesystem_plugin.so video_chroma/libswscale_plugin.so; do
  if [ ! -f "$OUT/plugins/$required" ]; then
    echo "bundle-libvlc: required plugin $required is missing; run this script on Ubuntu 24.04 (the snap's core24 base) with vlc-plugin-base installed" >&2
    exit 1
  fi
done

copy_bundled() {
  local soname
  for soname in "${!bundled[@]}"; do
    if [ ! -e "$OUT/lib/$soname" ]; then
      cp -L "${bundled[$soname]}" "$OUT/lib/$soname"
    fi
  done
}

copy_bundled
# Plugins link ffmpeg, which links dav1d / matroska / ... One pass only sees the first edge.
for _round in 1 2 3 4 5 6; do
  before="${#bundled[@]}"
  while IFS= read -r -d '' file; do
    collect "$file"
    if [ -n "$missing_deps" ]; then
      echo "bundle-libvlc: $(basename "$file") needs$missing_deps" >&2
      exit 1
    fi
  done < <(find "$OUT" -name '*.so*' -type f -print0)
  [ "${#bundled[@]}" -eq "$before" ] && break
  copy_bundled
done

if [ ! -f "$OUT/lib/libavcodec.so.60" ]; then
  echo "bundle-libvlc: libavcodec.so.60 was not copied into the bundle" >&2
  exit 1
fi

# GNOME Flatpak runtime ships libbz2.so.1 but not Ubuntu's libbz2.so.1.0 soname that
# ffmpeg DT_NEEDED asks for. Force-copy these even when the build host resolves them.
for soname in libbz2.so.1.0 libbsd.so.0 libmd.so.0; do
  if [ -e "$OUT/lib/$soname" ]; then
    continue
  fi
  src="$(find /usr/lib /lib -name "$soname" 2>/dev/null | head -1 || true)"
  if [ -z "$src" ]; then
    echo "bundle-libvlc: missing $soname on the build host (apt-get install libbz2-1.0 libbsd0)" >&2
    exit 1
  fi
  cp -L "$src" "$OUT/lib/$soname"
  echo "bundle-libvlc: forced $soname for Flatpak (from $src)"
done

# Drop bundled libva*: the Ubuntu snap copy is older than Flatpak codecs-extra / ffmpeg-full
# need (vaMapBuffer2). Soft decode uses the runtime libva; shipping ours breaks WebKit HLS.
rm -f "$OUT/lib"/libva.so* "$OUT/lib"/libva-*.so*
echo "bundle-libvlc: removed libva* from private lib/ (use runtime libva)"

# Every bundled object must resolve with the bundle plus the host libraries only.
export LD_LIBRARY_PATH="$OUT:$OUT/lib"
unresolved=0
while IFS= read -r -d '' file; do
  if ldd "$file" | grep -q "=> not found"; then
    echo "bundle-libvlc: unresolved dependency in ${file#"$OUT"/}:" >&2
    ldd "$file" | grep "=> not found" >&2
    unresolved=1
  fi
done < <(find "$OUT" -name '*.so*' -type f -print0)
if [ "$unresolved" -ne 0 ]; then
  exit 1
fi

{
  echo "VLC $SNAP_VERSION"
  echo "Source: VideoLAN vlc snap, edge channel, revision $VLC_SNAP_REVISION"
  echo "Built: $(date -u +%Y-%m-%dT%H:%M:%SZ) on $(. /etc/os-release && echo "$PRETTY_NAME")"
  echo
  echo "Plugins (${#kept[@]}):"
  printf '  %s\n' "${kept[@]}" | sort
  echo
  echo "Private libraries (${#bundled[@]}):"
  printf '  %s\n' "${!bundled[@]}" | sort
  echo
  echo "Host libraries (${#host[@]}):"
  printf '  %s\n' "${!host[@]}" | sort
} > "$OUT/BUNDLE.txt"

echo "bundle-libvlc: ${#kept[@]} plugins, ${#bundled[@]} private libraries, ${#host[@]} host libraries -> $OUT ($(du -sh "$OUT" | cut -f1))"
