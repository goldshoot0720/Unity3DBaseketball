#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

if [[ ! -f index.html ]]; then
  if [[ -f ../Builds/WebGL/index.html ]]; then
    cd ../Builds/WebGL
  else
    echo "Cannot find WebGL index.html. Unzip MiaBasketball-WebGL.zip and put this script next to index.html." >&2
    exit 1
  fi
fi

echo "Serving $PWD"
echo "Open http://127.0.0.1:8080/"
echo "Press Ctrl+C to stop."

if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "http://127.0.0.1:8080/" >/dev/null 2>&1 || true
elif command -v open >/dev/null 2>&1; then
  open "http://127.0.0.1:8080/" || true
fi

if command -v python3 >/dev/null 2>&1; then
  exec python3 -m http.server 8080
elif command -v python >/dev/null 2>&1; then
  exec python -m http.server 8080
else
  echo "Python not found. Install Python 3 and retry." >&2
  exit 1
fi
