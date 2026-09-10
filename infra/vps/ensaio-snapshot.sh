#!/usr/bin/env bash
# O ENSAIO DO SNAPSHOT — o roteiro do SNAPSHOT-TORNEIO.md, automatizado.
#
# POR QUE ELE EXISTE: `snapshot-torneio.sh` e `restaurar-torneio.sh` são shell, e a suíte deste
# projeto é xUnit — não há teste de regressão em C# que os cubra. Este script é o substituto: ele
# monta um banco descartável com o SCHEMA DE VERDADE (copiado de um banco existente), semeia um
# torneio no formato do Er, e roda o roteiro inteiro medindo cada passo.
#
# ⚠️ Ele NUNCA escreve no banco que copia o schema: lê só a forma (`pg_dump --schema-only`), e
# todo o resto acontece no banco descartável, que ele cria e apaga.
#
# Uso:
#   ensaio-snapshot.sh                        # schema vindo de db_padel
#   ensaio-snapshot.sh --schema-de db_padel_dev
set -euo pipefail

AQUI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCHEMA_DE="db_padel"
ENSAIO="db_padel_ensaio"

while [ $# -gt 0 ]; do
  case "$1" in
    --schema-de) SCHEMA_DE="${2:?}"; shift 2 ;;
    *) echo "ERRO: opção desconhecida '$1'"; exit 1 ;;
  esac
done

TRABALHO=$(mktemp -d)
falhas=0
ok()   { printf '  ✔ %s\n' "$1"; }
nok()  { printf '  ✘ %s\n' "$1"; falhas=$((falhas+1)); }
igual(){ if [ "$2" = "$3" ]; then ok "$1 ($3)"; else nok "$1: esperado=$2 obtido=$3"; fi; }

q() { psql -X -At -q -d "$ENSAIO" -c "$1"; }

limpar() {
  psql -X -q -d postgres -c "DROP DATABASE IF EXISTS \"$ENSAIO\";" >/dev/null 2>&1 || true
  rm -rf "$TRABALHO"
}
trap limpar EXIT

echo "══ Ensaio do snapshot — schema vindo de $SCHEMA_DE ══"
psql -X -q -d postgres -c "DROP DATABASE IF EXISTS \"$ENSAIO\";" >/dev/null
psql -X -q -d postgres -c "CREATE DATABASE \"$ENSAIO\";" >/dev/null
pg_dump --schema-only "$SCHEMA_DE" | psql -X -q -v ON_ERROR_STOP=1 -d "$ENSAIO" >/dev/null 2>&1
igual "schema copiado" "sim" "$([ "$(q "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'")" -gt 50 ] && echo sim || echo nao)"

# ── A semente: um torneio no formato do Er, em miniatura ────────────────────────────────
psql -X -q -v ON_ERROR_STOP=1 -d "$ENSAIO" >/dev/null <<'SQL'
INSERT INTO "Clubes" ("Id","Nome","Endereco","Contato","MarcacaoHorariosAtiva","NotificarHorariosDiariamente")
VALUES (1,'Er Padel','Rua A','x',false,false), (2,'Radar','Rua B','y',false,false);
INSERT INTO "Jogador" ("Id","Nome","CPF","PontuacaoGlobal","IsProfessor","PerfilPrivado","NotificarEmail","NotificarWhatsApp","AgendaMostrarJogosSemanais","AgendaMostrarTorneios","AgendaMostrarAulas","AgendaMostrarAlunos","AgendaMostrarMarcacoes","AgendaFeedToken","IsAdminRaiz","IsAdminGeral","NotificarTorneiosAbertos","NotificarSeguidosTorneio","NotificarAvisoJogo","NotificarJogoAula","NotificarRaqueteLivre","NotificarHorarioVagoRegiao")
SELECT g,'Jogador '||g,lpad(g::text,11,'0'),0,false,false,false,false,false,false,false,false,false,gen_random_uuid(),false,false,false,false,false,false,false,false FROM generate_series(1,12) g;
INSERT INTO "Torneio" ("Id","Nome","Codigo","PermiteImpedimentos","PermiteImpedimentoSextaNoite","PermiteImpedimentoSabadoManha","PermiteImpedimentoSabadoTarde","PrecoInscricao","QuantidadeQuadras","Status","Formato","FormatoUnico","SetsFaseGrupos","GamesFaseGrupos","SetsFaseMataMata","GamesFaseMataMata","SetsFaseFinal","GamesFaseFinal","ClubeId","TempoPrevistoPartidaMinutos","TamanhoGrupo","ClassificadosPorGrupo","BloquearCategoriaInferior","RestricaoCategoria","Restrito")
VALUES (7,'Er Padel Open','ERPADEL',true,true,true,true,100.00,5,'Chaves Aprovadas','Grupos',true,1,6,1,6,1,6,1,50,3,2,false,'Nenhuma',false);
INSERT INTO "Categoria" ("Id","TorneioId","Nome","Codigo","ClubeId") VALUES (10,7,'3a Masculina','3M',1),(11,7,'4a Masculina','4M',2);
INSERT INTO "Quadra" ("Id","TorneioId","Nome","ClubeId") VALUES (20,7,'Quadra 1',1),(21,7,'Quadra 2',1),(22,7,'Radar 1',2);
INSERT INTO "GrupoTorneio" ("Id","CategoriaId","Nome") VALUES (30,10,'Grupo A'),(31,10,'Grupo B'),(32,11,'Grupo A');
INSERT INTO "Dupla" ("Id","CategoriaId","Jogador1Id","Jogador2Id","GrupoTorneioId","Grupo","ImpedimentoSextaNoite","ImpedimentoSabadoManha","ImpedimentoSabadoTarde","UltimaFase","EmListaDeEspera","Pago") VALUES
 (40,10,1,2,30,'Grupo A',false,false,false,'Fase de Grupos',false,true),(41,10,3,4,30,'Grupo A',false,false,false,'Fase de Grupos',false,true),
 (42,10,5,6,31,'Grupo B',false,false,false,'Fase de Grupos',false,true),(43,10,7,8,31,'Grupo B',false,false,false,'Fase de Grupos',false,true),
 (44,11,9,10,32,'Grupo A',false,false,false,'Fase de Grupos',false,true),(45,11,11,12,32,'Grupo A',false,false,false,'Fase de Grupos',false,true);
