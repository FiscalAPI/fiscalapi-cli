#!/usr/bin/env bash
set -euo pipefail

printf '%s' "llavero-de-prueba" | gnome-keyring-daemon --unlock --components=secrets > /dev/null

for _ in $(seq 1 50); do
  if dbus-send --session --print-reply --dest=org.freedesktop.DBus /org/freedesktop/DBus \
      org.freedesktop.DBus.ListNames | grep -q '"org.freedesktop.secrets"'; then
    exec "$@"
  fi
  sleep 0.2
done

echo "::error::org.freedesktop.secrets no apareció en la sesión de D-Bus en 10 s." >&2
exit 1
