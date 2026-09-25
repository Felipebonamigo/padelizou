#!/usr/bin/env bash
# O online quando a sala não abre: porta ocupada, endereço que não resolve, host que não existe.
# Em todos, o jogo tem que DIZER o motivo (uma linha "Sala: ...") e chegar ao "Saindo após" — sem exceção em laço,
# sem ficar "Conectando" pra sempre. Uso: ferramentas/conferir_salas_impossiveis.sh GODOT
set -uo pipefail
godot="$1"
projeto="$(cd "$(dirname "$0")/../Padel.Godot" && pwd)"
falhas=0
saida=$(mktemp)

caso() {   # caso NOME ESPERADO ARGS...
    local nome="$1" esperado="$2"; shift 2
    timeout 90 "$godot" --headless --path "$projeto" -- "$@" > "$saida" 2>&1
    local codigo=$?
    local problemas=""
    [ "$codigo" -eq 124 ] && problemas+=" não saiu sozinho (timeout);"
    grep -q "Saindo após" "$saida" || problemas+=" não chegou ao 'Saindo após';"
    grep -qE "$esperado" "$saida" || problemas+=" não disse o motivo (esperado: /$esperado/);"
    local excecoes; excecoes=$(grep -c "Exception" "$saida")
    [ "$excecoes" -gt 0 ] && problemas+=" $excecoes linha(s) de exceção no log;"
    if [ -n "$problemas" ]; then echo "FALHOU  $nome:$problemas"; falhas=$((falhas + 1)); else echo "ok      $nome"; fi
}

# Porta ocupada: um socket UDP do Python segura a porta.
porta=47890
python3 -c "import socket,time; s=socket.socket(socket.AF_INET, socket.SOCK_DGRAM); s.bind(('0.0.0.0', $porta)); time.sleep(80)" &
segura=$!
sleep 1
caso "porta ocupada" "Sala: .*porta $porta" --host "$porta" --sair-apos 3
kill "$segura" 2>/dev/null

caso "endereço que não resolve" "Sala: .*sala-que-nao-existe.invalid" --conectar sala-que-nao-existe.invalid:7777 --sair-apos 3
caso "ninguém hospedando no endereço" "Sala: .*(não respondeu|sem resposta)" --conectar 127.0.0.1:47891 --sair-apos 25

# Recusado: a partida do host já começou quando o cliente chega. A tela de fim diz o motivo — antes dizia " /  venceu",
# na cor da vitória (o cliente sem vaga era tratado como demonstração).
porta_host=47892
log_host=$(mktemp)
timeout 90 "$godot" --headless --path "$projeto" -- --host "$porta_host" --esperar 1 --sair-apos 60 > "$log_host" 2>&1 &
host=$!
for _ in $(seq 1 120); do grep -q "partida iniciada" "$log_host" && break; sleep 0.5; done
caso "recusado: a partida já começou" "Fim: O host recusou a entrada: a partida já começou" --conectar "127.0.0.1:$porta_host" --sair-apos 20
kill "$host" 2>/dev/null
rm -f "$log_host"

rm -f "$saida"
echo "Salas impossíveis: $falhas falha(s)."
[ "$falhas" -eq 0 ]
