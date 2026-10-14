#!/usr/bin/env bash
# Verifica dotnet-install.sh con su firma GPG antes de ejecutarlo (Microsoft Learn, "dotnet-install scripts reference",
# sección "Signature validation of dotnet-install.sh"). La llave pública va versionada junto a este script
# (dotnet-install.asc, descargada de https://dot.net/v1/dotnet-install.asc el 2026-10-13) y se exige la huella que publica
# Microsoft en esa página:
#   2B93 0AB1 228D 11D5 D7F6  B6AC B9CF 1A51 FC7D 3ACF  Microsoft DevUXTeamPrague <devuxteamprague@microsoft.com>
# Si Microsoft rota la llave, la verificación falla y la llave nueva se actualiza a propósito, comprobando su huella.
#
# Uso: verify-dotnet-install.sh <dotnet-install.sh> <dotnet-install.sig>
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Uso: verify-dotnet-install.sh <dotnet-install.sh> <dotnet-install.sig>" >&2
  exit 2
fi

script="$1"
signature="$2"
expected_fingerprint="2B930AB1228D11D5D7F6B6ACB9CF1A51FC7D3ACF"
public_key="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/dotnet-install.asc"

# Un anillo de llaves desechable: solo contiene la llave de Microsoft y no toca el del usuario.
GNUPGHOME=$(mktemp -d)
export GNUPGHOME
trap 'rm -rf "$GNUPGHOME"' EXIT
gpg --batch --quiet --import "$public_key"

# gpg --verify sale con 0 aunque la llave no tenga confianza (lo advierte Microsoft Learn); lo que cuenta es la línea
# VALIDSIG del estado, que solo aparece con una firma buena y trae la huella de la llave que firmó.
if ! status=$(gpg --batch --status-fd 1 --verify "$signature" "$script" 2> /dev/null); then
  echo "::error::La firma GPG de dotnet-install.sh no es válida." >&2
  exit 1
fi
if ! grep -q "^\[GNUPG:\] VALIDSIG $expected_fingerprint " <<< "$status"; then
  echo "::error::dotnet-install.sh no está firmado con la llave $expected_fingerprint." >&2
  exit 1
fi
echo "dotnet-install.sh: firma GPG válida de la llave $expected_fingerprint."
