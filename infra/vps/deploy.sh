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

# ── CREDENCIAL DO GITHUB (opcional) ─────────────────────────────────────────
# Em 15/09/2026 o repositório passou alguns minutos como PRIVADO e este script morreu no ato:
# ele falava com o GitHub sem se identificar, e repositório privado devolve 404 pra quem não
# se identifica. Medido no mesmo release (`build-1450-7ec7a5d`): 404 privado, 206 público.
#
# E mesmo com o repositório PÚBLICO já havia um buraco: a API sem token dá 60 chamadas por
# HORA por IP, e o laço que espera o CI gerar o build de um sha faz até 60 chamadas em 10
# minutos. Um `deploy.sh dev <sha>` que espera até o fim gasta a cota inteira do VPS; o
# segundo deploy da mesma hora leva 403 em tudo. Com token são 5.000/hora.
#
# O token é OPCIONAL de propósito: sem ele o script funciona igual enquanto o repositório for
# público. Ele é o que faz o deploy parar de depender DISSO.
#
# Como criar: github.com/settings/personal-access-tokens → fine-grained, só este repositório,
# permissão **Contents: Read-only**. Nada além disso — este script só baixa release.
TOKEN="${GH_TOKEN:-${GITHUB_TOKEN:-}}"
TOKEN_ARQUIVO="$(dirname "$(readlink -f "$0")")/.github-token"
if [ -z "$TOKEN" ] && [ -r "$TOKEN_ARQUIVO" ]; then
  # `tr -d` porque o jeito natural de criar o arquivo (`echo`, ou colar num editor) deixa um
  # \n no fim, e um header terminado em \n vira requisição malformada em vez de 401 — falha
  # que não se parece nem um pouco com a causa.
  TOKEN=$(tr -d ' \t\n\r' < "$TOKEN_ARQUIVO")

  # Aviso, e não recusa: travar o deploy por causa do modo de um arquivo é pior que publicar
  # — quem está publicando às 2h resolve o modo depois, e o token continua alcançável por
  # quem já tem conta no servidor de qualquer jeito. O que não pode é ficar CALADO.
  MODO=$(stat -c %a "$TOKEN_ARQUIVO")
  case "$MODO" in
    600|400) ;;
    *) echo "AVISO: $TOKEN_ARQUIVO está $MODO — qualquer usuário do servidor lê o token. chmod 600 nele." ;;
  esac
fi

# O token NUNCA entra como argumento de comando: argumento é legível por qualquer usuário da
# máquina (`ps auxww`), e a app roda com outro usuário neste mesmo VPS. O `-K` lê o header de
# um arquivo 600. O arquivo é criado SEMPRE (vazio quando não há token) pra que exista um
# caminho só: `curl -K "$CURL_CFG"` em toda chamada, com ou sem credencial.
CURL_CFG=$(mktemp)
chmod 600 "$CURL_CFG"
trap 'rm -f "$CURL_CFG"' EXIT
if [ -n "$TOKEN" ]; then
  printf 'header = "Authorization: Bearer %s"\n' "$TOKEN" >> "$CURL_CFG"
fi

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

# 401, 403 e 404 têm três causas diferentes e uma cara só no `curl -fsS`: "error: 404". Pior,
# o `set -euo pipefail` derrubava o script na atribuição do TAG, então nem a mensagem
# "não encontrei build" que existe logo abaixo chegava a ser lida. Aqui cada recusa diz o que
# fazer, porque a próxima sessão não vai refazer a investigação de 15/09/2026.
api() {
  local corpo codigo
  corpo=$(mktemp)
  codigo=$(curl -sS -K "$CURL_CFG" -H "Accept: application/vnd.github+json" \
                -o "$corpo" -w '%{http_code}' "https://api.github.com/repos/$REPO/$1")

  case "$codigo" in
    200) cat "$corpo" ;;
    401) echo "ERRO: o GitHub recusou o token (401). Ele expirou ou está errado — veja $TOKEN_ARQUIVO." >&2 ;;
    403|429)
      if grep -q "rate limit" "$corpo"; then
        echo "ERRO: cota da API do GitHub estourada (rate limit). Sem token são 60 chamadas por hora" >&2
        echo "      por IP, e esperar o build de um sha gasta quase todas. Ponha um token em" >&2
        echo "      $TOKEN_ARQUIVO (Contents: Read-only) — com ele são 5.000/hora." >&2
      else
        echo "ERRO: o GitHub recusou o acesso (403). O token não alcança $REPO." >&2
      fi ;;
    404)
      echo "ERRO: o GitHub devolveu 404 em $1." >&2
      echo "      Se o repositório está privado, é ISSO: sem token ele não existe pra este servidor." >&2
      echo "      Crie um token (Contents: Read-only) e salve em $TOKEN_ARQUIVO." >&2 ;;
    *) echo "ERRO: o GitHub respondeu $codigo em $1." >&2 ;;
  esac

  rm -f "$corpo"
  [ "$codigo" = "200" ]
}

