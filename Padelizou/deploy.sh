#!/usr/bin/env bash
# Deploy de PRODUÇÃO — via GitHub, não mais pelo disco local.
#
# Fluxo: commit → push → CI roda os testes → CI gera o pacote →
#        o VPS baixa e instala exatamente aquele commit.
# Se algo der errado, voltar é 1 comando:
#        ssh root@179.197.233.184 /opt/padelizou-deploy/rollback.sh prod
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
echo "==> Pedindo pro servidor instalar o commit $SHA..."
ssh "$SERVIDOR" "/opt/padelizou-deploy/deploy.sh prod $SHA"
