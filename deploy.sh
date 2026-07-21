#!/usr/bin/env bash
# Publica o Padelizou e envia pro servidor de produção.
# Preencher SERVIDOR (usuário@ip) antes de usar pela primeira vez.
set -euo pipefail

SERVIDOR="usuario@SEU_IP_AQUI"
PASTA_REMOTA="/opt/padelizou"
SERVICO="padelizou"

echo "==> Publicando (framework-dependent, o runtime já está instalado no servidor)..."
rm -rf ./publish
dotnet publish -c Release -o ./publish

echo "==> Enviando arquivos pro servidor ($SERVIDOR:$PASTA_REMOTA)..."
rsync -avz --delete \
    --exclude 'App_Data/GoogleTokens/' \
    --exclude 'wwwroot/uploads/' \
    ./publish/ "$SERVIDOR:$PASTA_REMOTA/"

echo "==> Reiniciando o serviço..."
ssh "$SERVIDOR" "sudo systemctl restart $SERVICO && sleep 2 && sudo systemctl status $SERVICO --no-pager -l"

echo "==> Feito. Se essa atualização incluiu uma migration nova, rode:"
echo "    dotnet ef database update --connection \"<connection string de produção>\""