INSERT INTO "Partida" ("Id","CategoriaId","TorneioId","Dupla1Id","Dupla2Id","Codigo","SendoTransmitida","Status","Fase","HorarioPrevisto","NomeQuadra","ClubeId") VALUES
 (50,10,7,40,41,'P50',false,'Agendada','Fase de Grupos','2026-09-12 08:00','Quadra 1',1),
 (51,10,7,42,43,'P51',false,'Agendada','Fase de Grupos','2026-09-12 08:50','Quadra 1',1),
 (52,10,7,40,42,'P52',false,'Agendada','Fase de Grupos','2026-09-12 09:40','Quadra 2',1),
 (53,10,7,41,43,'P53',false,'Agendada','Fase de Grupos','2026-09-12 10:30','Quadra 2',1),
 (54,11,7,44,45,'P54',false,'Agendada','Fase de Grupos','2026-09-12 11:20','Radar 1',2),
 (55,11,7,45,44,'P55',false,'Agendada','Fase de Grupos','2026-09-12 12:10','Radar 1',2),
 (56,10,7,40,43,'P56',false,'Finalizada','Fase de Grupos','2026-09-11 20:00','Quadra 1',1),
 (57,10,7,41,42,'P57',false,'AoVivo','Fase de Grupos','2026-09-11 21:00','Quadra 1',1);
INSERT INTO "ReservaDeHorario" ("CategoriaId","Fase","Numero","Horario","NomeQuadra") VALUES
 (10,'Final',1,'2026-09-13 22:00','Quadra 1'),(11,'Semifinal',1,'2026-09-13 20:00','Radar 1');
INSERT INTO "Pagamento" ("Id","Tipo","JogadorId","Valor","ValorRepasse","Comissao","Status","CriadoEm","TorneioId")
VALUES (60,'Inscricao',1,100.00,95.00,5.00,'Confirmado','2026-09-01 10:00',7),(61,'Inscricao',3,100.00,95.00,5.00,'Confirmado','2026-09-01 11:00',7);
INSERT INTO "PalpitePartida" ("Id","PartidaId","JogadorId","DuplaEscolhidaId","DataHora") VALUES (70,56,5,40,'2026-09-11 19:00');
SELECT setval(pg_get_serial_sequence('public."Partida"','Id'), 57);
SELECT setval(pg_get_serial_sequence('public."GrupoTorneio"','Id'), 32);
SQL

DINHEIRO_ANTES=$(q "SELECT count(*)||'/'||coalesce(sum(\"Valor\")::text,'0') FROM \"Pagamento\"")

echo ""
echo "── 1. Snapshot ──"
"$AQUI/snapshot-torneio.sh" ERPADEL --banco "$ENSAIO" --saida "$TRABALHO" --sem-cofre >/dev/null
SNAP=$(ls -t "$TRABALHO"/ERPADEL_*.sql.gz | head -1)
igual "arquivo gerado" "sim" "$([ -s "$SNAP" ] && echo sim || echo nao)"
igual "reservas dentro do snapshot" "2" "$(zcat "$SNAP" | awk '/^COPY snapshot."s_ReservaDeHorario" /{d=1;next} d&&$0=="\\."{d=0} d' | wc -l)"

