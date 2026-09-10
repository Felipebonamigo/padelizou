#!/usr/bin/env bash
# QUAIS TABELAS FORMAM UM TORNEIO — a lista, e o filtro que pega só as linhas de um deles.
#
# Nasceu extraído do `copiar-torneio.sh` em 10/09/2026, quando o `snapshot-torneio.sh` precisou
# da mesma lista. Duas cópias divergiriam na primeira tabela nova — e divergiram: o
# `copiar-torneio.sh` é de 09/09, a `ReservaDeHorario` nasceu em 10/09, e o ensaio do Er no `dev`
# estava indo sem as reservas de horário das finais. Uma lista só, num arquivo só.
#
# Quem usa: copiar-torneio.sh, snapshot-torneio.sh, restaurar-torneio.sh.
#
# ⚠️ TABELA NOVA QUE PENDURE NO TORNEIO ENTRA AQUI. O sinal de que faltou é sempre o mesmo:
# alguma das três ferramentas leva o torneio pela metade e ninguém percebe, porque a tela abre.

# ── A ORDEM É A DE DEPENDÊNCIA ────────────────────────────────────────────────────────────
# É nela que as linhas são gravadas, então cada tabela vem depois de quem ela cita. `Clubes` e
# `Jogador` vêm primeiro porque o torneio inteiro os cita; `ReservaDeHorario` vem por último,
# citando só `Categoria`.
TABELAS_DO_TORNEIO="Clubes Jogador Torneio Categoria Quadra GrupoTorneio QuadraDaCategoria Dupla Partida TorneioOrganizador TorneioMarcador ReservaDeHorario"

# As que são a CHAVE propriamente dita — o que o sorteio cria e os dois botões destroem. É o
# recorte que o `restaurar-torneio.sh --chave` devolve; o resto do torneio (categoria, quadra,
# inscrição) nenhum dos dois botões toca.
TABELAS_DA_CHAVE="GrupoTorneio Partida ReservaDeHorario"

# filtro_do_torneio <tabela> <torneio_id> → o SELECT que devolve só as linhas daquele torneio.
filtro_do_torneio() {
  local tabela="$1" t="$2"
  case "$tabela" in
    Torneio)            echo "SELECT * FROM \"Torneio\" WHERE \"Id\" = $t" ;;
    Categoria)          echo "SELECT * FROM \"Categoria\" WHERE \"TorneioId\" = $t" ;;
    Quadra)             echo "SELECT * FROM \"Quadra\" WHERE \"TorneioId\" = $t" ;;
    GrupoTorneio)       echo "SELECT g.* FROM \"GrupoTorneio\" g JOIN \"Categoria\" c ON c.\"Id\" = g.\"CategoriaId\" WHERE c.\"TorneioId\" = $t" ;;
    QuadraDaCategoria)  echo "SELECT q.* FROM \"QuadraDaCategoria\" q JOIN \"Categoria\" c ON c.\"Id\" = q.\"CategoriaId\" WHERE c.\"TorneioId\" = $t" ;;
    Dupla)              echo "SELECT d.* FROM \"Dupla\" d JOIN \"Categoria\" c ON c.\"Id\" = d.\"CategoriaId\" WHERE c.\"TorneioId\" = $t" ;;
    Partida)            echo "SELECT * FROM \"Partida\" WHERE \"TorneioId\" = $t" ;;
    TorneioOrganizador) echo "SELECT * FROM \"TorneioOrganizador\" WHERE \"TorneioId\" = $t" ;;
    TorneioMarcador)    echo "SELECT * FROM \"TorneioMarcador\" WHERE \"TorneioId\" = $t" ;;
    # A reserva do horário de uma eliminatória que ainda não nasceu (Models/ReservaDeHorario).
    # Pendura na Categoria, não no Torneio — a chave dela é (CategoriaId, Fase, Numero).
    ReservaDeHorario)   echo "SELECT r.* FROM \"ReservaDeHorario\" r JOIN \"Categoria\" c ON c.\"Id\" = r.\"CategoriaId\" WHERE c.\"TorneioId\" = $t" ;;
    # Os jogadores citados por QUALQUER um dos anteriores — dupla, organizador, marcador.
    Jogador)            echo "SELECT * FROM \"Jogador\" WHERE \"Id\" IN (
                                SELECT d.\"Jogador1Id\" FROM \"Dupla\" d JOIN \"Categoria\" c ON c.\"Id\"=d.\"CategoriaId\" WHERE c.\"TorneioId\" = $t
                                UNION SELECT d.\"Jogador2Id\" FROM \"Dupla\" d JOIN \"Categoria\" c ON c.\"Id\"=d.\"CategoriaId\" WHERE c.\"TorneioId\" = $t
                                UNION SELECT \"JogadorId\" FROM \"TorneioOrganizador\" WHERE \"TorneioId\" = $t
                                UNION SELECT \"JogadorId\" FROM \"TorneioMarcador\" WHERE \"TorneioId\" = $t)" ;;
    # Os clubes citados pelo torneio, pelas categorias e pelas quadras. `Torneio.ClubeId` é NOT NULL.
    Clubes)             echo "SELECT * FROM \"Clubes\" WHERE \"Id\" IN (
                                SELECT \"ClubeId\" FROM \"Torneio\" WHERE \"Id\" = $t
                                UNION SELECT \"ClubeId\" FROM \"Categoria\" WHERE \"TorneioId\" = $t
                                UNION SELECT \"ClubeId\" FROM \"Quadra\" WHERE \"TorneioId\" = $t)" ;;
    *) echo "ERRO_TABELA_DESCONHECIDA_$tabela" ; return 1 ;;
  esac
}

# Como contar as linhas de uma tabela JÁ GRAVADA num banco, pra conferir as duas pontas. É o
# mesmo filtro de cima visto do outro lado: por TorneioId direto, ou pela Categoria.
contagem_do_torneio() {
  local tabela="$1" t="$2"
  case "$tabela" in
    Torneio)
      echo "SELECT count(*) FROM \"Torneio\" WHERE \"Id\" = $t" ;;
    Categoria|Quadra|Partida|TorneioOrganizador|TorneioMarcador)
      echo "SELECT count(*) FROM \"$tabela\" WHERE \"TorneioId\" = $t" ;;
    GrupoTorneio|QuadraDaCategoria|Dupla|ReservaDeHorario)
      echo "SELECT count(*) FROM \"$tabela\" x JOIN \"Categoria\" c ON c.\"Id\" = x.\"CategoriaId\" WHERE c.\"TorneioId\" = $t" ;;
    *) echo "ERRO_TABELA_DESCONHECIDA_$tabela" ; return 1 ;;
  esac
}
