#!/usr/bin/env bash
# Rodadas de teste (sem tela, com --sair-apos, --bot, carreira automática) não tocam no save de quem joga e terminam
# sozinhas. Cada caso roda o jogo de verdade com um user:// só dele — no Linux o Godot tira o user:// do
# XDG_DATA_HOME —, semeado com um save conhecido, e confere o disco e o log depois.
# Uso: ferramentas/conferir_rodadas_de_teste.sh GODOT [SÓ_ESTE_CASO]
set -uo pipefail
godot="$1"
so="${2:-}"
projeto="$(cd "$(dirname "$0")/../Padel.Godot" && pwd)"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
falhas=0
sentinela='{"Versao": 1, "sentinela": "o save de quem joga - teste nenhum pode mexer aqui"}'

# jogo XDG LOG LIMITE ARGS... — o Godot sem tela, passo fixo de 120 Hz (a partida roda bem mais rápido que o relógio).
jogo() {
    local xdg="$1" log="$2" limite="$3"; shift 3
    XDG_DATA_HOME="$xdg" timeout "$limite" "$godot" --headless --path "$projeto" --fixed-fps 120 "$@" > "$log" 2>&1
}
user_de() { echo "$1/godot/app_userdata/Padelizou Arena"; }   # o user:// do projeto (config/name do project.godot)
md5() { md5sum "$1" 2>/dev/null | cut -d' ' -f1; }

resultado() {   # resultado NOME PROBLEMAS
    if [ -n "$2" ]; then echo "FALHOU  $1:$2"; falhas=$((falhas + 1)); else echo "ok      $1"; fi
}
quero() { [ -z "$so" ] || [ "$so" = "$1" ]; }

# ---- A carreira de teste (--carreira-sozinha, --carreira-nova) só mexe no --carreira ARQ ----

if quero carreira-sozinha-com-arquivo; then
    xdg="$tmp/c1"; u=$(user_de "$xdg"); mkdir -p "$u"; printf '%s' "$sentinela" > "$u/carreira.json"; antes=$(md5 "$u/carreira.json")
    jogo "$xdg" "$tmp/c1.log" 120 res://cenas/Carreira.tscn -- --carreira-sozinha --carreira "$tmp/c1-carreira.json" --perfil "$tmp/c1-perfil.json" --sair-apos 5
    codigo=$?; p=""
    [ "$codigo" -ne 0 ] && p+=" saiu com código $codigo;"
    [ "$(md5 "$u/carreira.json")" != "$antes" ] && p+=" mexeu no user://carreira.json;"
    [ -f "$tmp/c1-carreira.json" ] || p+=" não gravou a carreira no --carreira ARQ;"
    resultado "carreira-sozinha-com-arquivo (--carreira-sozinha --carreira ARQ não toca no user://carreira.json)" "$p"
fi

if quero carreira-sozinha-sem-arquivo; then
    xdg="$tmp/c2"; u=$(user_de "$xdg"); mkdir -p "$u"; printf '%s' "$sentinela" > "$u/carreira.json"; antes=$(md5 "$u/carreira.json")
    jogo "$xdg" "$tmp/c2.log" 60 res://cenas/Carreira.tscn -- --carreira-sozinha --perfil "$tmp/c2-perfil.json" --sair-apos 5
    codigo=$?; p=""
    { [ "$codigo" -eq 0 ] || [ "$codigo" -eq 124 ]; } && p+=" deveria recusar e sair com erro (veio código $codigo);"
    grep -q -- "--carreira ARQ" "$tmp/c2.log" || p+=" não disse que falta o --carreira ARQ;"
    [ "$(md5 "$u/carreira.json")" != "$antes" ] && p+=" mexeu no user://carreira.json;"
    resultado "carreira-sozinha-sem-arquivo (--carreira-sozinha sem --carreira recusa)" "$p"
fi

