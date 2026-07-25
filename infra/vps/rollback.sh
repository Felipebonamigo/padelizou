#!/usr/bin/env bash
# Rollback do Padelizou em 1 comando — volta pra versão que estava no ar antes.
# Fica em /opt/padelizou-deploy/rollback.sh no servidor.
#
# Uso:  rollback.sh <prod|dev>
#
# Rodar de novo desfaz o rollback (as duas versões trocam de lugar).
# Pra voltar mais longe: deploy.sh <env> build-N-sha (as 5 últimas ficam
# guardadas em /opt/padelizou-releases/<env>/, e os builds seguem no GitHub).
set -euo pipefail

AMBIENTE="${1:?Uso: rollback.sh <prod|dev>}"

case "$AMBIENTE" in
  prod) SERVICO="padelizou";     LIVE="/opt/padelizou";     URL="https://padelizou.com.br/healthz" ;;
  dev)  SERVICO="padelizou-dev"; LIVE="/opt/padelizou-dev"; URL="https://dev.padelizou.com.br/healthz" ;;
  *) echo "ERRO: ambiente '$AMBIENTE' inválido (use prod ou dev)"; exit 1 ;;
esac

RELEASES="/opt/padelizou-releases/$AMBIENTE"

[ -f "$RELEASES/.anterior" ] || { echo "ERRO: não há versão anterior registrada."; exit 1; }
ANTERIOR=$(cat "$RELEASES/.anterior")
[ -d "$ANTERIOR" ] || { echo "ERRO: a versão anterior ($ANTERIOR) não existe mais no disco."; exit 1; }

ATUAL=$(readlink "$LIVE")
echo "==> Voltando de $(basename "$ATUAL") pra $(basename "$ANTERIOR")..."

ln -sfn "$ANTERIOR" "$LIVE"
echo "$ATUAL" > "$RELEASES/.anterior"   # permite desfazer o rollback
systemctl restart "$SERVICO"

for i in $(seq 1 30); do
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "$URL" || true)
  [ "$CODE" = "200" ] && { echo "==> Feito. $(basename "$ANTERIOR") no ar e saudável."; exit 0; }
  sleep 2
done

echo "ERRO: /healthz não respondeu 200 em 60s. Verifique: journalctl -u $SERVICO -n 50"
exit 1
