# Publicar o Padelizou pelo GitHub (dá pra fazer do celular)

Os scripts desta pasta (`deploy.sh`, `rollback.sh`, `backup.sh`, `backup-offsite.sh`) moram
no VPS e continuam funcionando do jeito de sempre, por SSH.

| Arquivo aqui | Onde fica no servidor | Quando roda |
|---|---|---|
| `deploy.sh` · `rollback.sh` | `/opt/padelizou-deploy/` | sob demanda |
| `backup.sh` | `/usr/local/bin/backup-padelizou.sh` | cron, 4h00 — cópia local |
| `backup-offsite.sh` | `/usr/local/bin/backup-drive.sh` | cron, 4h30 — cópia FORA do servidor |

⚠️ O nome no servidor ainda é `backup-drive.sh` por motivo histórico: desde 07/08/2026 ele
manda pro **Backblaze B2** (principal, chave que não expira) **e** pro Google Drive (reserva).
O cron aponta pro nome antigo — renomear exigiria mexer no cron pra ganhar só estética.

O que mudou: o workflow `.github/workflows/deploy.yml` chama esses mesmos scripts
por você. Assim dá pra publicar do celular — app do GitHub → **Actions** → **Deploy**
→ **Run workflow** — sem precisar de terminal.

Nada de novo entra no caminho do código: o pacote continua saindo do `ci.yml`, e o
`deploy.sh` continua só instalando release gerado pelo CI. Publicar pelo celular não
cria atalho pra subir código que não passou nos testes.

## O formulário

| Campo | O que faz |
|---|---|
| **ambiente** | `dev` ou `prod`. O `prod` para e espera sua aprovação (veja abaixo). |
| **acao** | `deploy` publica; `rollback` volta pra versão anterior. |
| **build** | Vazio = o build mais recente. Ou `build-12-ab12cd3`, ou o sha de um commit (aí ele espera o CI daquele commit ficar pronto). |

## Configuração — uma vez só

Enquanto isso não estiver feito, o workflow falha no primeiro passo. É de propósito:
melhor falhar do que publicar com o acesso mal configurado.

### 1. Uma chave SSH só pra isso

Não reaproveite a sua chave pessoal — esta aqui você quer poder revogar sozinha, sem
perder o seu próprio acesso. Na sua máquina:

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/padelizou_deploy -N ""
```

Autorize a pública no VPS. Dois caminhos, escolha um:

**Pelo painel da Hostinger** — hPanel → VPS → **Chave SSH → Gerenciar**, e cole o
conteúdo de `~/.ssh/padelizou_deploy.pub`. É o caminho que funciona até do celular.

**Pelo terminal:**

```bash
ssh root@SEU_IP "cat >> ~/.ssh/authorized_keys" < ~/.ssh/padelizou_deploy.pub
```

Só pelo terminal dá pra colocar o prefixo `restrict,` na frente dessa linha do
`authorized_keys`. Ele desliga port forwarding, agente e tty — coisas que um deploy não
usa e que só serviriam pra outra pessoa se a chave vazasse:

```
restrict ssh-ed25519 AAAA... github-actions-deploy
```

Opcional: sem ele a chave fica um pouco mais poderosa do que precisa, e nada mais.

### 2. Os secrets

Em **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Conteúdo |
|---|---|
| `VPS_SSH_KEY` | O conteúdo do arquivo **privado** `~/.ssh/padelizou_deploy` (inteiro, com as linhas `BEGIN`/`END`) |
| `VPS_HOST` | O IP ou hostname do VPS |
| `VPS_KNOWN_HOSTS` | **Opcional.** A saída de `ssh-keyscan -t ed25519 SEU_IP` |

O `VPS_KNOWN_HOSTS` fixa a chave pública do servidor: com ele, se algo se passar pelo
VPS, o ssh recusa a conexão em vez de entregar o acesso. Rode o `ssh-keyscan` de uma rede
em que você confia.

**Ele é opcional.** Sem o secret, o workflow busca a chave do servidor na hora do deploy —
confia no primeiro contato em vez de conferir contra algo sabido. É mais fraco, e é o
padrão da maioria dos deploys por CI. A troca existe por um motivo prático: pegar o valor
exigia ENTRAR no servidor, e esse era o único passo da configuração que não dava pra fazer
do computador. Com dois secrets (IP e chave privada) o deploy já sai — e um deploy
configurado protege mais que um pino que ninguém chegou a configurar.

O usuário não precisa de configuração: o workflow já usa `root`, que é o do VPS. Se um
dia isso mudar, crie a **variável** (não secret) `VPS_USER` com o nome do novo — e
garanta que ele consegue rodar `systemctl restart` e escrever em `/opt`.

### O mínimo pra funcionar

Dois secrets, os dois preenchíveis sem abrir o servidor:

```powershell
# 1) VPS_HOST  →  179.197.233.184
# 2) VPS_SSH_KEY
Get-Content "$HOME\.ssh\padelizou_deploy" -Raw | Set-Clipboard
```

Com esses dois o deploy já sai. O `VPS_KNOWN_HOSTS` fica pra quando sobrar tempo.

### Atalho: gerar todos os valores de uma vez

**No Linux ou no Mac:**

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/padelizou_deploy -N ""

echo "=== VPS_HOST ==="; echo "179.197.233.184"
echo "=== VPS_KNOWN_HOSTS ==="; ssh-keyscan -t ed25519 179.197.233.184
echo "=== VPS_SSH_KEY ==="; cat ~/.ssh/padelizou_deploy
echo "=== cole esta no hPanel (Chave SSH → Gerenciar) ==="; cat ~/.ssh/padelizou_deploy.pub
```

