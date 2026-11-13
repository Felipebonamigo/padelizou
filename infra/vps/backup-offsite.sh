#!/usr/bin/env bash
# Segunda copia do backup do Padelizou, FORA do servidor.
#
# Por que existe: o backup local (/var/backups/padelizou, feito pelo backup-padelizou.sh as 4h)
# mora no MESMO disco do banco. Se o VPS morrer, morrem os dois juntos.
#
# ⚠️ DOIS DESTINOS desde 07/08/2026, e a ordem importa:
#
#   1. BACKBLAZE B2  — PRINCIPAL. Acesso por chave de API que NAO EXPIRA. Se ele falhar, o
#      script falha, e o VigiaDoBackup avisa.
#   2. GOOGLE DRIVE  — reserva, "melhor esforco". Falha dele NAO derruba a rodada.
#
# O motivo dessa hierarquia esta escrito com sangue: o Drive usa token OAuth, e em 04/08/2026
# ele morreu sozinho — 7 dias depois de emitido, porque tinha nascido enquanto o app do Google
# ainda estava em modo Teste. Ficou 3 dias sem copia nenhuma fora do servidor. Com o B2 no
# comando, token vencido vira no maximo um aviso no log, nao um buraco no backup.
#
# Tudo sobe CRIPTOGRAFADO (remotes "cofre-b2" e "cofre-padelizou", os dois do tipo crypt e com
# A MESMA CHAVE — e por isso que deu pra copiar o historico de um pro outro sem decifrar). A
# chave esta em /root/padelizou-chave-backup.txt e no gerenciador de senhas do Felipe — SEM ELA
# O BACKUP E INUTIL, essa e a troca que a criptografia impoe.
#
# Roda pelo cron (/etc/cron.d/padelizou-backup-drive), 4h30 — meia hora depois do backup local,
# pra pegar o dump do dia ja pronto.
#
# ⚠️ O nome do arquivo ficou "backup-drive.sh" de propósito: o cron aponta pra ele e trocar o
# nome na véspera do lançamento seria criar um ponto de falha pra ganhar estética.
set -eo pipefail

RCLONE=/usr/local/bin/rclone
COFRE=cofre-b2                  # principal
COFRE_RESERVA=cofre-padelizou   # reserva (Google Drive)
LOCAL=/var/backups/padelizou
COMPARTILHADO=/opt/padelizou-shared/prod
DIAS_DE_HISTORICO=30

log() { echo "[$(date '+%d/%m %H:%M:%S')] $*"; }

# O DUMP DO DIA EXISTE E ESTÁ INTEIRO? (13/11/2026)
#
# O `rclone copy` lá embaixo passa LISO quando não há nada novo pra copiar — e era ele que
# liberava o carimbo. Se o pg_dump das 4h parasse (disco cheio, senha, versão do Postgres), o
# vigia via carimbo fresco todo dia e ficava calado, enquanto o `delete --min-age` ia apagando o
# histórico do B2 até não sobrar dump nenhum. Agora a rodada só segue com o dump MAIS NOVO da
# pasta sendo:
#   - de menos de $2 horas — o das 4h00, nunca o do meio-dia de ontem (12h30 antes desta rodada):
#     aceitar aquele esconderia um cron das 4h morto, o mesmo motivo de o backup do meio-dia não
#     gravar o carimbo;
#   - um gzip inteiro (`gzip -t`);
#   - COMPLETO: o pg_dump só escreve "PostgreSQL database dump complete" no fim. Dump que morreu
#     no meio ainda vira um .gz válido (o gzip fecha o arquivo direitinho), e só esta linha
#     separa um do outro.
dump_do_dia_ok() {
  local pasta="$1" horas="$2" arquivo
  arquivo=$(find "$pasta" -maxdepth 1 -type f -name 'db_padel_*.sql.gz' -printf '%T@ %p\n' 2>/dev/null \
    | sort -rn | head -n 1 | cut -d' ' -f2-)
  if [ -z "$arquivo" ]; then
    log "ERRO: nenhum dump db_padel_*.sql.gz em $pasta."
    return 1
  fi
  if [ -z "$(find "$arquivo" -mmin "-$(( horas * 60 ))")" ]; then
    log "ERRO: o dump mais novo ($(basename "$arquivo")) tem mais de ${horas}h — o backup das 4h rodou?"
    return 1
  fi
  if ! gzip -t "$arquivo" 2>/dev/null; then
    log "ERRO: o dump mais novo ($(basename "$arquivo")) está corrompido."
    return 1
  fi
  if ! gzip -dc "$arquivo" | tail -n 20 | grep -q 'PostgreSQL database dump complete'; then
    log "ERRO: o dump mais novo ($(basename "$arquivo")) está incompleto — o pg_dump morreu no meio."
    return 1
  fi
  log "Dump do dia conferido: $(basename "$arquivo")."
  return 0
}

