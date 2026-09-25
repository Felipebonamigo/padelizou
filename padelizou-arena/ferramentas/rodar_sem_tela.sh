#!/usr/bin/env bash
# Roda o jogo sem tela (o que o CI faz) e reprova se ele não chegar ao fim pedido ou se sair sujo:
# código de saída diferente de 0, exceção ou erro de script no log, ou objeto/recurso vazando na saída
# (ex.: som ainda tocando quando o jogo fecha). Testado por ferramentas/teste_rodar_sem_tela.sh.
# Uso: ferramentas/rodar_sem_tela.sh GODOT [argumentos do jogo depois de --]
set -uo pipefail
godot="$1"; shift
saida=$(mktemp)
trap 'rm -f "$saida"' EXIT

timeout 300 "$godot" --headless --path "$(dirname "$0")/../Padel.Godot" -- "$@" 2>&1 | tee "$saida"
status=${PIPESTATUS[0]}

reprovar() { echo "::error::$1"; exit 1; }
[ "$status" -eq 124 ] && reprovar "o jogo não terminou em 300 s (timeout)"
[ "$status" -ne 0 ] && reprovar "o jogo saiu com código $status"
grep -q "Saindo após" "$saida" || reprovar "o jogo não chegou ao 'Saindo após'"
grep -qE "^(ERROR|SCRIPT ERROR): |Unhandled exception" "$saida" && reprovar "exceção ou erro no log (as linhas 'ERROR:' acima)"
grep -qE "leaked at exit|still in use at exit" "$saida" && reprovar "o jogo saiu vazando objeto ou recurso (rode com --verbose pra ver quais)"
exit 0
