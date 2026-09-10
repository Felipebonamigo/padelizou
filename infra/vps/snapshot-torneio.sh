#!/usr/bin/env bash
# GUARDA UM TORNEIO INTEIRO NUM ARQUIVO — pra poder devolvê-lo depois com o restaurar-torneio.sh.
#
# 🗣️ Felipe, 10/09/2026: *"como esta nosso backup, como eu montei todo torneio do er, nao podemos
# perder essa chave de nenhum jeito"*. Desenho em SNAPSHOT-TORNEIO.md, aprovado antes do código.
#
# POR QUE EXISTE, se já há backup das 4h e das 16h: aqueles protegem contra o VPS morrer. Eles não
# desfazem um erro na chave do Er sem desfazer junto tudo o que entrou depois — inscrição,
# pagamento, placar de outro torneio. E não há caminho de volta pra dentro do prod: o
# `copiar-torneio.sh` recusa `--para db_padel`, de propósito. Esta é a peça do meio.
#
# QUANDO RODAR: antes de apertar "Refazer grade" ou "Desfazer sorteio" numa chave montada à mão.
#
# O ARQUIVO É UM SQL COMUM (gzipado): schema de trabalho + blocos COPY, exatamente o que o
# pg_dump emite. Se um dia este script quebrar, dá pra ler e restaurar na mão com psql — é o
# motivo de não ser formato próprio.
#
# ⚠️ E O `CREATE TABLE` VAI DENTRO DO ARQUIVO — então a forma da área de trabalho é decidida
# AQUI, na hora de guardar, e não na hora de restaurar. É o preço de o arquivo ser autossuficiente
# (é o que permite restaurá-lo na mão), e tem uma consequência real: melhoria feita neste script
# só vale pros snapshots tirados DEPOIS dela. Os de antes carregam o DDL de antes.
#
# ⚠️ SEM RETENÇÃO AUTOMÁTICA, de propósito. Os `find -mtime +14 -delete` do backup.sh não pegam
# este nome. Apagar sozinho o único registro de uma chave montada à mão é exatamente o risco que
# este script existe pra cobrir.
#
# Uso:
#   snapshot-torneio.sh ERPADEL                    # grava e sobe pro cofre
#   snapshot-torneio.sh ERPADEL --sem-cofre        # só local (sem rclone)
#   snapshot-torneio.sh ERPADEL --banco db_padel_dev --saida /tmp
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=torneio-tabelas.sh
source "$AQUI/torneio-tabelas.sh"

CODIGO="${1:?Uso: snapshot-torneio.sh <CODIGO-DO-TORNEIO> [--banco db] [--saida DIR] [--sem-cofre]}"
shift
BANCO="db_padel"
SAIDA="/var/backups/padelizou/torneios"
COFRE=cofre-b2
RCLONE=/usr/local/bin/rclone
SEM_COFRE=0

while [ $# -gt 0 ]; do
  case "$1" in
    --banco) BANCO="${2:?}"; shift 2 ;;
    --saida) SAIDA="${2:?}"; shift 2 ;;
    --sem-cofre) SEM_COFRE=1; shift ;;
    *) echo "ERRO: opção desconhecida '$1'"; exit 1 ;;
  esac
done

psql() { command psql -v ON_ERROR_STOP=1 -X -q -d "$BANCO" "$@"; }

# ── O torneio existe mesmo? ───────────────────────────────────────────────────────────────
CODIGO_SQL="${CODIGO//\'/\'\'}"
TORNEIO_ID=$(psql -At -c "SELECT \"Id\" FROM \"Torneio\" WHERE \"Codigo\" = '$CODIGO_SQL';")
if [ -z "$TORNEIO_ID" ]; then
  echo "ERRO: nenhum torneio com código '$CODIGO' em $BANCO."
  echo "Os que existem lá:"
  psql -c "SELECT \"Codigo\", \"Nome\", \"Status\" FROM \"Torneio\" ORDER BY \"Id\" DESC LIMIT 20;"
  exit 1
fi

mkdir -p "$SAIDA"
STAMP=$(date +%Y%m%d_%H%M%S)
ARQUIVO="$SAIDA/${CODIGO}_${STAMP}.sql.gz"
TMP=$(mktemp)
trap 'rm -f "$TMP"' EXIT

echo "── Snapshot de '$CODIGO' (Id $TORNEIO_ID) de $BANCO ──"

# ── O cabeçalho: quem é este arquivo ──────────────────────────────────────────────────────
{
  echo "-- padelizou-snapshot v1"
  echo "-- torneio: $CODIGO (Id $TORNEIO_ID)"
  echo "-- banco:   $BANCO"
  echo "-- em:      $(date --iso-8601=seconds)"
  echo "-- Restaurar: restaurar-torneio.sh <este arquivo> --grade|--chave"
  echo "DROP SCHEMA IF EXISTS snapshot CASCADE;"
  echo "CREATE SCHEMA snapshot;"
} > "$TMP"