# ── 1. Descobre qual build instalar ─────────────────────────────────────────
TAG=""
if [ -z "$REF" ]; then
  TAG=$(api "releases?per_page=30" | grep -o '"tag_name": *"build-[^"]*"' | head -1 | sed 's/.*"\(build-[^"]*\)"/\1/')
elif [[ "$REF" == build-* ]]; then
  TAG="$REF"
else
  # Recebeu um sha: espera o CI gerar o build dele (até 10 min)
  #
  # ⚠️ A CADENCIA É 20s, E A CONTA IMPORTA: cada volta é UMA chamada à API, e a API sem token
  # dá 60 por HORA por IP. Em 10s eram 60 voltas — a cota inteira num deploy só, sem sobrar
  # nada nem pro passo seguinte, que também é chamada de API desde que o download passou a
  # sair do endpoint de asset. Em 20s são 30 voltas: mesma janela de 10 min, metade da cota, e
  # sobra pro download e pra um segundo deploy na mesma hora. Com token (5.000/hora) nada
  # disso pesa — a cadência existe pro caso SEM token continuar funcionando.
  SHA7="${REF:0:7}"
  for i in $(seq 1 30); do
    TAG=$(api "releases?per_page=30" | grep -o '"tag_name": *"build-[0-9]*-'"$SHA7"'"' | head -1 | sed 's/.*"\(build-[^"]*\)"/\1/') || true
    [ -n "$TAG" ] && break
    echo "  aguardando o CI gerar o build do commit $SHA7... ($i/30)"
    sleep 20
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
trap 'rm -rf "$MONTAGEM"; rm -f "$CURL_CFG"' EXIT

# ⚠️ POR QUE NÃO `github.com/<repo>/releases/download/<tag>/padelizou.tar.gz`: aquela é a URL
# de NAVEGADOR e só atende quem pode ver o repositório sem se identificar — em repositório
# privado ela devolve 404 com token ou sem, porque o token nem chega a ser considerado. O
# endpoint de asset da API atende as duas visibilidades (medido em 15/09/2026: 206 anônimo com
# o repo público, 200 com token) e é o documentado pra isso.
#
# ⚠️ E NADA de `--location-trusted`: o endpoint redireciona pro armazenamento de objetos, que é
# OUTRO host, e aquela opção reenviaria o `Authorization` pra lá. O redirecionamento já vem
# assinado — não precisa de credencial nenhuma, e entregá-la seria dar o token a um terceiro.
ASSETS=$(api "releases/tags/$TAG" | sed -n 's#.*"url": *"[^"]*/releases/assets/\([0-9]*\)".*#\1#p')
QUANTOS=$(printf '%s' "$ASSETS" | grep -c . || true)
if [ "$QUANTOS" != "1" ]; then
  echo "ERRO: o release $TAG tem $QUANTOS arquivo(s) anexado(s); esperava exatamente 1."
  echo "      O ci.yml anexa só o padelizou.tar.gz. Se passou a anexar mais de um, este"
  echo "      script precisa escolher pelo NOME — instalar 'o primeiro' seria sorteio."
  exit 1
fi

curl -fL --retry 3 -K "$CURL_CFG" -H "Accept: application/octet-stream" \
  -o /tmp/padelizou-$TAG.tar.gz "https://api.github.com/repos/$REPO/releases/assets/$ASSETS"
tar -xzf /tmp/padelizou-$TAG.tar.gz -C "$MONTAGEM"
rm -f /tmp/padelizou-$TAG.tar.gz
# Última chamada ao GitHub já foi: o token sai do disco agora, e não no fim do script —
# o `trap - EXIT` lá embaixo desarma a limpeza, e sem isto o arquivo ficaria pra trás.
rm -f "$CURL_CFG"

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
