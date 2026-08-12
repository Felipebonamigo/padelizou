#!/bin/bash
# Faz o X-Frame-Options parar de sair DUPLICADO — só nos blocos do Padelizou.
#
# ── DE ONDE VINHA O SEGUNDO (descoberto em 12/08/2026, depois de duas rodadas achando
#    que era código nosso) ────────────────────────────────────────────────────────────────
#
# Do **próprio ASP.NET Core**: o `DefaultAntiforgery` acrescenta `X-Frame-Options: SAMEORIGIN`
# sempre que EMITE o cookie de token antiforgery. Não é código nosso — nunca foi. Provado
# assim, e o teste vale a pena repetir se alguém duvidar:
#
#   1a requisição sem cookie  -> Set-Cookie: .AspNetCore.Antiforgery... + X-Frame-Options
#   2a requisição COM o cookie -> nenhum dos dois
#
# Por isso `grep` no .cs e `strings` no Padelizou.dll davam ZERO: a string mora no
# Microsoft.AspNetCore.Antiforgery.dll, e como o publish é framework-dependent essa dll está
# em /usr/share/dotnet/shared/, não no diretório do app.
#
# E por isso só aparecia em resposta de VIEW (o /healthz e os arquivos estáticos não emitem
# token, e não tinham o header): com o CSRF global do Program.cs, toda view emite.
#
# ── A CORREÇÃO, E POR QUE NÃO É "TIRAR DO CADDY" ────────────────────────────────────────
#
# Tirar do Caddy deixaria SEM o cabeçalho justamente o que o antiforgery não cobre: arquivo
# estático, /healthz e qualquer resposta que não emita token. O Caddy tem que continuar
# pondo — só precisa SUBSTITUIR em vez de acrescentar.
#
# No Caddy v2 o prefixo `>` é exatamente isso: "set, substituindo inclusive o que veio do
# upstream". Sem ele, o valor do app e o do Caddy convivem, e a RFC 7034 diz que servidor não
# deve mandar mais de um (navegador pode ignorar os dois quando divergem).
#
# 🛟 Rede de segurança: mesmo que isto desse errado e o cabeçalho sumisse, o CSP que entrou em
#    11/08 traz `frame-ancestors 'self'`, que é o sucessor do X-Frame-Options e tem precedência
#    nos navegadores atuais. A proteção contra clickjacking não depende só desta linha.
set -eo pipefail

CADDYFILE=/etc/caddy/Caddyfile
BACKUP="$CADDYFILE.bak-$(date +%Y%m%d-%H%M%S)"
cp -p "$CADDYFILE" "$BACKUP"
echo "== backup em $BACKUP =="

python3 - "$CADDYFILE" <<'PYTHON'
import sys
caminho = sys.argv[1]
with open(caminho, encoding='utf-8') as f:
    linhas = f.readlines()

# SÓ os blocos do Padelizou. O terceiro X-Frame-Options do arquivo é de OUTRO negócio e não
# é da nossa conta — mexer nele seria alterar o site de outra pessoa de carona.
BLOCOS = ('padelizou.com.br, admin.padelizou.com.br', 'dev.padelizou.com.br')
trocadas = 0

for nome in BLOCOS:
    inicio = next((i for i, l in enumerate(linhas)
                   if l.startswith(nome) and l.rstrip().endswith('{')), None)
    if inicio is None:
        sys.exit(f'ERRO: não achei o bloco "{nome}" — nada foi alterado.')
    fim = next((i for i in range(inicio + 1, len(linhas)) if linhas[i].rstrip() == '}'), None)
    if fim is None:
        sys.exit(f'ERRO: não achei o fim do bloco "{nome}" — nada foi alterado.')

    for i in range(inicio, fim):
        despido = linhas[i].strip()
        if despido == 'X-Frame-Options SAMEORIGIN':
            linhas[i] = linhas[i].replace('X-Frame-Options', '>X-Frame-Options', 1)
            trocadas += 1
            print(f'  linha {i + 1}: virou ">X-Frame-Options" (bloco {nome.split(",")[0]})')

if trocadas != 2:
    sys.exit(f'ERRO: esperava trocar 2 linhas e troquei {trocadas} — nada foi gravado.')

with open(caminho, 'w', encoding='utf-8') as f:
    f.writelines(linhas)
PYTHON

echo
echo "== VALIDANDO =="
if ! caddy validate --config "$CADDYFILE" --adapter caddyfile 2>&1 | grep -q "Valid configuration"; then
  echo "🚨 VALIDATE REPROVOU — desfazendo, o servico nao foi tocado."
  cp -p "$BACKUP" "$CADDYFILE"
  exit 1
fi
echo "  Valid configuration"

echo
echo "== RELOAD =="
systemctl reload caddy
sleep 3
systemctl is-active caddy

echo
echo "== QUANTAS VEZES O CABECALHO SAI AGORA (tem que ser 1) =="
for U in https://padelizou.com.br/ https://padelizou.com.br/Auth/Login https://admin.padelizou.com.br/; do
  printf "  %-42s %s\n" "$U" "$(curl -s -o /dev/null -D - --max-time 15 "$U" | grep -ci '^x-frame-options')"
done

echo
echo "== E O VALOR CONTINUA CERTO? =="
curl -s -o /dev/null -D - --max-time 15 https://padelizou.com.br/ | grep -i "^x-frame-options\|^content-security" | cut -c1-55

echo
echo "== OS OUTROS NEGOCIOS (nao encostei neles, mas conferindo) =="
for D in $(grep -oE '^[a-z0-9.-]+\.[a-z]{2,3}(\.[a-z]{2})?' "$CADDYFILE" | sort -u); do
  printf "  %-32s %s\n" "$D" "$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "https://$D/" || echo FALHOU)"
done
