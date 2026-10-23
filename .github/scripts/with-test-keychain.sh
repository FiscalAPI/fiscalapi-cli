#!/usr/bin/env bash
set -euo pipefail

keychain="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/fiscalapi-cli-test.keychain-db"
password="llavero-de-prueba"
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
security set-keychain-settings "$keychain"
security unlock-keychain -p "$password" "$keychain"
security list-keychains -d user -s "$keychain" ${original_search_list[@]+"${original_search_list[@]}"}
security default-keychain -d user -s "$keychain"

"$@"
