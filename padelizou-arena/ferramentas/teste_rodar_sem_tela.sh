#!/usr/bin/env bash
# Testa o ferramentas/rodar_sem_tela.sh com "Godots" falsos: ele tem que reprovar o que está quebrado.
set -uo pipefail
aqui="$(cd "$(dirname "$0")" && pwd)"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
falhas=0

falso() {   # falso NOME SAIDA CODIGO — cria um "godot" que imprime SAIDA e sai com CODIGO
    printf '#!/usr/bin/env bash\nprintf "%%b" "%s"\nexit %s\n' "$2" "$3" > "$tmp/$1"
    chmod +x "$tmp/$1"
}
espera() {   # espera NOME CODIGO_ESPERADO(0|1)
    "$aqui/rodar_sem_tela.sh" "$tmp/$1" --auto > /dev/null 2>&1
    local codigo=$?
    if { [ "$2" -eq 0 ] && [ "$codigo" -eq 0 ]; } || { [ "$2" -ne 0 ] && [ "$codigo" -ne 0 ]; }; then echo "ok      $1"
    else echo "FALHOU  $1: esperado $([ "$2" -eq 0 ] && echo aprovar || echo reprovar), deu código $codigo"; falhas=$((falhas + 1)); fi
}

falso limpo 'Saindo após 15.0 s: tudo certo\n' 0;                                                  espera limpo 0
falso sem_fim 'rodando...\n' 0;                                                                      espera sem_fim 1
falso vazando 'Saindo após 15.0 s\nWARNING: 3 ObjectDB instances were leaked at exit\n' 0;           espera vazando 1
falso sai_com_erro 'Saindo após 15.0 s\n' 139;                                                       espera sai_com_erro 1
falso excecao 'ERROR: System.NullReferenceException: Object reference not set\nSaindo após 15.0 s\n' 0; espera excecao 1
falso excecao_sem_tratar 'Saindo após 15.0 s\nUnhandled exception. System.InvalidOperationException: x\n' 0; espera excecao_sem_tratar 1
falso erro_de_script 'SCRIPT ERROR: Invalid call\nSaindo após 15.0 s\n' 0;                           espera erro_de_script 1

echo "rodar_sem_tela: $falhas falha(s)."
[ "$falhas" -eq 0 ]