# ── Uma tabela de cada vez: a forma dela, depois os dados ─────────────────────────────────
for t in $TABELAS_DO_TORNEIO; do
  # As colunas saem do catálogo, e não de uma lista escrita à mão — é o que faz este arquivo
  # sobreviver à próxima coluna nova, que aqui nasce toda semana. Coluna GERADA fica de fora:
  # o COPY recusa escrever nela, e o valor volta sozinho do mesmo jeito.
  COLS=$(psql -At -c "SELECT string_agg(quote_ident(column_name), ', ' ORDER BY ordinal_position)
                        FROM information_schema.columns
                       WHERE table_schema='public' AND table_name='$t' AND is_generated <> 'ALWAYS';")
  if [ -z "$COLS" ]; then
    echo "ERRO: a tabela \"$t\" não existe em $BANCO — o torneio-tabelas.sh está à frente do banco."
    exit 1
  fi

  # `INCLUDING DEFAULTS`, e aqui está o oposto da escolha do copiar-torneio.sh — de propósito.
  # Lá a área de trabalho guarda o valor CRU porque tudo é remapeado. Aqui é o MESMO banco, e o
  # que interessa é a coluna que uma migration criar ENTRE este snapshot e a restauração: o COPY
  # deste arquivo não vai mencioná-la (ela não existia), e sem default ela entraria NULA por cima
  # do valor que a migration prometeu. Com default, ela nasce certa.
  {
    echo ""
    echo "CREATE TABLE snapshot.\"s_$t\" (LIKE public.\"$t\" INCLUDING DEFAULTS);"
    echo "COPY snapshot.\"s_$t\" ($COLS) FROM stdin;"
  } >> "$TMP"

  psql -c "COPY ($(filtro_do_torneio "$t" "$TORNEIO_ID")) TO STDOUT" >> "$TMP"
  echo '\.' >> "$TMP"

  n=$(psql -At -c "SELECT count(*) FROM ($(filtro_do_torneio "$t" "$TORNEIO_ID")) x;")
  printf '  %-20s %s\n' "$t" "$n"
done

# ── Conferência: o arquivo tem mesmo o que o banco tem? ───────────────────────────────────
# Contar as linhas do COPY escrito é o que separa "o script não deu erro" de "o torneio coube
# inteiro no arquivo". Um pg_dump interrompido no meio produz arquivo plausível.
falhou=0
for t in $TABELAS_DO_TORNEIO; do
  no_banco=$(psql -At -c "SELECT count(*) FROM ($(filtro_do_torneio "$t" "$TORNEIO_ID")) x;")
  # As linhas entre o COPY desta tabela e o \. que o fecha.
  no_arquivo=$(awk -v tab="s_$t" '
    $0 ~ ("^COPY snapshot\\.\"" tab "\" ") { dentro=1; next }
    dentro && $0 == "\\."                  { dentro=0 }
    dentro                                 { n++ }
    END { print n+0 }' "$TMP")
  if [ "$no_banco" != "$no_arquivo" ]; then
    printf '  ✘ %-20s banco=%s arquivo=%s\n' "$t" "$no_banco" "$no_arquivo"; falhou=1
  fi
done
if [ "$falhou" = "1" ]; then
  echo "ERRO: o arquivo não bate com o banco — snapshot DESCARTADO."
  exit 1
fi

gzip -c "$TMP" > "$ARQUIVO"
chmod 600 "$ARQUIVO"   # leva nome, CPF e telefone de quem joga o torneio
echo "── Gravado: $ARQUIVO ($(du -h "$ARQUIVO" | cut -f1)) ──"

# ── O cofre ───────────────────────────────────────────────────────────────────────────────
# Sobe AGORA, e não na rodada das 4h30: o valor deste arquivo é "vou apertar Refazer grade
# agora". Esperar a madrugada perderia o ponto inteiro.
if [ "$SEM_COFRE" = "1" ]; then
  echo "  (--sem-cofre: não subiu pro B2)"
elif [ ! -x "$RCLONE" ]; then
  echo "  ⚠️  rclone não encontrado em $RCLONE — o snapshot está SÓ no disco do servidor."
elif $RCLONE copy "$ARQUIVO" "$COFRE:torneios" --retries 3; then
  echo "  ✔ no cofre B2 (torneios/$(basename "$ARQUIVO"))"
else
  # Não derruba o script: o arquivo local já existe, e é ele que desfaz o botão daqui a um
  # minuto. Mas tem que ser dito — snapshot que só existe no VPS não protege contra o VPS.
  echo "  ⚠️  NÃO subiu pro cofre. O snapshot está SÓ no disco do servidor."
fi
