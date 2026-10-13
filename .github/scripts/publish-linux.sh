#!/usr/bin/env bash
# Publica fiscalapi para linux-x64 con Native AOT dentro de AlmaLinux 8 (glibc 2.28). Un binario AOT de Linux solo corre
# en la glibc con la que se enlazó o en una más nueva (Microsoft Learn, "Native AOT deployment"): enlazarlo aquí lo deja
# correr en RHEL 8 y sus derivados, Debian 10+, Ubuntu 20.04+ y SLES 15, en lugar de exigir la glibc 2.35 de Ubuntu 22.04.
#
# Lo usan el job linux-x64 de release.yml y la verificación local, desde la raíz del repo:
#   docker run --rm -v "$PWD:/src" -w /src almalinux:8@sha256:<digest> bash .github/scripts/publish-linux.sh <versión> <salida>
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Uso: publish-linux.sh <versión> <carpeta de salida>" >&2
  exit 2
fi

version="$1"
output="$2"
# La glibc de AlmaLinux 8. Si el binario pide una más nueva, se rompió la promesa de compatibilidad y el release no sigue.
max_glibc="2.28"

# Prerrequisitos de Native AOT en RHEL 8 (Microsoft Learn): clang y zlib-devel; binutils trae objcopy (StripSymbols) y
# objdump. libicu es del SDK, no del binario (InvariantGlobalization). gnupg2 verifica el script de instalación.
dnf install -y --setopt=install_weak_deps=False clang zlib-devel binutils libicu findutils gnupg2 > /dev/null

# El SDK exacto de global.json, con el script oficial (https://dot.net/v1/dotnet-install.sh), que solo se ejecuta si su
# firma GPG es la de Microsoft. El script baja el SDK por HTTPS de builds.dotnet.microsoft.com.
export DOTNET_ROOT=/opt/dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sig -o /tmp/dotnet-install.sig
bash "$(dirname "${BASH_SOURCE[0]}")/verify-dotnet-install.sh" /tmp/dotnet-install.sh /tmp/dotnet-install.sig
bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir "$DOTNET_ROOT" --no-path

# TreatWarningsAsErrors (Directory.Build.props) convierte cualquier advertencia IL de trimming/AOT en un error.
dotnet publish src/FiscalApi.Cli -c Release -r linux-x64 -p:Version="$version" -o "$output"

binary="$output/fiscalapi"
required_glibc=$(objdump -T "$binary" | grep -oE 'GLIBC_[0-9]+(\.[0-9]+)+' | sed 's/^GLIBC_//' | sort -uV | tail -n 1)
newest=$(printf '%s\n%s\n' "$required_glibc" "$max_glibc" | sort -V | tail -n 1)
if [ "$newest" != "$max_glibc" ]; then
  echo "::error::fiscalapi pide glibc $required_glibc; el release promete correr desde glibc $max_glibc." >&2
  exit 1
fi
echo "fiscalapi pide glibc $required_glibc o más nueva."

# Humo en la distro más vieja que se soporta, sin el aviso de versión nueva (el contenedor no hereda CI=true).
actual=$(FISCALAPI_NO_UPDATE_CHECK=1 "$binary" --version)
if [ "$actual" != "$version" ]; then
  echo "::error::fiscalapi --version devolvió '$actual' en AlmaLinux 8; se esperaba '$version'." >&2
  exit 1
fi
