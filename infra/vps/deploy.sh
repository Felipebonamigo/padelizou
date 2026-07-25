#!/usr/bin/env bash
# Deploy do Padelizou no VPS a partir dos builds do GitHub Actions.
# Fica em /opt/padelizou-deploy/deploy.sh no servidor.
#
# Uso:
#   deploy.sh prod            → instala o build mais recente
#   deploy.sh dev <sha>       → espera o CI do commit e instala aquele build
#   deploy.sh prod build-7-ab12cd3 → instala um build específico (serve de rollback também)
#
# O que ele garante:
#   - só instala pacote gerado pelo CI (testes verdes, por construção)
#   - cada versão fica guardada em /opt/padelizou-releases/<env>/
#   - uploads, tokens do Google e appsettings.json vivem FORA das versões
#     (em /opt/padelizou-shared/<env>/) — trocar de versão não apaga nada
#   - se o /healthz não responder 200 depois do restart, volta sozinho
set -euo pipefail

REPO="Felipebonamigo/padelizou"
AMBIENTE="${1:?Uso: deploy.sh <prod|dev> [tag|sha]}"
REF="${2:-}"

case "$AMBIENTE" in
  prod) SERVICO="padelizou";     LIVE="/opt/padelizou";     URL="https://padelizou.com.br/healthz" ;;
  dev)  SERVICO="padelizou-dev"; LIVE="/opt/padelizou-dev"; URL="https://dev.padelizou.com.br/healthz" ;;
  *) echo "ERRO: ambiente '$AMBIENTE' inválido (use prod ou dev)"; exit 1 ;;
esac

SHARED="/opt/padelizou-shared/$AMBIENTE"
RELEASES="/opt/padelizou-releases/$AMBIENTE"
mkdir -p "$RELEASES"

if [ -e "$LIVE" ] && [ ! -L "$LIVE" ]; then
  echo "ERRO: $LIVE ainda é uma pasta comum, não um atalho de versão."
  echo "Migre primeiro (mover dados pro shared e transformar em symlink)."
  exit 1
fi

api() { curl -fsS "https://api.github.com/repos/$REPO/$1"; }

# ── 1. Descobre qual build instalar ─────────────────────────────────────────
TAG=""
if [ -z "$REF" ]; then
  TAG=$(api "releases?per_page=30" | grep -o '"tag_name": *"build-[^"]*"' | head -1 | sed 's/.*"\(build-[^"]*\)"/\1/')
elif [[ "$REF" == build-* ]]; then
  TAG="$REF"
else
  # Recebeu um sha: espera o CI gerar o build dele (até 10 min)
  SHA7="${REF:0:7}"
  for i in $(seq 1 60); do
    TAG=$(api "releases?per_page=30" | grep -o '"tag_name": *"build-[0-9]*-'"$SHA7"'"' | head -1 | sed 's/.*"\(build-[^"]*\)"/\1/') || true
    [ -n "$TAG" ] && break
    echo "  aguardando o CI gerar o build do commit $SHA7... ($i/60)"
    sleep 10
  done
fi

if [ -z "$TAG" ]; then
  echo "ERRO: não encontrei build pra '$REF'."
  echo "O CI passou? Veja https://github.com/$REPO/actions"
  exit 1
fi
echo "==> Instalando $TAG no ambiente $AMBIENTE"

# ── 2. Baixa e desempacota ──────────────────────────────────────────────────
DESTINO="$RELEASES/$TAG"
rm -rf "$DESTINO"
mkdir -p "$DESTINO"
curl -fL --retry 3 -o /tmp/padelizou-$TAG.tar.gz \
  "https://github.com/$REPO/releases/download/$TAG/padelizou.tar.gz"
tar -xzf /tmp/padelizou-$TAG.tar.gz -C "$DESTINO"
rm -f /tmp/padelizou-$TAG.tar.gz

# ── 3. Conecta os dados persistentes (fora das versões) ─────────────────────
rm -rf "$DESTINO/wwwroot/uploads"
ln -s "$SHARED/uploads" "$DESTINO/wwwroot/uploads"
mkdir -p "$DESTINO/App_Data"
rm -rf "$DESTINO/App_Data/GoogleTokens"
ln -s "$SHARED/GoogleTokens" "$DESTINO/App_Data/GoogleTokens"
rm -f "$DESTINO/appsettings.json"
ln -s "$SHARED/appsettings.json" "$DESTINO/appsettings.json"

# ── 4. Troca a versão e reinicia ────────────────────────────────────────────
ANTERIOR=""
[ -L "$LIVE" ] && ANTERIOR=$(readlink "$LIVE")
[ -n "$ANTERIOR" ] && echo "$ANTERIOR" > "$RELEASES/.anterior"

ln -sfn "$DESTINO" "$LIVE"
systemctl restart "$SERVICO"

# ── 5. Confere a saúde; se falhar, volta sozinho ────────────────────────────
OK=""
for i in $(seq 1 30); do
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "$URL" || true)
  [ "$CODE" = "200" ] && { OK=1; break; }
  sleep 2
done

if [ -z "$OK" ]; then
  echo "ERRO: /healthz não respondeu 200 em 60s."
  if [ -n "$ANTERIOR" ] && [ -d "$ANTERIOR" ]; then
    echo "==> Voltando pra versão anterior: $ANTERIOR"
    ln -sfn "$ANTERIOR" "$LIVE"
    systemctl restart "$SERVICO"
    sleep 5
    CODE=$(curl -s -o /dev/null -w '%{http_code}' "$URL" || true)
    echo "==> Versão anterior respondeu: $CODE"
  fi
  exit 1
fi

echo "$(date '+%Y-%m-%d %H:%M') $TAG" >> "$RELEASES/.historico"

# ── 6. Limpa versões antigas (mantém as 5 últimas) ──────────────────────────
cd "$RELEASES"
ls -dt build-* 2>/dev/null | tail -n +6 | while read -r VELHA; do
  [ "$RELEASES/$VELHA" = "$(readlink "$LIVE")" ] && continue
  rm -rf "$RELEASES/$VELHA"
done

echo "==> Feito. $TAG no ar em $AMBIENTE (o app aplica as migrations sozinho no startup)."
