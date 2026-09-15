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
#   - funciona com o repositório público OU privado (token em /opt/padelizou-deploy/github-token)
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

# ── O ACESSO AO GITHUB ──────────────────────────────────────────────────────
# Este script fala com o GitHub em dois pontos — a lista de releases, pra descobrir a tag, e
# o download do pacote. Com o repositório PRIVADO, os dois respondem 404 sem credencial, e a
# publicação simplesmente para no clique de Settings → Change visibility.
#
# O token mora num ARQUIVO no servidor, e não em variável de ambiente, porque o deploy roda
# por dois caminhos que não herdam ambiente nenhum: o `ssh` do workflow e a mão no terminal.
#
# ⚠️ Sem o arquivo, o script funciona como sempre funcionou (repositório público). É de
# propósito: assim o token entra no servidor ANTES de virar a chave da visibilidade, e sai
# depois de ela voltar, sem existir um minuto em que o deploy não sai.
ARQUIVO_TOKEN="${PADELIZOU_ARQUIVO_TOKEN:-/opt/padelizou-deploy/github-token}"
TOKEN=""
# O `tr` tira QUALQUER espaço, e não só o \n final: arquivo criado do Windows vem com \r no
# fim, e um \r dentro do cabeçalho faz o GitHub recusar o token sem dizer por quê.
if [ -r "$ARQUIVO_TOKEN" ]; then TOKEN=$(tr -d '[:space:]' < "$ARQUIVO_TOKEN"); fi

# ⚠️ O token vai pela ENTRADA do curl (`-K -`), NUNCA em `-H` na linha de comando: argumento
# de processo se lê com `ps` de qualquer usuário da máquina; o arquivo, 600 e do root, não.
curl_github() {
  if [ -n "$TOKEN" ]; then
    printf 'header = "Authorization: Bearer %s"\n' "$TOKEN" | curl -K - "$@"
  else
    curl "$@"
  fi
}

api() { curl_github -fsS -H "Accept: application/vnd.github+json" "https://api.github.com/repos/$REPO/$1"; }

# ── 0. CONFERE O ACESSO ANTES DE PROCURAR QUALQUER BUILD ────────────────────
# 404 de repositório privado é o MESMO código de "esse build não existe". Sem esta
# conferência, um token vencido vira "não encontrei build pra ''" lá embaixo — mensagem que
# manda investigar o CI, que está verde, e some com a tarde de quem for atrás. Aqui em cima,
# antes de qualquer tag, o 404 só pode significar uma coisa.
CODIGO=$(curl_github -sS -o /dev/null -w '%{http_code}' "https://api.github.com/repos/$REPO" || echo 000)
case "$CODIGO" in
  200) ;;
  401|404)
    echo "ERRO: o GitHub respondeu $CODIGO para $REPO."
    if [ -z "$TOKEN" ]; then
      echo "  O repositório está privado? Então falta o token em $ARQUIVO_TOKEN."
      echo "  Como criar: seção 'Repositório privado' do infra/vps/README.md."
    else
      echo "  Existe token em $ARQUIVO_TOKEN, mas ele foi recusado — vencido, revogado, ou"
      echo "  sem a permissão 'Contents: Read' NESTE repositório."
    fi
    exit 1 ;;
  403)
    echo "ERRO: 403 do GitHub. Sem token o limite é 60 requisições por hora por IP."
    echo "  Se já existe token em $ARQUIVO_TOKEN, então ele está sem 'Contents: Read'."
    exit 1 ;;
  *)
    echo "ERRO: não consegui falar com a api.github.com (código $CODIGO). Rede do servidor?"
    exit 1 ;;
esac

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

# O anexo vem pelo endpoint de ASSET da API, e não pela URL de browser do release: aquela
# responde 404 em repositório privado MESMO COM TOKEN — ela existe só pra quem está
# deslogado. Como o endpoint da API serve os dois casos, aqui não há um "se privado": é um
# caminho só, que não tem como enferrujar enquanto o repositório for público.
#
# ⚠️ O `Accept: application/octet-stream` é o que faz a API mandar os BYTES. Sem ele vem o
# JSON que descreve o anexo, e o `tar` logo abaixo morre com "not in gzip format" — erro que
# não fala de permissão nenhuma e manda investigar o pacote, que está inteiro.
URL_ASSET=$(api "releases/tags/$TAG" \
  | tr -d '\n' | tr '{' '\n' \
  | grep '"name": *"padelizou\.tar\.gz"' \
  | grep -oE 'https://api\.github\.com/repos/[^"]+/releases/assets/[0-9]+' \
  | head -1 || true)

if [ -z "$URL_ASSET" ]; then
  echo "ERRO: o release $TAG existe, mas não tem o anexo padelizou.tar.gz."
  echo "  O passo 'Criar release de build' do ci.yml morreu no meio? Confira em"
  echo "  https://github.com/$REPO/releases/tag/$TAG"
  exit 1
fi

PACOTE="/tmp/padelizou-$TAG.tar.gz"
curl_github -fL --retry 3 -H "Accept: application/octet-stream" -o "$PACOTE" "$URL_ASSET"
tar -xzf "$PACOTE" -C "$MONTAGEM"
rm -f "$PACOTE"

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
