#!/usr/bin/env bash
# COPIA UM TORNEIO INTEIRO DE UM BANCO PRO OUTRO — na prática, do `prod` pro `dev`.
#
# 🗣️ Felipe, 09/09/2026: *"copie os dados de PRD para DEV do torneio do ER"*. O motivo é o certo:
# ensaiar no `dev` o "Refazer grade" com a ordem de fases nova (Services/OrdemDasFases) sobre o
# torneio de verdade, antes de apertar o botão em produção — que é a Regra 3 do projeto.
#
# ⚠️ POR QUE NÃO É UM `pg_dump | psql`. Restaurar o dump inteiro do prod no dev resolveria em uma
# linha, e foi recusado de propósito: o `dev.padelizou.com.br` está ABERTO NA INTERNET, e o dump
# leva nome, CPF, e-mail, telefone e histórico de pagamento da base inteira pra lá. Copiar UM
# torneio leva as pessoas daquele torneio e mais ninguém.
#
# ⚠️ O QUE ELE COPIA: Torneio, Categoria, Quadra, QuadraDaCategoria, GrupoTorneio, Dupla, Partida,
# TorneioOrganizador, TorneioMarcador — e os Jogadores citados por eles.
#
# ⚠️ O QUE ELE NÃO COPIA, de propósito: Pagamento, PalpitePartida, VotoDeMvp, SeguidorTorneio,
# AvaliacaoDoTorneio, e tudo do Ranking RS. Dinheiro e voto não precisam existir no dev pra ensaiar
# uma grade, e cada tabela a mais é mais dado pessoal num host público.
#
# ── AS DUAS ARMADILHAS, E COMO ELE SAI DELAS ────────────────────────────────────────────
#
# 1. ID QUE COLIDE. Os dois bancos numeram do 1: o torneio 5 do prod não pode entrar como 5 no dev,
#    onde já existe outro. Cada tabela ganha um mapa `velho → novo` tirado da própria sequence do
#    destino, e as FKs são reescritas pelo mapa. Nada de "somar um milhão no Id", que quebra na
#    primeira base que passar do milhão.
#
# 2. CHAVE ÚNICA QUE COLIDE. `Jogador.CPF`, `Jogador.Login` e `Jogador.AgendaFeedToken` são ÚNICOS,
#    e o dev quase sempre já tem essas pessoas. Então jogador não é copiado às cegas: quem já
#    existe no destino (mesmo CPF) é REAPROVEITADO, e só quem falta é inserido. `Torneio.Codigo`
#    também é único — por isso o torneio de mesmo código no destino é APAGADO antes (é uma cópia,
#    não uma fusão).
#
# ⚠️ O `AgendaFeedToken` É REGERADO, nunca copiado. Ele é a chave que abre o feed de agenda daquela
# pessoa; duplicá-lo num segundo host é espalhar um segredo vivo sem necessidade.
#
# Uso:
#   copiar-torneio.sh AMIGOS26                      # de db_padel pra db_padel_dev
#   copiar-torneio.sh AMIGOS26 --de X --para Y
#   copiar-torneio.sh AMIGOS26 --conferir           # só mostra o que faria, não grava
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=torneio-tabelas.sh
source "$AQUI/torneio-tabelas.sh"

CODIGO="${1:?Uso: copiar-torneio.sh <CODIGO-DO-TORNEIO> [--de db] [--para db] [--conferir]}"
shift
DE="db_padel"
PARA="db_padel_dev"
CONFERIR=0

while [ $# -gt 0 ]; do
  case "$1" in
    --de)   DE="${2:?}"; shift 2 ;;
    --para) PARA="${2:?}"; shift 2 ;;
    --conferir) CONFERIR=1; shift ;;
    *) echo "ERRO: opção desconhecida '$1'"; exit 1 ;;
  esac
done

# ── AS TRAVAS ───────────────────────────────────────────────────────────────────────────
# Este script APAGA o torneio de mesmo código no destino. Escrever em produção por engano — um
# --para trocado, um copiar-e-colar — apagaria o torneio de verdade no meio do fim de semana.
if [ "$DE" = "$PARA" ]; then
  echo "ERRO: origem e destino são o mesmo banco ($DE)."; exit 1