psql -X -q -d "$ENSAIO" -c "COPY (SELECT * FROM \"Partida\" WHERE \"TorneioId\"=7 ORDER BY \"Id\") TO STDOUT" > "$TRABALHO/original.txt"

echo ""
echo "── 2. Refazer grade (o que RecalcularAGradeAsync faz) ──"
psql -X -q -v ON_ERROR_STOP=1 -d "$ENSAIO" >/dev/null <<'SQL'
UPDATE "Partida" SET "HorarioPrevisto"="HorarioPrevisto"+interval '3 hours', "NomeQuadra"='Quadra 2', "ClubeId"=1
 WHERE "TorneioId"=7 AND "Status"='Agendada';
DELETE FROM "ReservaDeHorario" r USING "Categoria" c WHERE c."Id"=r."CategoriaId" AND c."TorneioId"=7;
SQL
igual "jogos remarcados" "6" "$(q "SELECT count(*) FROM \"Partida\" p JOIN (SELECT 1) x ON true WHERE p.\"TorneioId\"=7 AND p.\"Status\"='Agendada'")"
igual "reservas apagadas" "0" "$(q "SELECT count(*) FROM \"ReservaDeHorario\"")"

echo ""
echo "── 3. Conferir (não pode gravar nada) ──"
SAIDA=$("$AQUI/restaurar-torneio.sh" "$SNAP" --grade --banco "$ENSAIO" 2>&1)
igual "listou os 6 jogos que mudaram" "6" "$(echo "$SAIDA" | sed -n 's/.*jogos que voltam de horário\/quadra: //p')"
igual "não gravou (reservas seguem 0)" "0" "$(q "SELECT count(*) FROM \"ReservaDeHorario\"")"

echo ""
echo "── 4. Restaurar a grade ──"
"$AQUI/restaurar-torneio.sh" "$SNAP" --grade --banco "$ENSAIO" --aplicar >/dev/null 2>&1
psql -X -q -d "$ENSAIO" -c "COPY (SELECT * FROM \"Partida\" WHERE \"TorneioId\"=7 ORDER BY \"Id\") TO STDOUT" > "$TRABALHO/restaurado.txt"
if diff -q "$TRABALHO/original.txt" "$TRABALHO/restaurado.txt" >/dev/null; then ok "os 8 jogos voltaram idênticos, byte a byte"; else nok "os jogos NÃO voltaram idênticos"; fi
igual "reservas de volta" "2" "$(q "SELECT count(*) FROM \"ReservaDeHorario\"")"

echo ""
echo "── 5. Idempotência ──"
SAIDA=$("$AQUI/restaurar-torneio.sh" "$SNAP" --grade --banco "$ENSAIO" 2>&1)
igual "rodar de novo muda 0 jogos" "0" "$(echo "$SAIDA" | sed -n 's/.*jogos que voltam de horário\/quadra: //p')"

echo ""
echo "── 6. A trava da FK (palpite pendurado num jogo) ──"
if "$AQUI/restaurar-torneio.sh" "$SNAP" --chave --banco "$ENSAIO" >/dev/null 2>&1; then
  nok "o --chave DEIXOU apagar jogo com palpite pendurado"
else
  ok "o --chave recusou, como devia"
fi