if quero carreira-nova-sem-arquivo; then
    xdg="$tmp/c3"; u=$(user_de "$xdg"); mkdir -p "$u"; printf '%s' "$sentinela" > "$u/carreira.json"; antes=$(md5 "$u/carreira.json")
    jogo "$xdg" "$tmp/c3.log" 20 res://cenas/Carreira.tscn -- --carreira-nova
    codigo=$?; p=""
    { [ "$codigo" -eq 0 ] || [ "$codigo" -eq 124 ]; } && p+=" deveria recusar e sair com erro (veio código $codigo);"
    [ "$(md5 "$u/carreira.json")" != "$antes" ] && p+=" apagou ou trocou o user://carreira.json;"
    resultado "carreira-nova-sem-arquivo (--carreira-nova sem --carreira recusa e não apaga o save)" "$p"
fi

# ---- Rodada automática (--sair-apos, --screenshot) só grava perfil com --perfil ARQ ----

sem_perfil() {   # sem_perfil NOME ARGS... — a rodada termina e o user://perfil.json não aparece
    local nome="$1"; shift
    local xdg="$tmp/$nome"; local u; u=$(user_de "$xdg"); mkdir -p "$u"
    jogo "$xdg" "$tmp/$nome.log" 60 -- "$@"
    local codigo=$? p=""
    [ "$codigo" -ne 0 ] && p+=" saiu com código $codigo;"
    grep -q "Saindo após" "$tmp/$nome.log" || p+=" não chegou ao 'Saindo após';"
    [ -e "$u/perfil.json" ] && p+=" gravou o user://perfil.json de verdade ($(head -c 120 "$u/perfil.json" | tr '\n' ' '));"
    resultado "$nome (rodada automática sem --perfil não grava perfil: $*)" "$p"
}
quero perfil-sozinho && sem_perfil perfil-sozinho --sair-apos 3
quero perfil-coop && sem_perfil perfil-coop --coop --sair-apos 3
quero perfil-host && sem_perfil perfil-host --host 47893 --esperar 1 --sair-apos 3

if quero perfil-com-arquivo; then   # o controle: com --perfil ARQ a rodada grava, e só lá
    xdg="$tmp/p4"; u=$(user_de "$xdg"); mkdir -p "$u"
    jogo "$xdg" "$tmp/p4.log" 60 -- --coop --sair-apos 3 --perfil "$tmp/p4-perfil.json"
    codigo=$?; p=""
    [ "$codigo" -ne 0 ] && p+=" saiu com código $codigo;"
    [ -f "$tmp/p4-perfil.json" ] || p+=" não gravou no --perfil ARQ;"
    [ -e "$u/perfil.json" ] && p+=" gravou também o user://perfil.json;"
    resultado "perfil-com-arquivo (com --perfil ARQ a rodada automática grava no ARQ)" "$p"
fi

# ---- A rodada automática termina sozinha, e termina quando diz que termina ----

if quero carreira-sai-apos-o-fim; then
    # A partida da carreira acaba antes do prazo: o "Saindo após" sai, e a volta pra tela da carreira (agendada pelo fim)
    # não pode engolir o Quit — antes o SairLimpo morria com a cena trocada e o processo jogava o circuito inteiro.
    xdg="$tmp/s1"; mkdir -p "$(user_de "$xdg")"
    jogo "$xdg" "$tmp/s1.log" 150 res://cenas/Carreira.tscn -- --carreira "$tmp/s1-carreira.json" --carreira-nova --carreira-sozinha --sair-apos 100000 --perfil "$tmp/s1-perfil.json"
    codigo=$?; p=""
    [ "$codigo" -eq 124 ] && p+=" não saiu sozinho (timeout);"
    [ "$codigo" -ne 0 ] && [ "$codigo" -ne 124 ] && p+=" saiu com código $codigo;"
    [ "$(grep -c "Saindo após" "$tmp/s1.log")" -eq 1 ] || p+=" esperava um 'Saindo após' só (veio $(grep -c "Saindo após" "$tmp/s1.log"));"
    depois=$(sed -n '/Saindo após/,$p' "$tmp/s1.log" | grep -c "Carreira: jogo")
    [ "$depois" -gt 0 ] && p+=" seguiu jogando a carreira depois do 'Saindo após' ($depois jogo(s));"
    grep -q "Exception" "$tmp/s1.log" && p+=" exceção no log ($(grep -m1 "Exception" "$tmp/s1.log" | cut -c1-120));"
    resultado "carreira-sai-apos-o-fim (--sair-apos na carreira automática: a partida que acaba antes sai do jogo)" "$p"