fi
if [ "$PARA" = "db_padel" ]; then
  echo "ERRO: o destino é o banco de PRODUÇÃO. Este script apaga o torneio de mesmo código no"
  echo "      destino antes de escrever — ele existe pra levar dado PRA fora do prod, nunca pra dentro."
  exit 1
fi

psql() { command psql -v ON_ERROR_STOP=1 -X -q "$@"; }

# A lista de tabelas e o filtro de cada uma moram no torneio-tabelas.sh, compartilhados com o
# snapshot-torneio.sh. Duas cópias divergiriam na primeira tabela nova — e divergiram: a
# ReservaDeHorario nasceu em 10/09 e ficou de fora daqui até o dia seguinte.
filtro() { filtro_do_torneio "$1" "$TORNEIO_ID"; }
TABELAS="$TABELAS_DO_TORNEIO"
NA_ORIGEM()  { psql -d "$DE" "$@"; }
NO_DESTINO() { psql -d "$PARA" "$@"; }

# ── O torneio existe mesmo? ─────────────────────────────────────────────────────────────
TORNEIO_ID=$(NA_ORIGEM -At -c "SELECT \"Id\" FROM \"Torneio\" WHERE \"Codigo\" = '${CODIGO//\'/\'\'}';")
if [ -z "$TORNEIO_ID" ]; then
  echo "ERRO: nenhum torneio com código '$CODIGO' em $DE."
  echo "Os que existem lá:"
  NA_ORIGEM -c "SELECT \"Codigo\", \"Nome\", \"Status\" FROM \"Torneio\" ORDER BY \"Id\" DESC LIMIT 20;"
  exit 1
fi

echo "── Copiando '$CODIGO' (Id $TORNEIO_ID) de $DE para $PARA ──"

if [ "$CONFERIR" = "1" ]; then
  echo "── O que seria copiado (nada foi gravado) ──"
  for t in $TABELAS; do
    n=$(NA_ORIGEM -At -c "SELECT count(*) FROM ($(filtro "$t")) x;")
    printf '  %-20s %s\n' "$t" "$n"
  done
  ja=$(NO_DESTINO -At -c "SELECT count(*) FROM \"Torneio\" WHERE \"Codigo\" = '${CODIGO//\'/\'\'}';")
  [ "$ja" -gt 0 ] && echo "  ⚠️  O destino JÁ TEM um torneio '$CODIGO' — ele seria APAGADO e reescrito."
  exit 0
fi

# ── 1. A área de trabalho no destino ────────────────────────────────────────────────────
NO_DESTINO -c "DROP SCHEMA IF EXISTS copia CASCADE; CREATE SCHEMA copia;"
for t in $TABELAS; do
  # LIKE sem INCLUDING: a área de trabalho guarda o valor CRU da origem, sem default nem
  # constraint. É de propósito — quem valida é o INSERT final, na tabela de verdade.
  NO_DESTINO -c "CREATE TABLE copia.\"s_$t\" (LIKE public.\"$t\");"
done

# ── 2. A travessia ──────────────────────────────────────────────────────────────────────
for t in $TABELAS; do
  NA_ORIGEM -c "COPY ($(filtro "$t")) TO STDOUT" \
    | NO_DESTINO -c "COPY copia.\"s_$t\" FROM STDIN"
  n=$(NO_DESTINO -At -c "SELECT count(*) FROM copia.\"s_$t\";")
  printf '  trouxe %-20s %s\n' "$t" "$n"
done

# ── 3. O remapeamento e a gravação ──────────────────────────────────────────────────────
NO_DESTINO -f - <<'SQL'
-- Constrói o INSERT de uma tabela lendo as colunas do catálogo, e não de uma lista escrita à
-- mão. É o que faz este script sobreviver à próxima coluna nova — e este projeto ganha coluna
-- toda semana. `overrides` troca a expressão de colunas específicas (as FKs e o Id).
CREATE FUNCTION copia.inserir(tabela text, overrides jsonb) RETURNS bigint AS $fn$
DECLARE cols text; vals text; n bigint;
BEGIN
  SELECT string_agg(quote_ident(column_name), ', ' ORDER BY ordinal_position),
         string_agg(COALESCE(overrides ->> column_name, 's.' || quote_ident(column_name)),
                    ', ' ORDER BY ordinal_position)
    INTO cols, vals
  FROM information_schema.columns
  WHERE table_schema = 'public' AND table_name = tabela;

  EXECUTE format('INSERT INTO public.%I (%s) SELECT %s FROM copia.%I s', tabela, cols, vals, 's_' || tabela);
  GET DIAGNOSTICS n = ROW_COUNT;
  RETURN n;
