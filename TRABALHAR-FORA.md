# Trabalhar no Padelizou longe do desktop

Escrito em 31/07/2026, antes de uma viagem: a ideia é conseguir alterar e publicar
tendo só o celular e o notebook.

Este arquivo está no repositório de propósito. Se o Claude, o notebook ou a sessão
sumirem, ele continua acessível pelo GitHub no navegador do celular.

## Retomar a conversa com o Claude

As sessões do Claude Code na web ficam na sua conta, não na máquina. Abra
**claude.ai/code** de qualquer aparelho logado e a conversa continua de onde parou —
celular, notebook, computador emprestado.

O que **não** sobrevive é o container: depois de um tempo parado ele é reciclado e o
repositório é clonado de novo. A conversa fica, os arquivos não commitados não. Por
isso a regra é chata mas simples: **o que importa, commite e dê push.**

## O mapa

| O quê | Onde |
|---|---|
| Repositório | github.com/Felipebonamigo/padelizou (público) |
| Workflow de publicação | Actions → **Deploy** |
| Testes automáticos | Actions → **CI** (roda sozinho a cada push e PR) |
| Configuração do deploy | `infra/vps/README.md` |
| Rodar na sua máquina | `AMBIENTE-LOCAL.md` |
| Produção | https://padelizou.com.br · serviço `padelizou` |
| Homologação | https://dev.padelizou.com.br · serviço `padelizou-dev` |
| Scripts no VPS | `/opt/padelizou-deploy/{deploy,rollback,backup}.sh` |

## Antes de sair — só dá pra fazer no desktop

Enquanto isto não estiver feito, publicar de fora não funciona.

- [ ] **Copiar a chave SSH do VPS pro notebook.** Não é mais questão de vida ou morte
      (o hPanel tem terminal no navegador, veja "Plano B"), mas é o jeito confortável
      de mexer no servidor.
- [ ] **Copiar o `appsettings.json` pro notebook** (`Padelizou/appsettings.json`). É
      git-ignored porque guarda os segredos de verdade, então não vem pelo `git clone` —
      sem ele a app não sobe local.
- [ ] **Criar a chave de deploy e os secrets** — passo a passo em `infra/vps/README.md`.
- [ ] **Criar os environments `dev` e `prod`**, com *required reviewers* no `prod`.
      Se o `prod` não existir, o GitHub cria sozinho na primeira execução, sem trava.
- [ ] **Conferir que o notebook dá `git push`** no repositório. Descobrir isso longe
      de casa é ruim.
- [ ] **Testar um deploy no `dev` pelo celular, com o desktop ainda ligado.** Se algo
      estiver errado, você conserta em cinco minutos em vez de reconstruir tudo de longe.

## Publicar de fora

**Pelo app do GitHub** (funciona sempre, sem depender de mais nada):
Actions → Deploy → Run workflow → escolha `dev` ou `prod` → Run.

Deploy em `prod` fica pendente esperando aprovação. Chega notificação; um toque em
*Approve* libera.

**Pelo Claude:** peça em claude.ai/code — "publica o último build no dev" — que ele
dispara o workflow.

**Deu ruim?** Actions → Deploy → Run workflow → **acao: rollback**. Volta pra versão
anterior.

Vale lembrar que o `deploy.sh` já se defende sozinho: se o `/healthz` não responder 200
em 60 segundos depois do restart, ele volta pra versão anterior por conta própria. Job
vermelho quase sempre quer dizer "a versão nova não subiu **e** a antiga já voltou",
não "o site está fora".

## Plano B: sem GitHub Actions

Se o workflow quebrar e você precisar publicar assim mesmo:

```bash
ssh root@179.197.233.184
/opt/padelizou-deploy/deploy.sh dev      # ou prod
/opt/padelizou-deploy/rollback.sh prod   # se precisar voltar
```

É exatamente o que o workflow faz — ele só chama esses mesmos scripts.

**E se você não tiver a chave SSH à mão?** O hPanel da Hostinger tem um botão
**Terminal** que abre um shell do VPS dentro do navegador: hpanel.hostinger.com → VPS →
Terminal. Funciona no celular. Os mesmos comandos acima rodam ali.

Se nem a senha do root você tiver, o hPanel também faz **Reset password**. Ou seja: são
três caminhos independentes até o servidor (chave SSH, terminal do painel, reset de
senha), e perder um não te deixa sem nada.

## Alterar código de fora

Pelo celular ou pelo notebook, em claude.ai/code: peça a mudança, ele commita numa
branch e abre PR. O CI roda sozinho e você vê ✅ ou ❌ na tela.

As sessões da web já vêm com o .NET 10 instalado (`.claude/hooks/session-start.sh`),
então o Claude compila e roda os 1000+ testes antes de commitar — você não depende só
do CI pra saber que quebrou.

O que ele **não** consegue de lá: entrar no VPS, ler os bancos de produção ou dev, e
usar os segredos do `appsettings.json`. Integrações com Asaas, e-mail e push só dá pra
conferir de verdade publicando no `dev`.

## Ver a UI de dentro da sessão web (10/09/2026)

Dá — e por meses a gente achou que não dava: dezenas de entradas do `STATUS.md` fecham
com "⚠️ NÃO RODEI A UI". O container da sessão web tem **PostgreSQL 16** e **Chromium com
Playwright** instalados. O app sobe local, com dados de demonstração, e o Claude clica na
tela de verdade — foi assim que a tabela do palpitrômetro apareceu com uma fileira de
zeros que nenhum teste da suíte pegaria.

```bash
pg_ctlcluster 16 main start
su postgres -c "psql -c \"ALTER USER postgres WITH PASSWORD 'postgres'\" -c 'CREATE DATABASE db_padel_local'"

cd Padelizou && ASPNETCORE_ENVIRONMENT=Development \
  ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=db_padel_local;Username=postgres;Password=postgres" \
  dotnet run -c Release            # aplica as migrations e semeia o DadosDemo sozinho
```

O Playwright fica em `/opt/node22/lib/node_modules/playwright` e o Chromium em
`/opt/pw-browsers/chromium-1194/chrome-linux/chrome` (o caminho tem o número da build —
`ls /opt/pw-browsers` antes de fixar no script). Um `.mjs` de 30 linhas navega, tira
print e imprime o texto da tela.

⚠️ **O DadosDemo nasce raso** — 5 torneios e 2 partidas, sem palpite nenhum. Pra conferir
uma tela específica, o caminho curto é `INSERT` direto no banco local (as tabelas são
singulares: `"Torneio"`, `"Partida"`, `"PalpitePartida"`).

⚠️ **Login local**: os jogadores do demo nascem sem senha (pré-cadastro). Gerar um hash do
Identity num teste descartável (`new PasswordHasher<Jogador>().HashPassword(...)`) e
carimbar no `SenhaHash` é mais rápido do que preencher o formulário de cadastro inteiro.

⚠️ **Isto não substitui o `dev`**: aqui não há Asaas, e-mail, push nem os dados de
verdade. O que se prova é layout, clique, JavaScript e o que o Razor de fato desenhou.
