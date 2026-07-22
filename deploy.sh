#!/usr/bin/env bash
# Publica o Padelizou e envia pro servidor de produção.
# Preencher SERVIDOR (usuário@ip) antes de usar pela primeira vez.
set -euo pipefail

SERVIDOR="root@179.197.233.184"
PASTA_REMOTA="/opt/padelizou"
SERVICO="padelizou"

echo "==> Publicando (framework-dependent, o runtime já está instalado no servidor)..."
rm -rf ./publish
dotnet publish -c Release -o ./publish

echo "==> Enviando arquivos pro servidor ($SERVIDOR:$PASTA_REMOTA)..."
# scp em vez de rsync (rsync não vem por padrão no Git Bash do Windows).
# App_Data/GoogleTokens e wwwroot/uploads são gerados em runtime no servidor,
# não fazem parte do publish local, então não precisam de --exclude aqui.
ssh "$SERVIDOR" "mkdir -p $PASTA_REMOTA"
scp -r ./publish/. "$SERVIDOR:$PASTA_REMOTA/"

echo "==> Reiniciando o serviço..."
ssh "$SERVIDOR" "systemctl restart $SERVICO && sleep 2 && systemctl status $SERVICO --no-pager -l"

echo "==> Feito. Se essa atualização incluiu uma migration nova, rode:"
echo "    dotnet ef database update --connection \"<connection string de produção>\""
