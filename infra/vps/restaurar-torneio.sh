#!/usr/bin/env bash
# DEVOLVE UM TORNEIO guardado pelo snapshot-torneio.sh — sem mexer no resto do banco.
#
# Desenho em SNAPSHOT-TORNEIO.md, aprovado antes do código. Leia-o antes de mexer aqui: as
# escolhas deste script saem do que os DOIS BOTÕES fazem de verdade, e não do que parecem fazer.
#
#   Refazer grade    → zera Partida.HorarioPrevisto e Partida.NomeQuadra do que está "Agendada",
#                      reencaixa, e apaga as ReservaDeHorario. NÃO apaga jogo: os Ids ficam.
#   Desfazer sorteio → apaga Partida e GrupoTorneio, solta Dupla.GrupoTorneioId, apaga as
#                      reservas, volta o Status e mexe no fiado.
#
# Daí as DUAS PORTAS:
#
#   --grade   desfaz o Refazer grade. Escreve HorarioPrevisto, NomeQuadra e ClubeId por Id, e
#             repõe as reservas. NENHUM insert, NENHUM delete de jogo — só UPDATE.
#   --chave   desfaz o Desfazer sorteio. Reinsere Partida e GrupoTorneio com os Ids ORIGINAIS,
#             repõe Dupla.GrupoTorneioId, as reservas e o Status.
#
# ── O QUE ELE NUNCA TOCA, e o porquê ─────────────────────────────────────────────────────
#
#   Pagamento .................. dinheiro. Nem lido, nem escrito, nem contado.
#   Torneio.TaxaExternoAdiadaEm  o carimbo do fiado é DÍVIDA. O --chave devolve o Status, nunca
#                                o carimbo: ele mostra o valor de antes e o de agora e manda
#                                resolver na tela do financeiro. Script não restaura dívida.
#   Dupla como LINHA ........... dupla inscrita depois do snapshot é inscrição PAGA; apagá-la é
#                                perder dinheiro. Só o GrupoTorneioId é escrito.
#   Jogador, Clubes ............ mesmo banco, mesmos Ids. Não há o que remapear.
#   Palpite, voto, seguidor,     apontam pra dentro do torneio e não são a chave. Como --grade só
#   avaliação, mural, ranking .. faz UPDATE e --chave só reinsere Id que o app já tinha apagado,
#                                nenhum deles é tocado — e a trava da FK abaixo garante isso
#                                contra a próxima tabela nova.
#
# Uso:
#   restaurar-torneio.sh ARQUIVO.sql.gz --grade              # só mostra o que faria
#   restaurar-torneio.sh ARQUIVO.sql.gz --grade --aplicar    # grava
#   restaurar-torneio.sh ARQUIVO.sql.gz --chave --aplicar
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=torneio-tabelas.sh
source "$AQUI/torneio-tabelas.sh"

ARQUIVO="${1:?Uso: restaurar-torneio.sh <ARQUIVO.sql.gz> --grade|--chave [--aplicar] [--banco db]}"
shift
BANCO="db_padel"
MODO=""
APLICAR=0

while [ $# -gt 0 ]; do
  case "$1" in
    --grade) MODO="grade"; shift ;;
    --chave) MODO="chave"; shift ;;
    --aplicar) APLICAR=1; shift ;;
    --banco) BANCO="${2:?}"; shift 2 ;;
    *) echo "ERRO: opção desconhecida '$1'"; exit 1 ;;
  esac
done

[ -f "$ARQUIVO" ] || { echo "ERRO: arquivo '$ARQUIVO' não existe."; exit 1; }
[ -n "$MODO" ] || { echo "ERRO: escolha --grade (desfaz o Refazer grade) ou --chave (desfaz o Desfazer sorteio)."; exit 1; }

psql() { command psql -v ON_ERROR_STOP=1 -X -q -d "$BANCO" "$@"; }
limpar() { command psql -X -q -d "$BANCO" -c "DROP SCHEMA IF EXISTS snapshot CASCADE;" >/dev/null 2>&1 || true; }
trap limpar EXIT

# ── 1. O arquivo entra num schema de trabalho ────────────────────────────────────────────
echo "── Lendo $ARQUIVO ──"
zcat -f "$ARQUIVO" | psql -v ON_ERROR_STOP=1 -c "SET client_min_messages = warning;" -f - >/dev/null

