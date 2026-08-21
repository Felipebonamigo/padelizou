#!/usr/bin/env bash
# Backup diário do Padelizou (cron: /etc/cron.d/padelizou-backup, 4h da manhã).
# Fica em /usr/local/bin/backup-padelizou.sh no servidor.
#
# Cobre as três coisas que não dá pra recuperar de outro lugar:
#   1. banco de produção (pg_dump)
#   2. arquivos enviados pelos usuários + tokens do Google + appsettings
#      (tudo em /opt/padelizou-shared, prod e dev)
#   3. a configuração que só existe NO VPS — Caddyfile, units do systemd, cron, rclone
#      (achado da análise de sistema de 21/08/2026: sem isto, reconstruir o servidor do zero
#      dependia de alguém LEMBRAR de cada arquivo espalhado fora deste repositório — o
#      Caddyfile versionado aqui é só um RECORTE de referência, sem os outros 4 sites que o
#      VPS serve, e o unit real do systemd nunca esteve em lugar nenhum além do servidor)
# O código em si não precisa de backup — está no GitHub.
set -eo pipefail

DEST=/var/backups/padelizou
mkdir -p "$DEST"
STAMP=$(date +%Y%m%d_%H%M%S)

# 1. Banco de produção
sudo -u postgres pg_dump db_padel | gzip > "$DEST/db_padel_$STAMP.sql.gz"

# 2. Uploads, tokens e configs da aplicação (prod + dev)
tar -czf "$DEST/arquivos_$STAMP.tar.gz" -C /opt padelizou-shared

# 3. Configuração do SERVIDOR — fora de /opt, então fora do tar acima. Cada caminho só entra
# se existir: um drop-in do systemd (.d/) é opcional, e um backup diário não pode falhar
# porque um arquivo opcional não está lá.
CONFIG_DO_SERVIDOR=(
    /etc/caddy/Caddyfile
    /etc/systemd/system/padelizou.service
    /etc/systemd/system/padelizou.service.d
    /etc/systemd/system/padelizou-dev.service
    /etc/systemd/system/padelizou-dev.service.d
    /etc/cron.d/padelizou-backup
    /etc/cron.d/padelizou-backup-drive
    /root/.config/rclone/rclone.conf
)
PRESENTES=()
for caminho in "${CONFIG_DO_SERVIDOR[@]}"; do
    [ -e "$caminho" ] && PRESENTES+=("$caminho")
done
if [ "${#PRESENTES[@]}" -gt 0 ]; then
    tar -czf "$DEST/config-servidor_$STAMP.tar.gz" "${PRESENTES[@]}"
fi

# Mantém 14 dias de histórico
find "$DEST" -name 'db_padel_*.sql.gz' -mtime +14 -delete
find "$DEST" -name 'arquivos_*.tar.gz' -mtime +14 -delete
find "$DEST" -name 'config-servidor_*.tar.gz' -mtime +14 -delete