END $fn$ LANGUAGE plpgsql;

-- ⚠️ TUDO NUMA TRANSAÇÃO SÓ. Uma cópia pela metade — torneio sem duplas, duplas sem partidas —
-- é pior que nenhuma: o app abre, mostra a chave torta, e ninguém sabe que faltou coisa.
BEGIN;

-- ── APAGA o torneio de mesmo código no destino ────────────────────────────────────────
-- `Torneio.Codigo` é único: sem isto o INSERT estoura. E é uma CÓPIA, não uma fusão — deixar
-- as duas versões conviverem daria dois torneios com o mesmo código pra quem olha a tela.
CREATE TEMP TABLE alvo AS
SELECT "Id" FROM public."Torneio"
WHERE "Codigo" = (SELECT "Codigo" FROM copia."s_Torneio");

DELETE FROM public."ReservaDeHorario"   WHERE "CategoriaId" IN (SELECT "Id" FROM public."Categoria" WHERE "TorneioId" IN (SELECT "Id" FROM alvo));
DELETE FROM public."Partida"            WHERE "TorneioId" IN (SELECT "Id" FROM alvo);
DELETE FROM public."Dupla"              WHERE "CategoriaId" IN (SELECT "Id" FROM public."Categoria" WHERE "TorneioId" IN (SELECT "Id" FROM alvo));
DELETE FROM public."GrupoTorneio"       WHERE "CategoriaId" IN (SELECT "Id" FROM public."Categoria" WHERE "TorneioId" IN (SELECT "Id" FROM alvo));
DELETE FROM public."QuadraDaCategoria"  WHERE "CategoriaId" IN (SELECT "Id" FROM public."Categoria" WHERE "TorneioId" IN (SELECT "Id" FROM alvo));
DELETE FROM public."Categoria"          WHERE "TorneioId" IN (SELECT "Id" FROM alvo);
DELETE FROM public."Quadra"             WHERE "TorneioId" IN (SELECT "Id" FROM alvo);
DELETE FROM public."TorneioOrganizador" WHERE "TorneioId" IN (SELECT "Id" FROM alvo);
DELETE FROM public."TorneioMarcador"    WHERE "TorneioId" IN (SELECT "Id" FROM alvo);
DELETE FROM public."Torneio"            WHERE "Id" IN (SELECT "Id" FROM alvo);

-- ── OS MAPAS velho → novo ─────────────────────────────────────────────────────────────

-- CLUBE: casa pelo NOME, que é como uma pessoa reconhece o lugar. Sem nome igual no destino, o
-- clube é copiado (com o dono desligado — dono é assunto do clube, não deste torneio).
CREATE TEMP TABLE map_clube AS
SELECT s."Id" AS velho,
       COALESCE((SELECT d."Id" FROM public."Clubes" d WHERE d."Nome" = s."Nome" ORDER BY d."Id" LIMIT 1),
                nextval(pg_get_serial_sequence('public."Clubes"', 'Id'))) AS novo,
       (SELECT d."Id" FROM public."Clubes" d WHERE d."Nome" = s."Nome" LIMIT 1) IS NOT NULL AS ja_existia
FROM copia."s_Clubes" s;

-- JOGADOR: casa pelo CPF, que é a identidade da pessoa e é ÚNICO nas duas pontas. Quem o destino
-- já conhece é reaproveitado — inserir de novo estouraria a unicidade, e criaria a mesma pessoa
-- duas vezes. CPF nulo não casa com ninguém (em Postgres NULL nunca é igual a NULL, e é o certo
-- aqui: sem CPF não dá pra afirmar que são a mesma pessoa).
CREATE TEMP TABLE map_jogador AS
SELECT s."Id" AS velho,
       COALESCE((SELECT d."Id" FROM public."Jogador" d WHERE d."CPF" = s."CPF" AND s."CPF" IS NOT NULL LIMIT 1),
                nextval(pg_get_serial_sequence('public."Jogador"', 'Id'))) AS novo,
       (SELECT d."Id" FROM public."Jogador" d WHERE d."CPF" = s."CPF" AND s."CPF" IS NOT NULL LIMIT 1) IS NOT NULL AS ja_existia
