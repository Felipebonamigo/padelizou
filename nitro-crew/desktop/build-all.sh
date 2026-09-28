#!/usr/bin/env bash
# Gera tudo o que dá para gerar NESTA máquina: build do jogo (../dist) + pacotes desktop + conferência do conteúdo.
#   ./build-all.sh            Linux x64 e Windows x64 + macOS universal (só num Mac). Testado em Linux em 28/09/2026;
#                             no Windows, rode no Git Bash (ou use os npm run dist:* direto).
#   ./build-all.sh --e2e      idem, e abre o pacote Linux de verdade (e2e.mjs; usa xvfb-run se não houver tela)
#   ./build-all.sh --mac-x64  num Linux: também o .app x64 SEM assinatura, só para conferir a estrutura
# Nada é enviado a lugar nenhum e nenhum segredo é lido aqui; a assinatura do macOS vem das variáveis de
# ambiente que o electron-builder já procura (desktop/README.md → macOS). A matriz do que sai onde está no README.
set -euo pipefail
cd "$(dirname "$0")"

E2E=0
MAC_X64=0
for arg in "$@"; do
  case "$arg" in
    --e2e) E2E=1 ;;
    --mac-x64) MAC_X64=1 ;;
    *) echo "opção desconhecida: $arg (use --e2e e/ou --mac-x64)" >&2; exit 2 ;;
  esac
done

step() { printf '\n==> %s\n' "$*"; }

step "jogo: typecheck + vite build (../dist)"
(cd .. && npm run build)

if [ ! -x node_modules/.bin/electron-builder ]; then
  step "desktop: npm ci (Electron + electron-builder, só nesta pasta)"
  npm ci
fi

step "limpando release/"
rm -rf release

HOST="$(uname -s)"
step "Linux x64"
npm run dist:linux
step "Windows x64 (sem wine: o electron-builder 26 grava ícone e versão do .exe em JavaScript)"
npm run dist:win
if [ "$HOST" = "Darwin" ]; then
  step "macOS universal (assina e notariza se as variáveis do README estiverem definidas)"
  npm run dist:mac
elif [ "$MAC_X64" = "1" ]; then
  step "macOS x64 SEM assinatura (estrutura apenas; não roda em Mac com Apple Silicon)"
  npm run predist:mac
  npx electron-builder --mac --x64
else
  echo "(macOS: pulado — universal e assinatura exigem um Mac; --mac-x64 gera um .app de conferência)"
fi

step "conferindo o conteúdo dos pacotes"
node check-package.mjs

if [ "$E2E" = "1" ]; then
  step "e2e: abrindo o pacote Linux"
  if [ "$HOST" != "Linux" ]; then
    echo "(e2e pulado: o e2e.mjs abre release/linux-unpacked, só em Linux)"
  elif [ -n "${DISPLAY:-}" ]; then
    node e2e.mjs
  else
    xvfb-run -a node e2e.mjs
  fi
fi

step "pronto"
du -sh release/*/ 2>/dev/null || true
