#!/usr/bin/env bash
# Deploy do ambiente de TESTE (dev.padelizou.com.br) — via GitHub.
#
# Mesmo fluxo da produção: só instala commit que passou no CI.
# Voltar: ssh root@179.197.233.184 /opt/padelizou-deploy/rollback.sh dev
set -euo pipefail

SERVIDOR="root@179.197.233.184"

cd "$(dirname "$0")/.."   # raiz do repositório

if [ -n "$(git status --porcelain)" ]; then
  echo "ERRO: há alterações não commitadas."
  echo "Só se publica o que está no GitHub — faça commit (e push) primeiro."
  git status --short
  exit 1
fi

echo "==> Enviando pro GitHub (se já estiver lá, não muda nada)..."
git push origin main

SHA=$(git rev-parse HEAD)
echo "==> Pedindo pro servidor instalar o commit $SHA no DEV..."
ssh "$SERVIDOR" "/opt/padelizou-deploy/deploy.sh dev $SHA"
