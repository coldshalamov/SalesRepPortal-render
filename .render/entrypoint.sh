#!/bin/sh
set -e

if [ -n "${PORT:-}" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
elif [ -z "${ASPNETCORE_URLS:-}" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:8080"
fi

exec dotnet LeadManagementPortal.dll