**No Windows (PowerShell)** — duas diferenças que fazem o comando de cima falhar:

O `-N ""` não funciona: o PowerShell descarta string vazia ao passar pra programa
nativo, e o ssh-keygen reclama que falta o argumento. E o `~` só é expandido pelos
cmdlets, não pelo ssh-keygen — por isso use `$HOME`.

```powershell
ssh-keygen -t ed25519 -C "github-actions-deploy" -f "$HOME\.ssh\padelizou_deploy"
# pede passphrase duas vezes: dê Enter nas duas. A chave TEM que ser sem senha,
# senão o GitHub Actions fica esperando alguém digitar.

Get-Content "$HOME\.ssh\padelizou_deploy"        # → VPS_SSH_KEY
Get-Content "$HOME\.ssh\padelizou_deploy.pub"    # → cole no hPanel
```

O `ssh-keyscan` que vem no Windows é mais antigo que os algoritmos que o servidor
oferece e morre com `choose_kex: unsupported KEX method sntrup761x25519-sha512`.
Em vez de brigar com ele, pegue a chave na fonte — pelo Terminal do hPanel:

```bash
echo "179.197.233.184 $(cut -d' ' -f1,2 /etc/ssh/ssh_host_ed25519_key.pub)"
```

A linha que sair é o conteúdo do `VPS_KNOWN_HOSTS`. É a mesma coisa que o ssh-keyscan
traria, só que sem depender da rede — o servidor está lendo a própria chave.

### 3. Os environments — é aqui que mora a trava do prod

Em **Settings → Environments**, crie dois:

- **`dev`** — sem regra nenhuma. Deploy sai direto.
- **`prod`** — marque **Required reviewers** e coloque você mesmo.

Com isso, um deploy em produção fica pendente e você recebe a notificação; um toque
em *Approve* libera. São três segundos, e é o que separa "publiquei em produção" de
"publiquei em produção sem querer".

> ⚠️ **Crie o environment `prod` antes do primeiro deploy em produção.** Se ele não
> existir, o GitHub cria sozinho na primeira execução — sem regra nenhuma, e aí o
> deploy sai direto. A trava não vem do arquivo `deploy.yml`, vem daqui.

## Quando algo dá errado

O job fica vermelho quando o script remoto falha, e o log traz a saída dele inteira.

Vale lembrar do que o `deploy.sh` já faz sozinho: se o `/healthz` não responder 200 em
60 segundos depois do restart, ele volta pra versão anterior por conta própria. Job
vermelho normalmente quer dizer "a versão nova não subiu **e** a antiga já está de
volta no ar" — não "o site está fora".

Pra voltar mais longe do que a última versão, dispare o workflow com **acao: deploy** e
o `build-N-sha` que você quer. Os cinco últimos ficam no disco do VPS e todos seguem
guardados nos releases do GitHub.