# Manda o conteudo do dia pra UM destino. Recebe o remote; quem chama decide se a falha e fatal.
#
# O "mkdir" antes de tudo e o teste de acesso, e nao "lsd" de proposito: "lsd" tambem falha
# quando a pasta simplesmente ainda nao existe, e ai o script se declararia sem acesso pra
# sempre — um backup que nunca roda e nao reclama. "mkdir" cria se faltar (idempotente) e so
# falha quando o destino recusa de verdade.
copiar_para() {
  local cofre="$1"

  $RCLONE mkdir "$cofre:" >/dev/null 2>&1 || return 1

  # 1. Dumps do banco. Sao minusculos (dezenas de KB), entao vale guardar historico com folga.
  $RCLONE copy "$LOCAL" "$cofre:banco" \
    --include "db_padel_*.sql.gz" \
    --transfers 4 --retries 3 || return 1

  # 2. Arquivos de producao: fotos, logos, capas, tokens do Google, appsettings e as chaves de
  #    sessao. Espelho incremental — so sobe o que mudou, entao o custo diario e quase zero.
  #
  #    --backup-dir e o detalhe que importa: no "sync" puro, apagar uma foto aqui apagaria a
  #    copia la tambem, e o backup nao protegeria justamente contra apagar sem querer. Assim, o
  #    que sai do ar vai pra uma pasta datada em vez de sumir.
  $RCLONE sync "$COMPARTILHADO" "$cofre:arquivos-prod" \
    --backup-dir "$cofre:apagados/$(date +%Y-%m)" \
    --transfers 4 --retries 3 || return 1

  # 3. Historico: dump mais velho que o limite sai (o backup local ja guarda 14 dias).
  $RCLONE delete "$cofre:banco" --min-age "${DIAS_DE_HISTORICO}d" || true

  return 0
}

# ── 0. SEM DUMP BOM DO DIA, NADA ACONTECE ─────────────────────────────────────────────────
# Nem sobe, nem PODA o histórico do cofre, nem grava o carimbo: o vigia avisa em 2 dias, e o
# que já está no B2 fica intacto até alguém consertar o dump. (BackupEDeployGuardamOBancoTests)
if ! dump_do_dia_ok "$LOCAL" 6; then
  log "ERRO: sem dump bom do dia — nada subiu, o historico do cofre NAO foi podado e o carimbo NAO foi gravado."
  exit 1
fi

# ── 1. PRINCIPAL: Backblaze B2 ────────────────────────────────────────────────────────────
log "Iniciando copia pro Backblaze B2."

if ! copiar_para "$COFRE"; then
  log "ERRO: falhou a copia pro B2 — o carimbo NAO sera gravado e o vigia vai avisar."
  exit 1
fi

TAMANHO=$($RCLONE size "$COFRE:" --json 2>/dev/null | grep -o '"bytes":[0-9]*' | cut -d: -f2)
log "B2 pronto. Ocupando $(( ${TAMANHO:-0} / 1024 / 1024 )) MB."

# ── 2. RESERVA: Google Drive ──────────────────────────────────────────────────────────────
# Falha aqui NAO derruba a rodada: a copia que importa ja esta feita. Mas fica escrita no log,
# pra que "o Drive caiu de novo" seja um fato visivel e nao uma descoberta de tres dias depois.
if copiar_para "$COFRE_RESERVA"; then
  log "Drive (reserva) pronto."
else
  log "AVISO: o Drive nao aceitou a copia (token vencido?). O B2 ja guardou tudo."
  log "Pra reautorizar: rclone config reconnect padelizou-drive: (precisa de tunel SSH -L 53682)"
fi

# Carimbo de "deu certo", lido pelo VigiaDoBackup dentro do app: passando de 2 dias sem
# atualizar, ele avisa os administradores.
#
# Só chega aqui quem passou pelo destino PRINCIPAL, então a data significa cópia completa em
# lugar seguro — gravar no começo faria o carimbo mentir justamente quando o upload falhasse.
mkdir -p /var/lib/padelizou
date --iso-8601=seconds > /var/lib/padelizou/ultimo-backup-drive
chmod 644 /var/lib/padelizou/ultimo-backup-drive
