#!/usr/bin/env bash
# Sobe o build exportado (build/windows e build/linux) pra Steam com o steamcmd.
# Pré-requisitos: steamcmd instalado; AppID e DepotIDs preenchidos em steam/app_build.vdf;
# conta de build (NUNCA a conta pessoal) com Steam Guard já autorizado nesta máquina.
# Uso: STEAM_USUARIO=conta_de_build steam/publicar.sh
set -euo pipefail
cd "$(dirname "$0")"

: "${STEAM_USUARIO:?defina STEAM_USUARIO com a conta de build}"
if grep -qE '"AppID"[[:space:]]+"0"' app_build.vdf; then
  echo "app_build.vdf ainda está com AppID 0 — preencha com o AppID do Steamworks." >&2
  exit 1
fi
for plataforma in windows linux; do
  if [ ! -d "../build/$plataforma" ]; then
    echo "Falta ../build/$plataforma — exporte antes (ver README)." >&2
    exit 1
  fi
done
steamcmd +login "$STEAM_USUARIO" +run_app_build "$(pwd)/app_build.vdf" +quit
