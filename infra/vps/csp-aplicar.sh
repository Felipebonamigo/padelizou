#!/bin/bash
# Põe (ou troca) o Content-Security-Policy num bloco do Caddyfile.
#
#   csp-aplicar.sh <dominio-do-bloco> <report|bloquear|remover>
#
#   csp-aplicar.sh padelizou.com.br report     # reporta no console, NAO bloqueia nada
#   csp-aplicar.sh padelizou.com.br bloquear   # passa a valer de verdade
#   csp-aplicar.sh padelizou.com.br remover    # desfaz
#
# O caminho seguro de implantar CSP é este: "report" primeiro, navegar o site olhando o
# Console, e só trocar pra "bloquear" quando ele estiver limpo. Em Report-Only o navegador
# escreve "would have been blocked" e carrega tudo assim mesmo — o risco de derrubar o site
# é ZERO, e é por isso que dá pra fazer isso em produção.
#
# 🚨 O /etc/caddy/Caddyfile SERVE CINCO NEGÓCIOS. Por isso: acha o bloco pelo NOME (nunca por
#    número de linha), `caddy validate` ANTES do reload, backup restaurado sozinho se o
#    validate reprovar, `reload` e nunca `restart`, e todos os domínios testados no fim.
set -eo pipefail

BLOCO="${1:?uso: csp-aplicar.sh <dominio-do-bloco> <report|bloquear|remover>}"
MODO="${2:?uso: csp-aplicar.sh <dominio-do-bloco> <report|bloquear|remover>}"

CADDYFILE=/etc/caddy/Caddyfile
BACKUP="$CADDYFILE.bak-$(date +%Y%m%d-%H%M%S)"
cp -p "$CADDYFILE" "$BACKUP"
echo "== backup em $BACKUP =="

python3 - "$CADDYFILE" "$BLOCO" "$MODO" <<'PYTHON'
import sys
caminho, bloco, modo = sys.argv[1], sys.argv[2], sys.argv[3]

with open(caminho, encoding='utf-8') as f:
    linhas = f.readlines()

# A política é UMA só, e é a mesma no dev e na produção — ambiente de teste que roda com
# regra diferente da produção não testa a produção.
POLITICA = (
    "default-src 'self'; "
    # 'unsafe-inline': 39 views com <script> inline, 127 handlers on*= e 1.164 style=.
    # Com ele o CSP NÃO barra XSS injetado; o que barra é CARREGAR script de fora.
    "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; "
    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; "
    # ⚠️ jsdelivr no font-src NÃO é sobra: o bootstrap-icons.min.css vem de lá e puxa a
    # webfont dos ícones do mesmo domínio. Sem isto, todo ícone do site vira quadradinho.
    "font-src 'self' data: https://cdn.jsdelivr.net https://fonts.gstatic.com; "
    "img-src 'self' data: https:; "   # data: é o QR do Pix; imagem não executa, https: é barato
    "frame-src https://www.youtube.com; "
    "connect-src 'self'; "            # nenhum fetch do front sai do domínio
    "worker-src 'self'; "             # o service worker do PWA
    "manifest-src 'self'; "
    "object-src 'none'; "
    "base-uri 'self'; "               # <base> injetado sequestraria todo link relativo
    "form-action 'self'; "            # o XSS não aponta o POST do LOGIN pra fora
    "frame-ancestors 'self'; "
    "upgrade-insecure-requests"
)

NOME = ('Content-Security-Policy-Report-Only' if modo == 'report'
        else 'Content-Security-Policy' if modo == 'bloquear'
        else None)
if modo not in ('report', 'bloquear', 'remover'):
    sys.exit(f'ERRO: modo "{modo}" não existe (use report, bloquear ou remover).')

inicio = next((i for i, l in enumerate(linhas)
               if l.startswith(bloco) and l.rstrip().endswith('{')), None)
if inicio is None:
    sys.exit(f'ERRO: não achei um bloco começando em "{bloco}" — nada foi alterado.')
fim = next((i for i in range(inicio + 1, len(linhas)) if linhas[i].rstrip() == '}'), None)
if fim is None:
    sys.exit('ERRO: não achei o fim do bloco — nada foi alterado.')

# Tira o CSP que já estiver lá (nas duas formas), pra trocar de modo não empilhar dois.
antes = len(linhas)
linhas = (linhas[:inicio]
          + [l for l in linhas[inicio:fim] if 'Content-Security-Policy' not in l]
          + linhas[fim:])
removidas = antes - len(linhas)
fim -= removidas
if removidas:
    print(f'  {removidas} linha(s) de CSP anterior removida(s)')

if modo == 'remover':
    with open(caminho, 'w', encoding='utf-8') as f:
        f.writelines(linhas)
    print('  CSP removido do bloco.')
    sys.exit(0)

abre = next((i for i in range(inicio, fim) if linhas[i].strip() == 'header {'), None)
if abre is None:
    sys.exit('ERRO: o bloco não tem header { } — nada foi alterado.')
fecha = next((i for i in range(abre + 1, fim) if linhas[i].strip() == '}'), None)
if fecha is None:
    sys.exit('ERRO: não achei o fim do header — nada foi alterado.')

linhas.insert(fecha, f'\t\t{NOME} "{POLITICA}"\n')
with open(caminho, 'w', encoding='utf-8') as f:
    f.writelines(linhas)
print(f'  {NOME} inserido (linha {fecha + 1})')
PYTHON

echo
echo "== VALIDANDO ANTES DE RECARREGAR =="
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
echo "== TODOS OS DOMINIOS DO ARQUIVO CONTINUAM RESPONDENDO? =="
for D in $(grep -oE '^[a-z0-9.-]+\.[a-z]{2,3}(\.[a-z]{2})?' "$CADDYFILE" | sort -u); do
  printf "  %-32s %s\n" "$D" "$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "https://$D/" || echo FALHOU)"
done

echo
echo "== O CABECALHO QUE ESTA SAINDO AGORA =="
curl -s -o /dev/null -D - --max-time 15 "https://$BLOCO/" | grep -i "content-security" | cut -c1-70
