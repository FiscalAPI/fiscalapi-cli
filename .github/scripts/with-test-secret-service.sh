#!/usr/bin/env bash
# Corre un comando con un Secret Service de prueba: gnome-keyring desbloqueado con una contraseña desechable, en
# la sesión de D-Bus que abre quien lo llama. Lo usan el CI y la verificación local en Docker:
#   dbus-run-session -- bash .github/scripts/with-test-secret-service.sh dotnet test FiscalApi.Cli.slnx
set -euo pipefail

printf '%s' "llavero-de-prueba" | gnome-keyring-daemon --unlock --components=secrets > /dev/null

# gnome-keyring publica org.freedesktop.secrets después de volver: se espera a que el nombre exista en el bus,
# con tope, en lugar de dormir un tiempo fijo que en un runner lento no alcanza.
for _ in $(seq 1 50); do
  if dbus-send --session --print-reply --dest=org.freedesktop.DBus /org/freedesktop/DBus \
      org.freedesktop.DBus.ListNames | grep -q '"org.freedesktop.secrets"'; then
    exec "$@"
  fi
  sleep 0.2
done

echo "::error::org.freedesktop.secrets no apareció en la sesión de D-Bus en 10 s." >&2
exit 1
