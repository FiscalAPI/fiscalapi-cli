#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Uso: publish-linux.sh <versión> <carpeta de salida>" >&2
  exit 2
fi

version="$1"
output="$2"
max_glibc="2.28"

dnf install -y --setopt=install_weak_deps=False clang zlib-devel binutils libicu findutils gnupg2 > /dev/null

export DOTNET_ROOT=/opt/dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sig -o /tmp/dotnet-install.sig
bash "$(dirname "${BASH_SOURCE[0]}")/verify-dotnet-install.sh" /tmp/dotnet-install.sh /tmp/dotnet-install.sig
bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir "$DOTNET_ROOT" --no-path

dotnet publish src/FiscalApi.Cli -c Release -r linux-x64 -p:Version="$version" -o "$output"

binary="$output/fiscalapi"
required_glibc=$(objdump -T "$binary" | grep -oE 'GLIBC_[0-9]+(\.[0-9]+)+' | sed 's/^GLIBC_//' | sort -uV | tail -n 1)
newest=$(printf '%s\n%s\n' "$required_glibc" "$max_glibc" | sort -V | tail -n 1)
if [ "$newest" != "$max_glibc" ]; then
  echo "::error::fiscalapi pide glibc $required_glibc; el release promete correr desde glibc $max_glibc." >&2
  exit 1
fi
echo "fiscalapi pide glibc $required_glibc o más nueva."

actual=$(FISCALAPI_NO_UPDATE_CHECK=1 "$binary" --version)
if [ "$actual" != "$version" ]; then
  echo "::error::fiscalapi --version devolvió '$actual' en AlmaLinux 8; se esperaba '$version'." >&2
  exit 1
fi