FROM copia."s_Jogador" s;

-- As demais são do torneio e nunca existem no destino: Id novo pra todas, tirado da sequence.
CREATE TEMP TABLE map_torneio      AS SELECT "Id" AS velho, nextval(pg_get_serial_sequence('public."Torneio"','Id'))      AS novo FROM copia."s_Torneio";
CREATE TEMP TABLE map_categoria    AS SELECT "Id" AS velho, nextval(pg_get_serial_sequence('public."Categoria"','Id'))    AS novo FROM copia."s_Categoria";
CREATE TEMP TABLE map_quadra       AS SELECT "Id" AS velho, nextval(pg_get_serial_sequence('public."Quadra"','Id'))       AS novo FROM copia."s_Quadra";
CREATE TEMP TABLE map_grupo        AS SELECT "Id" AS velho, nextval(pg_get_serial_sequence('public."GrupoTorneio"','Id')) AS novo FROM copia."s_GrupoTorneio";
CREATE TEMP TABLE map_dupla        AS SELECT "Id" AS velho, nextval(pg_get_serial_sequence('public."Dupla"','Id'))        AS novo FROM copia."s_Dupla";
CREATE TEMP TABLE map_partida      AS SELECT "Id" AS velho, nextval(pg_get_serial_sequence('public."Partida"','Id'))      AS novo FROM copia."s_Partida";

-- ── A GRAVAÇÃO, na ordem em que as FKs exigem ─────────────────────────────────────────

-- Só os clubes que o destino ainda não tinha.
DELETE FROM copia."s_Clubes" s USING map_clube m WHERE m.velho = s."Id" AND m.ja_existia;
SELECT copia.inserir('Clubes', '{
  "Id": "(SELECT novo FROM map_clube m WHERE m.velho = s.\"Id\")",
  "DonoId": "NULL"
}');

-- Só os jogadores que o destino ainda não tinha.
-- ⚠️ O `AgendaFeedToken` é REGERADO: é a chave do feed de agenda daquela pessoa, e copiá-la
-- espalharia um segredo vivo pro segundo host sem nenhum ganho.
DELETE FROM copia."s_Jogador" s USING map_jogador m WHERE m.velho = s."Id" AND m.ja_existia;
SELECT copia.inserir('Jogador', '{
  "Id": "(SELECT novo FROM map_jogador m WHERE m.velho = s.\"Id\")",
  "AgendaFeedToken": "gen_random_uuid()"
}');

-- ⚠️ `TorneioOrigemId` sai NULO: ele aponta pro torneio de que este foi duplicado, e esse torneio
-- não veio junto. Apontar pro Id velho seria apontar pra outra coisa no banco de destino.
SELECT copia.inserir('Torneio', '{
  "Id": "(SELECT novo FROM map_torneio m WHERE m.velho = s.\"Id\")",
  "ClubeId": "(SELECT novo FROM map_clube m WHERE m.velho = s.\"ClubeId\")",
  "TorneioOrigemId": "NULL"
}');

SELECT copia.inserir('Categoria', '{
  "Id": "(SELECT novo FROM map_categoria m WHERE m.velho = s.\"Id\")",
  "TorneioId": "(SELECT novo FROM map_torneio m WHERE m.velho = s.\"TorneioId\")",
  "ClubeId": "(SELECT novo FROM map_clube m WHERE m.velho = s.\"ClubeId\")"
}');

SELECT copia.inserir('Quadra', '{
  "Id": "(SELECT novo FROM map_quadra m WHERE m.velho = s.\"Id\")",
  "TorneioId": "(SELECT novo FROM map_torneio m WHERE m.velho = s.\"TorneioId\")",
  "ClubeId": "(SELECT novo FROM map_clube m WHERE m.velho = s.\"ClubeId\")"
}');

SELECT copia.inserir('GrupoTorneio', '{
  "Id": "(SELECT novo FROM map_grupo m WHERE m.velho = s.\"Id\")",
  "CategoriaId": "(SELECT novo FROM map_categoria m WHERE m.velho = s.\"CategoriaId\")"
}');

