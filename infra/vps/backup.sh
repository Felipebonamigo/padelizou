#!/usr/bin/env bash
# Backup diário do Padelizou (cron: /etc/cron.d/padelizou-backup, 4h da manhã).
# Fica em /usr/local/bin/backup-padelizou.sh no servidor.
#
# Cobre as três coisas que não dá pra recuperar de outro lugar:
#   1. banco de produção (pg_dump)
#   2. arquivos enviados pelos usuários + tokens do Google + appsettings
#      (tudo em /opt/padelizou-shared, prod e dev)
# O código em si não precisa de backup — está no GitHub.
set -eo pipefail

DEST=/var/backups/padelizou
mkdir -p "$DEST"
STAMP=$(date +%Y%m%d_%H%M%S)

# 1. Banco de produção
sudo -u postgres pg_dump db_padel | gzip > "$DEST/db_padel_$STAMP.sql.gz"

# 2. Uploads, tokens e configs (prod + dev)
tar -czf "$DEST/arquivos_$STAMP.tar.gz" -C /opt padelizou-shared

# Mantém 14 dias de histórico
find "$DEST" -name 'db_padel_*.sql.gz' -mtime +14 -delete
find "$DEST" -name 'arquivos_*.tar.gz' -mtime +14 -delete