# ── 2. AS TRAVAS ─────────────────────────────────────────────────────────────────────────
QUANTOS=$(psql -At -c 'SELECT count(*) FROM snapshot."s_Torneio";')
[ "$QUANTOS" = "1" ] || { echo "ERRO: o snapshot tem $QUANTOS torneios (esperado 1) — arquivo corrompido."; exit 1; }

SNAP_ID=$(psql -At -c 'SELECT "Id" FROM snapshot."s_Torneio";')
SNAP_CODIGO=$(psql -At -c 'SELECT "Codigo" FROM snapshot."s_Torneio";')

# Mesmo torneio? Compara Id E código: só o Id casaria com outro torneio num banco restaurado do
# zero, e só o código casaria com um torneio recriado. Os dois juntos são a identidade.
AGORA_CODIGO=$(psql -At -c "SELECT \"Codigo\" FROM \"Torneio\" WHERE \"Id\" = $SNAP_ID;")
if [ -z "$AGORA_CODIGO" ]; then
  echo "ERRO: não existe torneio com Id $SNAP_ID em $BANCO. Este snapshot não é deste banco."
  exit 1
fi
if [ "$AGORA_CODIGO" != "$SNAP_CODIGO" ]; then
  echo "ERRO: o Id $SNAP_ID em $BANCO é o torneio '$AGORA_CODIGO', e o snapshot é de '$SNAP_CODIGO'."
  exit 1
fi

SNAP_EM=$(zcat -f "$ARQUIVO" | sed -n 's/^-- em: *//p' | head -1)
echo "── Torneio '$SNAP_CODIGO' (Id $SNAP_ID) · snapshot de ${SNAP_EM:-data desconhecida} ──"
echo "── Modo: --$MODO · $([ "$APLICAR" = 1 ] && echo 'VAI GRAVAR' || echo 'só conferindo, nada será gravado') ──"