SELECT copia.inserir('QuadraDaCategoria', '{
  "CategoriaId": "(SELECT novo FROM map_categoria m WHERE m.velho = s.\"CategoriaId\")",
  "QuadraId": "(SELECT novo FROM map_quadra m WHERE m.velho = s.\"QuadraId\")"
}');

-- ⚠️ `TimeId` sai NULO: time é de outra feature (Models/TimeSede) e não veio nesta cópia.
SELECT copia.inserir('Dupla', '{
  "Id": "(SELECT novo FROM map_dupla m WHERE m.velho = s.\"Id\")",
  "CategoriaId": "(SELECT novo FROM map_categoria m WHERE m.velho = s.\"CategoriaId\")",
  "GrupoTorneioId": "(SELECT novo FROM map_grupo m WHERE m.velho = s.\"GrupoTorneioId\")",
  "Jogador1Id": "(SELECT novo FROM map_jogador m WHERE m.velho = s.\"Jogador1Id\")",
  "Jogador2Id": "(SELECT novo FROM map_jogador m WHERE m.velho = s.\"Jogador2Id\")",
  "TimeId": "NULL"
}');

SELECT copia.inserir('Partida', '{
  "Id": "(SELECT novo FROM map_partida m WHERE m.velho = s.\"Id\")",
  "TorneioId": "(SELECT novo FROM map_torneio m WHERE m.velho = s.\"TorneioId\")",
  "CategoriaId": "(SELECT novo FROM map_categoria m WHERE m.velho = s.\"CategoriaId\")",
  "Dupla1Id": "(SELECT novo FROM map_dupla m WHERE m.velho = s.\"Dupla1Id\")",
  "Dupla2Id": "(SELECT novo FROM map_dupla m WHERE m.velho = s.\"Dupla2Id\")"
}');

SELECT copia.inserir('TorneioOrganizador', '{
  "TorneioId": "(SELECT novo FROM map_torneio m WHERE m.velho = s.\"TorneioId\")",
  "JogadorId": "(SELECT novo FROM map_jogador m WHERE m.velho = s.\"JogadorId\")"
}');

SELECT copia.inserir('TorneioMarcador', '{
  "TorneioId": "(SELECT novo FROM map_torneio m WHERE m.velho = s.\"TorneioId\")",
  "JogadorId": "(SELECT novo FROM map_jogador m WHERE m.velho = s.\"JogadorId\")"
}');

-- A reserva do horário da eliminatória prevista (Models/ReservaDeHorario, 10/09/2026). Sem ela o
-- ensaio no dev sai sem as trocas de horário das finais — que é justamente o que se quer ensaiar.
SELECT copia.inserir('ReservaDeHorario', '{
  "CategoriaId": "(SELECT novo FROM map_categoria m WHERE m.velho = s.\"CategoriaId\")"
}');

COMMIT;
SQL

# ── 4. A conferência ────────────────────────────────────────────────────────────────────
# Contar as duas pontas é o que separa "o script não deu erro" de "o torneio chegou inteiro".
echo "── Conferência ──"
NOVO_ID=$(NO_DESTINO -At -c "SELECT \"Id\" FROM \"Torneio\" WHERE \"Codigo\" = '${CODIGO//\'/\'\'}';")
falhou=0
for t in $TABELAS; do
  case "$t" in Clubes|Jogador|Torneio) continue ;; esac   # não são "do" torneio: são citados por ele
  na_origem=$(NA_ORIGEM -At -c "SELECT count(*) FROM ($(filtro "$t")) x;")
  no_destino=$(NO_DESTINO -At -c "$(contagem_do_torneio "$t" "$NOVO_ID")")
  if [ "$na_origem" = "$no_destino" ]; then
    printf '  ✔ %-20s %s\n' "$t" "$no_destino"
  else
    printf '  ✘ %-20s origem=%s destino=%s\n' "$t" "$na_origem" "$no_destino"; falhou=1
  fi
done

NO_DESTINO -c "DROP SCHEMA IF EXISTS copia CASCADE;"

if [ "$falhou" = "1" ]; then
  echo "ERRO: a contagem não bateu — o torneio chegou incompleto."; exit 1
fi
echo "── Pronto: '$CODIGO' está em $PARA com o Id $NOVO_ID ──"
