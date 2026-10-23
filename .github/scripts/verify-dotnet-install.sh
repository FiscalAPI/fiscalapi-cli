#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Uso: verify-dotnet-install.sh <dotnet-install.sh> <dotnet-install.sig>" >&2
  exit 2
fi

script="$1"
signature="$2"
expected_fingerprint="2B930AB1228D11D5D7F6B6ACB9CF1A51FC7D3ACF"
public_key="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/dotnet-install.asc"

GNUPGHOME=$(mktemp -d)
export GNUPGHOME
trap 'rm -rf "$GNUPGHOME"' EXIT
gpg --batch --quiet --import "$public_key"

if ! status=$(gpg --batch --status-fd 1 --verify "$signature" "$script" 2> /dev/null); then
  echo "::error::La firma GPG de dotnet-install.sh no es válida." >&2
  exit 1
fi
if ! grep -q "^\[GNUPG:\] VALIDSIG $expected_fingerprint " <<< "$status"; then
  echo "::error::dotnet-install.sh no está firmado con la llave $expected_fingerprint." >&2
  exit 1
fi
echo "dotnet-install.sh: firma GPG válida de la llave $expected_fingerprint."