# ── 3. A TRAVA DA FK: quem aponta pro que eu ia apagar? ──────────────────────────────────
# Pergunta ao catálogo, e não a uma lista escrita à mão: este projeto ganha tabela toda semana, e
# uma lista desatualizada é como se perde dado — a FK em cascata apaga calada.
#
# `Dupla.GrupoTorneioId` é o único par que este script SABE tratar (solta a dupla antes de apagar
# o grupo, como o próprio DesfazerSorteio faz). Qualquer outro que tenha linha apontando pra
# dentro faz o script recusar, dizendo o nome.
trava_de_fk() {
  local tabela_alvo="$1" ids_sql="$2"
  local pares
  pares=$(psql -At -F'|' -c "
    SELECT c.conrelid::regclass::text, a.attname
      FROM pg_constraint c
      JOIN LATERAL unnest(c.conkey) k(attnum) ON true
      JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum
     WHERE c.contype = 'f' AND c.confrelid = 'public.\"$tabela_alvo\"'::regclass;")

  local achou=0
  while IFS='|' read -r tabela coluna; do
    [ -n "$tabela" ] || continue
    [ "$tabela|$coluna" = "\"Dupla\"|GrupoTorneioId" ] && continue
    [ "$tabela|$coluna" = "Dupla|GrupoTorneioId" ] && continue
    local n
    n=$(psql -At -c "SELECT count(*) FROM $tabela WHERE $(printf '%s' "\"$coluna\"") IN ($ids_sql);")
    if [ "${n:-0}" -gt 0 ]; then
      echo "  ✘ $n linha(s) em $tabela.$coluna apontam pros $tabela_alvo que eu ia apagar."
      achou=1
    fi
  done <<< "$pares"
  return $achou
}

# ── 4. O QUE MUDA ────────────────────────────────────────────────────────────────────────
if [ "$MODO" = "grade" ]; then
  # Jogo do snapshot que não existe mais = houve Desfazer sorteio, e --grade não recria jogo.
  SUMIDOS=$(psql -At -c 'SELECT count(*) FROM snapshot."s_Partida" s
                          WHERE NOT EXISTS (SELECT 1 FROM public."Partida" p WHERE p."Id" = s."Id");')
  if [ "$SUMIDOS" -gt 0 ]; then
    echo "ERRO: $SUMIDOS jogos do snapshot não existem mais no banco — isto foi um Desfazer sorteio."
    echo "      O --grade só remarca jogo que existe. Use --chave."
    exit 1
  fi

  # A diferença, pelo TRIO que descreve o slot (10/09/2026: o clube é do horário, não do jogo).
  N_DIF=$(psql -At -c '
    SELECT count(*) FROM public."Partida" p JOIN snapshot."s_Partida" s ON s."Id" = p."Id"
     WHERE p."HorarioPrevisto" IS DISTINCT FROM s."HorarioPrevisto"
        OR p."NomeQuadra"      IS DISTINCT FROM s."NomeQuadra"
        OR p."ClubeId"         IS DISTINCT FROM s."ClubeId";')

  # Placar de jogo já jogado não volta pro horário antigo — é a mesma régua que o próprio
  # Refazer grade usa ("os já jogados ou em quadra não mudaram").
  N_JOGADOS=$(psql -At -c '
    SELECT count(*) FROM public."Partida" p JOIN snapshot."s_Partida" s ON s."Id" = p."Id"
     WHERE p."Status" <> '"'"'Agendada'"'"'
       AND (p."HorarioPrevisto" IS DISTINCT FROM s."HorarioPrevisto"
         OR p."NomeQuadra"      IS DISTINCT FROM s."NomeQuadra"
         OR p."ClubeId"         IS DISTINCT FROM s."ClubeId");')
  if [ "$N_JOGADOS" -gt 0 ]; then
    echo "ERRO: $N_JOGADOS jogo(s) já em quadra ou finalizados mudariam de horário. Recuso."
    psql -c 'SELECT p."Codigo", p."Status", p."HorarioPrevisto" AS agora, s."HorarioPrevisto" AS no_snapshot
               FROM public."Partida" p JOIN snapshot."s_Partida" s ON s."Id" = p."Id"
              WHERE p."Status" <> '"'"'Agendada'"'"'
                AND (p."HorarioPrevisto" IS DISTINCT FROM s."HorarioPrevisto"
                  OR p."NomeQuadra" IS DISTINCT FROM s."NomeQuadra"
                  OR p."ClubeId" IS DISTINCT FROM s."ClubeId");'
    exit 1
  fi

  N_RES_AGORA=$(psql -At -c "$(contagem_do_torneio ReservaDeHorario "$SNAP_ID")")
  N_RES_SNAP=$(psql -At -c 'SELECT count(*) FROM snapshot."s_ReservaDeHorario";')

  echo "  jogos que voltam de horário/quadra: $N_DIF"
  echo "  reservas: $N_RES_AGORA agora → $N_RES_SNAP do snapshot"
  if [ "$N_DIF" -gt 0 ]; then
    psql -c 'SELECT p."Codigo", p."Fase",
                    to_char(p."HorarioPrevisto", '"'"'DD/MM HH24:MI'"'"') AS agora,
                    to_char(s."HorarioPrevisto", '"'"'DD/MM HH24:MI'"'"') AS volta_para,
                    p."NomeQuadra" AS quadra_agora, s."NomeQuadra" AS quadra_volta
               FROM public."Partida" p JOIN snapshot."s_Partida" s ON s."Id" = p."Id"
              WHERE p."HorarioPrevisto" IS DISTINCT FROM s."HorarioPrevisto"
                 OR p."NomeQuadra" IS DISTINCT FROM s."NomeQuadra"
                 OR p."ClubeId" IS DISTINCT FROM s."ClubeId"
              ORDER BY s."HorarioPrevisto", p."Codigo" LIMIT 60;'
  fi

else
  # --chave: o que existe hoje sai, o do snapshot entra.
  IDS_PARTIDA="SELECT \"Id\" FROM public.\"Partida\" WHERE \"TorneioId\" = $SNAP_ID"
  IDS_GRUPO="SELECT g.\"Id\" FROM public.\"GrupoTorneio\" g JOIN public.\"Categoria\" c ON c.\"Id\" = g.\"CategoriaId\" WHERE c.\"TorneioId\" = $SNAP_ID"

  echo "── Trava das FKs ──"
  ok=1
  trava_de_fk Partida "$IDS_PARTIDA" || ok=0
  trava_de_fk GrupoTorneio "$IDS_GRUPO" || ok=0
  if [ "$ok" = "0" ]; then
    echo "ERRO: há dado pendurado no que eu ia apagar. Recuso — apagar levaria ele junto, calado."
    exit 1
  fi
  echo "  ✔ nada pendurado"

  # Jogo do snapshot cita dupla que não existe mais? A FK estouraria no meio da transação; é
  # melhor dizer QUAIS antes de tentar.
  DUPLAS_SUMIDAS=$(psql -At -c '
    SELECT count(*) FROM (
      SELECT "Dupla1Id" AS d FROM snapshot."s_Partida"
      UNION SELECT "Dupla2Id" FROM snapshot."s_Partida") x
     WHERE d IS NOT NULL AND NOT EXISTS (SELECT 1 FROM public."Dupla" p WHERE p."Id" = x.d);')
  if [ "$DUPLAS_SUMIDAS" -gt 0 ]; then
    echo "ERRO: $DUPLAS_SUMIDAS dupla(s) citadas pelos jogos do snapshot não existem mais."
    echo "      Alguém desinscreveu depois do snapshot. Restaurar a chave recriaria jogo pra"
    echo "      quem saiu do torneio — recuso."
    exit 1
  fi

  N_P_AGORA=$(psql -At -c "$(contagem_do_torneio Partida "$SNAP_ID")")
  N_P_SNAP=$(psql -At -c 'SELECT count(*) FROM snapshot."s_Partida";')
  N_G_AGORA=$(psql -At -c "$(contagem_do_torneio GrupoTorneio "$SNAP_ID")")
  N_G_SNAP=$(psql -At -c 'SELECT count(*) FROM snapshot."s_GrupoTorneio";')
  N_RES_SNAP=$(psql -At -c 'SELECT count(*) FROM snapshot."s_ReservaDeHorario";')
  ST_AGORA=$(psql -At -c "SELECT \"Status\" FROM \"Torneio\" WHERE \"Id\" = $SNAP_ID;")
  ST_SNAP=$(psql -At -c 'SELECT "Status" FROM snapshot."s_Torneio";')

  echo "  jogos:    $N_P_AGORA agora → $N_P_SNAP do snapshot"
  echo "  grupos:   $N_G_AGORA agora → $N_G_SNAP do snapshot"
  echo "  reservas: → $N_RES_SNAP do snapshot"
  echo "  status:   '$ST_AGORA' → '$ST_SNAP'"

  # A dupla inscrita DEPOIS do snapshot não estava na chave guardada — ela fica sem grupo, e o
  # organizador precisa saber disso antes, não descobrir na tela. A linha da dupla continua lá:
  # inscrição não se apaga por aqui (ver o cabeçalho).
  NOVAS=$(psql -At -c "
    SELECT count(*) FROM public.\"Dupla\" d
      JOIN public.\"Categoria\" c ON c.\"Id\" = d.\"CategoriaId\"
     WHERE c.\"TorneioId\" = $SNAP_ID
       AND NOT EXISTS (SELECT 1 FROM snapshot.\"s_Dupla\" s WHERE s.\"Id\" = d.\"Id\");")
  if [ "${NOVAS:-0}" -gt 0 ]; then
    echo ""
    echo "  ⚠️  $NOVAS dupla(s) se inscreveram DEPOIS deste snapshot e não estão na chave guardada."
    echo "      Elas FICAM SEM GRUPO (a inscrição continua lá, intacta). Sortear de novo ou pôr"
    echo "      no grupo à mão é decisão sua — este script não inventa confronto."
    psql -c "SELECT d.\"Id\", d.\"Codigo\", c.\"Nome\" AS categoria
               FROM public.\"Dupla\" d JOIN public.\"Categoria\" c ON c.\"Id\" = d.\"CategoriaId\"
              WHERE c.\"TorneioId\" = $SNAP_ID
                AND NOT EXISTS (SELECT 1 FROM snapshot.\"s_Dupla\" s WHERE s.\"Id\" = d.\"Id\");"
  fi

  # ⚠️ DINHEIRO: o carimbo do fiado é dívida, e este script não a restaura. Só CONTA.
  FIADO_AGORA=$(psql -At -c "SELECT COALESCE(\"TaxaExternoAdiadaEm\"::text, '(vazio)') FROM \"Torneio\" WHERE \"Id\" = $SNAP_ID;")
  FIADO_SNAP=$(psql -At -c $'SELECT COALESCE("TaxaExternoAdiadaEm"::text, \'(vazio)\') FROM snapshot."s_Torneio";')
  if [ "$FIADO_AGORA" != "$FIADO_SNAP" ]; then
    echo ""
    echo "  ⚠️  DINHEIRO — e eu NÃO vou mexer nisto:"
    echo "      Torneio.TaxaExternoAdiadaEm (o fiado da taxa do torneio por fora)"
    echo "        agora:       $FIADO_AGORA"
    echo "        no snapshot: $FIADO_SNAP"
    echo "      Desfazer o sorteio devolve a dívida (TorneiosController.Chaves.cs). Restaurar a"
    echo "      chave por script NÃO a retoma: resolva na tela do financeiro, olhando o valor."
  fi
fi

if [ "$APLICAR" != "1" ]; then
  echo ""
  echo "── Nada foi gravado. Rode de novo com --aplicar pra valer. ──"
  exit 0
fi

# ── 5. UM SNAPSHOT DE AGORA, ANTES DE ESCREVER ───────────────────────────────────────────
# A primeira coisa que se quer quando a restauração sai errada é desfazê-la.
echo ""
echo "── Guardando o estado de agora antes de escrever ──"
"$AQUI/snapshot-torneio.sh" "$SNAP_CODIGO" --banco "$BANCO" --saida "$(dirname "$ARQUIVO")" \
  | sed 's/^/  /'

# ── 6. A GRAVAÇÃO — uma transação só ─────────────────────────────────────────────────────
# Restauração pela metade (jogos com horário novo e reservas velhas) é pior que nenhuma: a tela
# abre, a grade parece certa, e ninguém sabe que faltou.
echo ""
echo "── Gravando ──"

if [ "$MODO" = "grade" ]; then
  psql <<'SQL'
BEGIN;

UPDATE public."Partida" p
   SET "HorarioPrevisto" = s."HorarioPrevisto",
       "NomeQuadra"      = s."NomeQuadra",
       "ClubeId"         = s."ClubeId"
  FROM snapshot."s_Partida" s
 WHERE s."Id" = p."Id"
   AND p."Status" = 'Agendada'
   AND (p."HorarioPrevisto" IS DISTINCT FROM s."HorarioPrevisto"
     OR p."NomeQuadra"      IS DISTINCT FROM s."NomeQuadra"
     OR p."ClubeId"         IS DISTINCT FROM s."ClubeId");

-- As reservas voltam inteiras: são poucas, e "apaga e põe de volta" não tem meio-termo pra dar
-- errado. A PK composta (CategoriaId, Fase, Numero) recusaria duplicata de qualquer forma.
DELETE FROM public."ReservaDeHorario" r
 USING public."Categoria" c
 WHERE c."Id" = r."CategoriaId"
   AND c."TorneioId" = (SELECT "Id" FROM snapshot."s_Torneio");

INSERT INTO public."ReservaDeHorario" SELECT * FROM snapshot."s_ReservaDeHorario";

COMMIT;
SQL

else
  psql <<'SQL'
BEGIN;

-- Solta as duplas dos grupos ANTES de apagar os grupos — a FK não deixa apagar GrupoTorneio com
-- Dupla ainda apontando. É o mesmo passo, na mesma ordem, que o DesfazerSorteio faz.
UPDATE public."Dupla" d
   SET "GrupoTorneioId" = NULL
  FROM public."Categoria" c
 WHERE c."Id" = d."CategoriaId"
   AND c."TorneioId" = (SELECT "Id" FROM snapshot."s_Torneio");

DELETE FROM public."ReservaDeHorario" r
 USING public."Categoria" c
 WHERE c."Id" = r."CategoriaId"
   AND c."TorneioId" = (SELECT "Id" FROM snapshot."s_Torneio");

DELETE FROM public."Partida" WHERE "TorneioId" = (SELECT "Id" FROM snapshot."s_Torneio");

DELETE FROM public."GrupoTorneio" g
 USING public."Categoria" c
 WHERE c."Id" = g."CategoriaId"
   AND c."TorneioId" = (SELECT "Id" FROM snapshot."s_Torneio");

-- Mesmo banco, mesmos Ids: não há mapa velho→novo pra construir. É toda a diferença entre este
-- script e o copiar-torneio.sh, e é o que o torna curto.
INSERT INTO public."GrupoTorneio"     SELECT * FROM snapshot."s_GrupoTorneio";
INSERT INTO public."Partida"          SELECT * FROM snapshot."s_Partida";
INSERT INTO public."ReservaDeHorario" SELECT * FROM snapshot."s_ReservaDeHorario";

-- A dupla volta pro grupo em que estava. Só o campo do grupo: a linha da dupla é inscrição, e
-- inscrição não se restaura por aqui (ver o cabeçalho).
UPDATE public."Dupla" d
   SET "GrupoTorneioId" = s."GrupoTorneioId",
       "Grupo"          = s."Grupo"
  FROM snapshot."s_Dupla" s
 WHERE s."Id" = d."Id";

-- O Status volta; o carimbo do fiado NÃO (dinheiro — ver o aviso na conferência).
UPDATE public."Torneio" t
   SET "Status" = s."Status"
  FROM snapshot."s_Torneio" s
 WHERE s."Id" = t."Id";

-- As sequences precisam ficar À FRENTE do maior Id que acabou de entrar: sem isto o próximo
-- sorteio tentaria gravar num Id que já existe e estouraria a PK.
DO $$ BEGIN
  PERFORM setval(pg_get_serial_sequence('public."Partida"','Id'),
                 GREATEST((SELECT COALESCE(max("Id"),1) FROM public."Partida"), 1));
  PERFORM setval(pg_get_serial_sequence('public."GrupoTorneio"','Id'),
                 GREATEST((SELECT COALESCE(max("Id"),1) FROM public."GrupoTorneio"), 1));
END $$;

COMMIT;
SQL
fi

# ── 7. A CONFERÊNCIA ─────────────────────────────────────────────────────────────────────
# Contar as duas pontas é o que separa "o script não deu erro" de "o torneio voltou inteiro".
echo ""
echo "── Conferência ──"
falhou=0
confere() {
  local rotulo="$1" esperado="$2" obtido="$3"
  if [ "$esperado" = "$obtido" ]; then printf '  ✔ %-34s %s\n' "$rotulo" "$obtido"
  else printf '  ✘ %-34s esperado=%s obtido=%s\n' "$rotulo" "$esperado" "$obtido"; falhou=1; fi
}

RESTA=$(psql -At -c '
  SELECT count(*) FROM public."Partida" p JOIN snapshot."s_Partida" s ON s."Id" = p."Id"
   WHERE p."HorarioPrevisto" IS DISTINCT FROM s."HorarioPrevisto"
      OR p."NomeQuadra"      IS DISTINCT FROM s."NomeQuadra"
      OR p."ClubeId"         IS DISTINCT FROM s."ClubeId";')
confere "jogos ainda diferentes do snapshot" "0" "$RESTA"

for t in $TABELAS_DA_CHAVE; do
  esperado=$(psql -At -c "SELECT count(*) FROM snapshot.\"s_$t\";")
  obtido=$(psql -At -c "$(contagem_do_torneio "$t" "$SNAP_ID")")
  confere "$t" "$esperado" "$obtido"
done

if [ "$MODO" = "chave" ]; then
  esperado=$(psql -At -c 'SELECT count(*) FROM snapshot."s_Dupla" WHERE "GrupoTorneioId" IS NOT NULL;')
  obtido=$(psql -At -c "SELECT count(*) FROM public.\"Dupla\" d JOIN public.\"Categoria\" c ON c.\"Id\" = d.\"CategoriaId\" WHERE c.\"TorneioId\" = $SNAP_ID AND d.\"GrupoTorneioId\" IS NOT NULL;")
  confere "duplas de volta no grupo" "$esperado" "$obtido"
fi

if [ "$falhou" = "1" ]; then
  echo "ERRO: a conferência não bateu — o torneio NÃO voltou inteiro. O snapshot do passo 5 tem"
  echo "      o estado de antes desta tentativa."
  exit 1
fi
echo "── Pronto: '$SNAP_CODIGO' está como no snapshot de ${SNAP_EM:-antes}. ──"