fi

if quero screenshot-sem-tela; then
    # Sem tela o quadro desenhado nunca chega: o --screenshot da partida esperava pra sempre depois do "Saindo após".
    xdg="$tmp/f1"; mkdir -p "$(user_de "$xdg")"
    jogo "$xdg" "$tmp/f1.log" 60 -- --auto --semente 42 --sair-apos 3 --screenshot "$tmp/f1.png"
    codigo=$?; p=""
    [ "$codigo" -eq 124 ] && p+=" travou esperando o quadro que não vem (timeout);"
    [ "$codigo" -eq 0 ] && p+=" deveria recusar com erro (saiu 0 sem foto);"
    grep -q "Screenshot: sem tela" "$tmp/f1.log" || p+=" não disse que sem tela não há foto;"
    resultado "screenshot-sem-tela (--screenshot sem tela recusa e sai com erro)" "$p"
fi

if quero screenshot-sem-tela-no-menu; then   # o mesmo nas telas de interface (menu, carreira): a Captura esperava igual
    xdg="$tmp/f4"; mkdir -p "$(user_de "$xdg")"
    jogo "$xdg" "$tmp/f4.log" 30 -- --screenshot "$tmp/f4.png"
    codigo=$?; p=""
    [ "$codigo" -eq 124 ] && p+=" travou esperando o quadro que não vem (timeout);"
    [ "$codigo" -eq 0 ] && p+=" deveria recusar com erro (saiu 0 sem foto);"
    grep -q "Screenshot: sem tela" "$tmp/f4.log" || p+=" não disse que sem tela não há foto;"
    resultado "screenshot-sem-tela-no-menu (--screenshot do menu sem tela recusa e sai com erro)" "$p"
fi

# Com tela: um X virtual. O llvmpipe desenha devagar, então sem --fixed-fps (o relógio anda) e numa janela pequena.
com_tela() {   # com_tela XDG LOG ARGS...
    local xdg="$1" log="$2"; shift 2
    XDG_DATA_HOME="$xdg" timeout 120 xvfb-run -a -s "-screen 0 640x400x24" "$godot" --path "$projeto" --audio-driver Dummy --resolution 640x400 -- "$@" > "$log" 2>&1
}
if quero screenshot-com-tela || quero screenshot-que-falha; then
    if ! command -v xvfb-run > /dev/null; then
        resultado "screenshot-com-tela / screenshot-que-falha" " precisa do xvfb-run (apt install xvfb) pra testar a foto com tela;"
    else
        if quero screenshot-com-tela; then   # o controle: a foto boa sai e o jogo fecha com 0
            xdg="$tmp/f2"; mkdir -p "$(user_de "$xdg")"
            com_tela "$xdg" "$tmp/f2.log" --auto --semente 42 --sair-apos 3 --screenshot "$tmp/f2.png"
            codigo=$?; p=""
            [ "$codigo" -ne 0 ] && p+=" saiu com código $codigo;"
            [ -s "$tmp/f2.png" ] || p+=" não salvou a foto;"
            resultado "screenshot-com-tela (a foto sai e o jogo fecha com 0)" "$p"
        fi
        if quero screenshot-que-falha; then   # foto que não dá pra gravar sai com erro, como a Captura das outras telas
            xdg="$tmp/f3"; mkdir -p "$(user_de "$xdg")"
            com_tela "$xdg" "$tmp/f3.log" --auto --semente 42 --sair-apos 3 --screenshot "$tmp/nao-existe/f3.png"
            codigo=$?; p=""
            [ "$codigo" -eq 124 ] && p+=" não saiu sozinho (timeout);"
            [ "$codigo" -eq 0 ] && p+=" a foto falhou e o jogo saiu com 0;"
            grep -q "Screenshot FALHOU" "$tmp/f3.log" || p+=" não disse que a foto falhou;"
            resultado "screenshot-que-falha (a foto que não grava sai com erro)" "$p"
        fi
    fi
fi

echo "Rodadas de teste: $falhas falha(s)."
[ "$falhas" -eq 0 ]
