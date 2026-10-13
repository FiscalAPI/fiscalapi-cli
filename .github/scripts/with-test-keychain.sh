#!/usr/bin/env bash
# Corre un comando con un llavero de prueba de macOS como llavero por omisión del usuario: desbloqueado, sin bloqueo por
# tiempo y desechable. Así CredentialStoreTests (SecItemAdd/SecItemCopyMatching/SecItemDelete sobre el llavero por omisión)
# no depende del estado del llavero de sesión del runner. Lo usa release.yml:
#   bash .github/scripts/with-test-keychain.sh dotnet test FiscalApi.Cli.slnx --filter "FullyQualifiedName~CredentialStoreTests"
set -euo pipefail
# Compatible con el bash 3.2 de macOS: un arreglo vacío se expande con ${a[@]+"${a[@]}"} para no chocar con set -u.

keychain="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/fiscalapi-cli-test.keychain-db"
password="llavero-de-prueba"
# `security` imprime las rutas entre comillas, una por línea; xargs las quita. Las de un runner no llevan espacios.
original_default=$( (security default-keychain -d user 2> /dev/null || true) | xargs)
read -r -a original_search_list <<< "$( (security list-keychains -d user 2> /dev/null || true) | xargs)"

restore() {
  if [ -n "$original_default" ]; then
    security default-keychain -d user -s "$original_default" || true
  fi
  security list-keychains -d user -s ${original_search_list[@]+"${original_search_list[@]}"} || true
  security delete-keychain "$keychain" || true
}
trap restore EXIT

security create-keychain -p "$password" "$keychain"
# Sin -t ni -l: el llavero no se bloquea solo a mitad de las pruebas.
security set-keychain-settings "$keychain"
security unlock-keychain -p "$password" "$keychain"
security list-keychains -d user -s "$keychain" ${original_search_list[@]+"${original_search_list[@]}"}
security default-keychain -d user -s "$keychain"

"$@"
