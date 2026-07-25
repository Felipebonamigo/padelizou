#!/usr/bin/env bash
# Publica o Padelizou no ambiente de teste isolado (dev.padelizou.com.br) — instância e banco
# Postgres próprios (db_padel_dev), separados da produção. Mesmo fluxo do deploy.sh, só
# apontando pra outra pasta/serviço no mesmo servidor.
set -euo pipefail

SERVIDOR="root@179.197.233.184"
PASTA_REMOTA="/opt/padelizou-dev"
SERVICO="padelizou-dev"

echo "==> Publicando (framework-dependent, o runtime já está instalado no servidor)..."
rm -rf ./publish
dotnet publish -c Release -o ./publish

echo "==> Enviando arquivos pro servidor ($SERVIDOR:$PASTA_REMOTA)..."
ssh "$SERVIDOR" "mkdir -p $PASTA_REMOTA"
scp -r ./publish/. "$SERVIDOR:$PASTA_REMOTA/"

echo "==> Reiniciando o serviço..."
ssh "$SERVIDOR" "systemctl restart $SERVICO && sleep 2 && systemctl status $SERVICO --no-pager -l"

echo "==> Feito. O app aplica as migrations sozinho no startup (db.Database.Migrate())."
