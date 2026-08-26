#!/usr/bin/env bash
set -euo pipefail

# Host runner installs .NET + maui-android before this container starts.
# Mounts in renovate.yml expose SDK, workloads, and Java to the Renovate image.
export DOTNET_ROOT="/usr/share/dotnet"
export PATH="${DOTNET_ROOT}:${PATH}"

if [ -d "/opt/hostedtoolcache/Java_Temurin-Hotspot_jdk" ]; then
  JAVA_HOME="$(find /opt/hostedtoolcache/Java_Temurin-Hotspot_jdk -mindepth 1 -maxdepth 1 -type d | sort -V | tail -1)"
  export JAVA_HOME
  export PATH="${JAVA_HOME}/bin:${PATH}"
fi

if [ "${EVENT_NAME:-}" = "schedule" ]; then
  export RENOVATE_SCHEDULE='["* 5-9 * * 1"]'
  export RENOVATE_TIMEZONE="Europe/Paris"
fi

exec runuser -u ubuntu -- env \
  RENOVATE_TOKEN="${RENOVATE_TOKEN:?}" \
  RENOVATE_PLATFORM="${RENOVATE_PLATFORM:-github}" \
  RENOVATE_REPOSITORIES="${RENOVATE_REPOSITORIES:-}" \
  RENOVATE_BINARY_SOURCE="${RENOVATE_BINARY_SOURCE:-global}" \
  RENOVATE_GIT_AUTHOR="${RENOVATE_GIT_AUTHOR:-}" \
  RENOVATE_SCHEDULE="${RENOVATE_SCHEDULE:-}" \
  RENOVATE_TIMEZONE="${RENOVATE_TIMEZONE:-}" \
  LOG_LEVEL="${LOG_LEVEL:-info}" \
  DOTNET_ROOT="${DOTNET_ROOT}" \
  PATH="${PATH}" \
  JAVA_HOME="${JAVA_HOME:-}" \
  renovate "$@"
