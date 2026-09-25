#!/usr/bin/env bash
# Roda o jogo sem tela (o que o CI faz) e reprova se ele não chegar ao fim pedido ou se sair sujo:
# objeto ou recurso vazando na saída (ex.: som ainda tocando quando o jogo fecha).
# Uso: ferramentas/rodar_sem_tela.sh GODOT [argumentos do jogo depois de --]
set -uo pipefail
godot="$1"; shift
saida=$(mktemp)
"$godot" --headless --path "$(dirname "$0")/../Padel.Godot" -- "$@" 2>&1 | tee "$saida"
if ! grep -q "Saindo após" "$saida"; then echo "::error::o jogo não chegou ao 'Saindo após'"; rm -f "$saida"; exit 1; fi
if grep -qE "leaked at exit|still in use at exit" "$saida"; then echo "::error::o jogo saiu vazando objeto ou recurso (rode com --verbose pra ver quais)"; rm -f "$saida"; exit 1; fi
rm -f "$saida"
