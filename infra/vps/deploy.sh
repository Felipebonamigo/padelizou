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

# ── UM DEPLOY POR VEZ ───────────────────────────────────────────────────────
# Dois deploys do mesmo ambiente ao mesmo tempo se destroem: o segundo apagava a pasta que
# o primeiro tinha acabado de pôr no ar, e o serviço reiniciava em cima de uma pasta pela
# metade. Aconteceu em 07/08/2026 no dev — o `.historico` registrou o mesmo build DUAS
# vezes no mesmo minuto e o start morreu com "static resources manifest não encontrado".
# Sobreviveu só porque o systemd tem Restart=always e a segunda tentativa pegou a pasta
# já refeita: um acidente com final feliz, não um projeto.
exec 9>"/var/lock/padelizou-deploy-$AMBIENTE.lock"
if ! flock -w 900 9; then
  echo "ERRO: já existe um deploy de $AMBIENTE em andamento (esperei 15 min)."
  echo "Se ninguém estiver publicando, veja quem segura: fuser -v /var/lock/padelizou-deploy-$AMBIENTE.lock"
  exit 1
fi

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

# ── 2. Baixa e desempacota NUMA PASTA DE MONTAGEM ───────────────────────────
# A versão é montada inteira ao lado e só depois entra em cena. Antes o script
# desempacotava direto no destino final, e o `rm -rf` da primeira linha apagava a pasta
# em uso quando o mesmo build era reinstalado (rollback pra versão atual, ou dois deploys
# seguidos) — o processo que estava rodando perdia os arquivos debaixo dele.
ATUAL=""
[ -L "$LIVE" ] && ATUAL=$(readlink -f "$LIVE" || true)

MONTAGEM="$RELEASES/.montando-$TAG-$$"
rm -rf "$MONTAGEM"
mkdir -p "$MONTAGEM"
# Sai limpo se o deploy morrer no meio: pasta de montagem não pode virar lixo permanente.
trap 'rm -rf "$MONTAGEM"' EXIT

curl -fL --retry 3 -o /tmp/padelizou-$TAG.tar.gz \
  "https://github.com/$REPO/releases/download/$TAG/padelizou.tar.gz"
tar -xzf /tmp/padelizou-$TAG.tar.gz -C "$MONTAGEM"
rm -f /tmp/padelizou-$TAG.tar.gz

# ── 3. Conecta os dados persistentes (fora das versões) ─────────────────────
rm -rf "$MONTAGEM/wwwroot/uploads"
ln -s "$SHARED/uploads" "$MONTAGEM/wwwroot/uploads"
mkdir -p "$MONTAGEM/App_Data"
rm -rf "$MONTAGEM/App_Data/GoogleTokens"
ln -s "$SHARED/GoogleTokens" "$MONTAGEM/App_Data/GoogleTokens"
rm -f "$MONTAGEM/appsettings.json"
ln -s "$SHARED/appsettings.json" "$MONTAGEM/appsettings.json"

# ── 4. ESPERA a versão estar INTEIRA antes de trocar ────────────────────────
# A espera é por uma CONDIÇÃO, não por um relógio: `sleep 5` seria um chute que às vezes
# é curto demais e sempre é lento demais. O que se confere é o que o app abre no start —
# foi justamente o manifesto de arquivos estáticos que faltou em 07/08/2026, e ele some
# calado: o pacote parece lá, o serviço sobe e morre em seguida.
for ARQUIVO in Padelizou.dll Padelizou.runtimeconfig.json Padelizou.staticwebassets.endpoints.json appsettings.json; do
  [ -e "$MONTAGEM/$ARQUIVO" ] || {
    echo "ERRO: pacote incompleto — falta $ARQUIVO. Nada foi trocado."
    exit 1
  }
done
# Empurra o que ainda estiver em cache de escrita pro disco antes de alguém ler.
sync

# ── 5. Troca a versão e reinicia ────────────────────────────────────────────
ANTERIOR=""
[ -L "$LIVE" ] && ANTERIOR=$(readlink "$LIVE")
[ -n "$ANTERIOR" ] && echo "$ANTERIOR" > "$RELEASES/.anterior"

DESTINO="$RELEASES/$TAG"
if [ "$ATUAL" = "$DESTINO" ]; then
  # Reinstalando a versão que JÁ está no ar: a pasta antiga não pode ser apagada enquanto
  # o processo a usa, então a nova entra com um nome próprio e a velha morre na limpeza.
  DESTINO="$RELEASES/$TAG+$(date +%H%M%S)"
fi
rm -rf "$DESTINO"
mv "$MONTAGEM" "$DESTINO"
trap - EXIT

ln -sfn "$DESTINO" "$LIVE"
systemctl restart "$SERVICO"

# ── 6. Confere a saúde; se falhar, volta sozinho ────────────────────────────
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

# ── 7. Limpa versões antigas (mantém as 3 últimas) ──────────────────────────
#
# Eram 5, e cada build ocupa ~530 MB: com prod e dev, a retenção sozinha segurava 5,2 GB —
# quase um terço do disco usado (medição de 11/08/2026, com o banco em 15 MB). Três é o que
# a gente de fato usa: a que está no ar, a de voltar atrás (o `rollback.sh` lê UMA, a do
# `.anterior`) e uma folga pra quando o rollback também não presta.
#
# ⚠️ A conta é POR AMBIENTE, e o `continue` abaixo é o que protege a versão no ar de ser
# apagada mesmo que ela caia fora das 3 mais recentes — pode acontecer depois de um rollback,
# quando a que está rodando é mais VELHA que as que ficaram no disco.
cd "$RELEASES"
ls -dt build-* 2>/dev/null | tail -n +4 | while read -r VELHA; do
  [ "$RELEASES/$VELHA" = "$(readlink "$LIVE")" ] && continue
  rm -rf "$RELEASES/$VELHA"
done
# Restos de montagem de deploys que morreram no meio (antes do `trap` existir, ou por
# kill -9). Só os que não são de agora: pasta com menos de uma hora pode ser de um deploy
# do outro ambiente rodando em paralelo.
find "$RELEASES" -maxdepth 1 -name '.montando-*' -type d -mmin +60 -exec rm -rf {} + 2>/dev/null || true

echo "==> Feito. $TAG no ar em $AMBIENTE (o app aplica as migrations sozinho no startup)."