echo ""
echo "── 7. Desfazer sorteio, e a volta pelo --chave ──"
psql -X -q -d "$ENSAIO" -c 'DELETE FROM "PalpitePartida";' -c $'UPDATE "Partida" SET "Status"=\'Agendada\' WHERE "TorneioId"=7;' >/dev/null
"$AQUI/snapshot-torneio.sh" ERPADEL --banco "$ENSAIO" --saida "$TRABALHO" --sem-cofre >/dev/null
BOM=$(ls -t "$TRABALHO"/ERPADEL_*.sql.gz | head -1)
psql -X -q -d "$ENSAIO" -c "COPY (SELECT * FROM \"Partida\" WHERE \"TorneioId\"=7 ORDER BY \"Id\") TO STDOUT" > "$TRABALHO/antes-chave.txt"
psql -X -q -v ON_ERROR_STOP=1 -d "$ENSAIO" >/dev/null <<'SQL'
UPDATE "Dupla" d SET "GrupoTorneioId"=NULL,"Grupo"=NULL FROM "Categoria" c WHERE c."Id"=d."CategoriaId" AND c."TorneioId"=7;
DELETE FROM "ReservaDeHorario" r USING "Categoria" c WHERE c."Id"=r."CategoriaId" AND c."TorneioId"=7;
DELETE FROM "Partida" WHERE "TorneioId"=7;
DELETE FROM "GrupoTorneio" g USING "Categoria" c WHERE c."Id"=g."CategoriaId" AND c."TorneioId"=7;
UPDATE "Torneio" SET "Status"='Chaves em Sorteio' WHERE "Id"=7;
SQL
igual "o sorteio sumiu" "0" "$(q "SELECT count(*) FROM \"Partida\" WHERE \"TorneioId\"=7")"
"$AQUI/restaurar-torneio.sh" "$BOM" --chave --banco "$ENSAIO" --aplicar >/dev/null 2>&1
psql -X -q -d "$ENSAIO" -c "COPY (SELECT * FROM \"Partida\" WHERE \"TorneioId\"=7 ORDER BY \"Id\") TO STDOUT" > "$TRABALHO/depois-chave.txt"
if diff -q "$TRABALHO/antes-chave.txt" "$TRABALHO/depois-chave.txt" >/dev/null; then ok "os jogos voltaram idênticos, com os Ids originais"; else nok "os jogos voltaram diferentes"; fi
igual "Ids preservados" "50,51,52,53,54,55,56,57" "$(q "SELECT string_agg(\"Id\"::text,',' ORDER BY \"Id\") FROM \"Partida\" WHERE \"TorneioId\"=7")"
igual "grupos de volta" "3" "$(q "SELECT count(*) FROM \"GrupoTorneio\" g JOIN \"Categoria\" c ON c.\"Id\"=g.\"CategoriaId\" WHERE c.\"TorneioId\"=7")"
igual "duplas de volta no grupo" "6" "$(q "SELECT count(*) FROM \"Dupla\" d JOIN \"Categoria\" c ON c.\"Id\"=d.\"CategoriaId\" WHERE c.\"TorneioId\"=7 AND d.\"GrupoTorneioId\" IS NOT NULL")"
igual "status de volta" "Chaves Aprovadas" "$(q "SELECT \"Status\" FROM \"Torneio\" WHERE \"Id\"=7")"

echo ""
echo "── 8. Coluna criada por MIGRATION entre o snapshot e a restauração ──"
# O snapshot de ontem não conhece a coluna de hoje. Ela não pode entrar NULA por cima do default
# que a migration prometeu — é o motivo do `LIKE ... INCLUDING DEFAULTS` no snapshot-torneio.sh.
psql -X -q -d "$ENSAIO" -c 'ALTER TABLE "Partida" ADD COLUMN "ColunaNova" boolean NOT NULL DEFAULT true;' >/dev/null
psql -X -q -v ON_ERROR_STOP=1 -d "$ENSAIO" >/dev/null <<'SQL'
UPDATE "Partida" SET "HorarioPrevisto" = "HorarioPrevisto" + interval '2 hours'
 WHERE "TorneioId"=7 AND "Status"='Agendada';
SQL
"$AQUI/restaurar-torneio.sh" "$BOM" --grade --banco "$ENSAIO" --aplicar >/dev/null 2>&1
igual "a coluna nova manteve o default (nenhuma nula)" "0" "$(q "SELECT count(*) FROM \"Partida\" WHERE \"TorneioId\"=7 AND \"ColunaNova\" IS NOT TRUE")"
psql -X -q -d "$ENSAIO" -c 'ALTER TABLE "Partida" DROP COLUMN "ColunaNova";' >/dev/null

echo ""
echo "── 9. A sequence ficou à frente? ──"
NOVO=$(q "INSERT INTO \"Partida\" (\"CategoriaId\",\"TorneioId\",\"Dupla1Id\",\"Dupla2Id\",\"Codigo\",\"SendoTransmitida\",\"Status\",\"Fase\") VALUES (10,7,40,41,'PNOVO',false,'Agendada','Semifinal') RETURNING \"Id\"")
if [ "${NOVO:-0}" -gt 57 ]; then ok "jogo novo nasceu no Id $NOVO, sem colidir"; else nok "jogo novo nasceu no Id $NOVO — colide com o restaurado"; fi
psql -X -q -d "$ENSAIO" -c "DELETE FROM \"Partida\" WHERE \"Codigo\"='PNOVO'" >/dev/null

echo ""
echo "── 10. Teste negativo: o dinheiro não se mexeu ──"
igual "pagamentos (contagem/soma)" "$DINHEIRO_ANTES" "$(q "SELECT count(*)||'/'||coalesce(sum(\"Valor\")::text,'0') FROM \"Pagamento\"")"
igual "duplas (inscrições) intactas" "6" "$(q "SELECT count(*) FROM \"Dupla\" d JOIN \"Categoria\" c ON c.\"Id\"=d.\"CategoriaId\" WHERE c.\"TorneioId\"=7")"

echo ""
if [ "$falhas" = "0" ]; then
  echo "══ ENSAIO VERDE ══"
else
  echo "══ ENSAIO VERMELHO: $falhas falha(s) ══"; exit 1
fi
