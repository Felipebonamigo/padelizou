#!/usr/bin/env bash
# Segundo dump do dia — a rede que corta a janela de perda de 24h pela metade.
# Fica em /usr/local/bin/backup-meio-dia.sh (cron: /etc/cron.d/padelizou-backup-meio-dia).
#
# POR QUE EXISTE: o backup completo roda às 4h. Entre uma rodada e a seguinte, tudo o que o
# site recebe vive só no disco do VPS. Isso era aceitável enquanto a base era de teste; com
# inscrição paga entrando pelo site, perder um dia de pagamento não é a mesma coisa que perder
# um dia de dado fictício. Com este, o pior caso cai de 24h para 12h.
#
# ── AS TRÊS COISAS QUE ELE **NÃO** FAZ, E O PORQUÊ ────────────────────────────────────────
#
# 1. NÃO copia as fotos (o tar de /opt/padelizou-shared, 17 MB).
#    O dump do banco tem ~96 KB; o tar é 180× maior e muda pouco de um dia pro outro. O que
#    corre risco de verdade entre 4h e 16h é INSCRIÇÃO, PAGAMENTO e PLACAR — tudo no banco.
#    No pior caso perde-se meio dia de FOTO (perfil sem imagem, que a pessoa reenvia), nunca
#    meio dia de dinheiro.
#
# 2. NÃO grava o carimbo do vigia (/var/lib/padelizou/ultimo-backup-drive).
#    ⚠️ Este é o ponto delicado: se gravasse, uma falha na rodada COMPLETA das 4h30 ficaria
#    escondida atrás do sucesso desta — o vigia veria carimbo fresco e ficaria calado enquanto
#    as fotos e os tokens passassem dias sem cópia. O vigia continua vigiando a rodada
#    completa, que é a única que protege tudo. Esta aqui é rede extra, não substituta.
#
# 3. NÃO tem retenção própria.
#    O nome segue o mesmo padrão `db_padel_<data>_<hora>.sql.gz` do backup das 4h, então o
#    `find -mtime +14 -delete` que já existe lá limpa os dois. Uma segunda regra de limpeza
#    seria uma segunda chance de apagar o arquivo errado.
set -eo pipefail

# O pg_dump reclama ("could not change directory to /root") quando roda a partir de um
# diretório que o usuário postgres não lê. O cron.d executa de /root — daí o cd.
cd /tmp

RCLONE=/usr/local/bin/rclone
COFRE=cofre-b2                  # principal
COFRE_RESERVA=cofre-padelizou   # reserva (Google Drive)
DEST=/var/backups/padelizou

log() { echo "[$(date '+%d/%m %H:%M:%S')] $*"; }

STAMP=$(date +%Y%m%d_%H%M%S)
ARQUIVO="$DEST/db_padel_$STAMP.sql.gz"

mkdir -p "$DEST"

log "Dump do meio-dia."
sudo -u postgres pg_dump db_padel | gzip > "$ARQUIVO"

# Dump vazio ou minúsculo é sinal de pg_dump que morreu no meio — e um arquivo desses no
# lugar do bom é pior que arquivo nenhum, porque parece backup. 10 KB é folga larga: o dump
# real está em ~96 KB e só cresce.
TAMANHO=$(stat -c %s "$ARQUIVO")
if [ "$TAMANHO" -lt 10240 ]; then
  log "ERRO: o dump saiu com $TAMANHO bytes (esperado > 10 KB). Apagando e saindo com erro."
  rm -f "$ARQUIVO"
  exit 1
fi
log "Banco copiado ($(du -h "$ARQUIVO" | cut -f1))."

# Ficar só no VPS não protege contra o que este backup existe pra cobrir: o VPS sumir. Sobe
# agora, sem esperar a rodada das 4h30 do dia seguinte — senão a janela de fora continuaria 24h.
#
# O `copy` compara antes de transferir, então só o arquivo novo sobe.
log "Enviando pro Backblaze B2."
$RCLONE copy "$DEST" "$COFRE:banco" \
  --include "db_padel_*.sql.gz" \
  --transfers 4 --retries 3
log "B2 pronto."

# Mesma hierarquia do backup principal: o B2 é o que vale (falha dele é fatal, acima); o Drive
# é reserva e não pode derrubar a rodada — foi token vencido do Google que causou os 3 dias sem
# cópia em 05-07/08/2026.
if $RCLONE copy "$DEST" "$COFRE_RESERVA:banco" \
     --include "db_padel_*.sql.gz" \
     --transfers 4 --retries 3 >/dev/null 2>&1; then
  log "Drive (reserva) pronto."
else
  log "AVISO: o Drive nao aceitou a copia — reserva pulada, o B2 ja tem o dump."
fi

log "Pronto."
