# App Android na Play Store

O app do Padelizou **é o próprio site** rodando dentro de uma casca chamada TWA (*Trusted Web
Activity*). Não existe um segundo código para manter: o que sobe na loja aponta para
`padelizou.com.br`, e todo deploy do site já atualiza o app.

Decisão de 11/08/2026: **Android sim, iPhone não** — US$ 25 uma vez contra US$ 99 por ano para
sempre. O PWA continua sendo o caminho no iPhone.

---

## O que já está pronto no repositório

| Peça | Onde | Situação |
|---|---|---|
| Manifest do PWA | `Padelizou/wwwroot/manifest.json` | Pronto: 192, 512, maskable, atalhos |
| Ícone da loja 512×512 | `loja-android/icone-512x512.png` | Pronto |
| Gráfico de destaque 1024×500 | `loja-android/destaque-1024x500.png` | Pronto |
| Capturas de tela | `loja-android/tela-*.png` | Válidas (ver ressalva no passo 5) |
| Digital Asset Links | `/.well-known/assetlinks.json` | Endpoint pronto, **esperando a impressão digital** |
| Política de privacidade | `https://padelizou.com.br/Home/Privacy` | No ar |
| Exclusão de conta | `https://padelizou.com.br/Auth/ExcluirConta` | No ar (a Play exige) |

---

## O que só o Felipe pode fazer

Eu não crio conta, não digito senha em formulário de serviço e não faço pagamento. Estes três
passos são seus:

1. Criar a conta no Play Console e pagar os **US$ 25**;
2. Guardar a senha da chave de assinatura;
3. Apertar o botão de publicar.

Todo o resto abaixo eu já deixei pronto ou posso executar.

---

## Passo 1 — A conta no Play Console (US$ 25, uma vez na vida)

`https://play.google.com/console/signup`

**A escolha do tipo de conta muda o caminho inteiro. Ela não pode ser trocada depois.**

| | Pessoal (CPF) | Organização (CNPJ) |
|---|---|---|
| Custo | US$ 25 | US$ 25 |
| Exige D-U-N-S | Não | **Sim** — grátis, mas leva ~28 dias |
| Teste fechado obrigatório | **Sim: 12 testadores por 14 dias corridos** | Não |
| Some na ficha pública | Seu nome e **seu endereço residencial** | Nome e endereço da empresa |

⚠️ **A ficha da loja mostra o endereço do desenvolvedor publicamente.** Como o CNPJ do MEI
provavelmente está registrado no endereço de casa, os dois caminhos podem terminar no mesmo
lugar. Confira o endereço do CNPJ antes de escolher — é o mesmo problema que a gente corrigiu na
tela de fatura, e aqui ele fica numa página pública que o Google indexa.

### ✅ Decidido em 11/08/2026: conta de ORGANIZAÇÃO

Conta Google: **`padelizou@gmail.com`** (dedicada, não a pessoal do Felipe).

A alternativa — conta pessoal, que começaria no mesmo dia — foi recusada: o app ficaria no CPF
em vez do CNPJ, e tirá-lo de lá depois exige transferência pelo suporte do Google. Os dois
caminhos terminavam em prazo parecido (~4 semanas contra ~6), então o que decidiu foi de quem é
o app, não a pressa.

**Consequência aceita:** o cadastro fica **parado na tela "Perfil de pagamentos"** até o D-U-N-S
chegar. Não dá pra pagar, criar o app nem subir pacote antes disso.

### O nome legal não é "Bonamigo Systems"

Conferido no Redesim em 11/08/2026:

| | |
|---|---|
| **Razão social** (vai no Google e na D&B) | `68.185.754 FELIPE CARBONI BONAMIGO` |
| Nome fantasia | Bonamigo Systems |
| Situação | ATIVA · RS |

É o padrão do MEI: os dígitos do CNPJ seguidos do nome. **Procurar ou cadastrar como "Bonamigo
Systems" não acha nada** — foi por isso que a busca no Dunsguide voltou vazia até usarmos o nome
certo. O nome público na loja pode ser "Padelizou"; isso é outro campo.

### O D-U-N-S

Não existe (confirmado no Dunsguide em 11/08 — MEI aberto em 23/07, novo demais pra estar na
base). **Não existe fila especial do Google**, como existe pra Apple: o pedido é o padrão da
Dun & Bradstreet, que no Brasil é a **CIAL** (`pt.cialdnb.com`).

⚠️ **A CIAL oferece duas velocidades e não mostra preço em nenhuma**: "24 a 48 horas" é serviço
**pago**; **"até 30 dias úteis" é a gratuita** — ~6 semanas. Escolher errado aqui custa dinheiro
sem necessidade.

⚠️ **Se for mudar o endereço do MEI, mude ANTES de pedir.** Depois cria divergência com o que a
D&B já gravou, e a verificação recomeça.

---

## Passo 2 — Gerar o pacote com Bubblewrap

O Bubblewrap é a ferramenta oficial do Google que transforma um PWA em pacote Android. É grátis.

```bash
npm install -g @bubblewrap/cli@latest
```

Na primeira execução ele baixa sozinho o JDK e o Android SDK de que precisa.

```bash
bubblewrap init --manifest https://padelizou.com.br/manifest.json
```

Ele lê o manifest e pergunta o resto. As respostas que importam:

- **Domain:** `padelizou.com.br`
- **Application name:** `Padelizou`
- **Short name:** `Padelizou` (máximo 12 caracteres, aparece embaixo do ícone)
- **Application ID / package:** `br.com.padelizou.app`
- **Start URL:** `/`
- **Status bar color:** `#141d33`

⚠️ **O nome do pacote é definitivo.** Depois do primeiro envio o Google não deixa trocar — seria
outro app, com outra ficha e outros instaladores. Se mudar de ideia sobre
`br.com.padelizou.app`, mude **agora**. O mesmo valor está no padrão de
`Padelizou/Services/AndroidSettings.cs` e precisa bater com o que você responder aqui.

```bash
bubblewrap build
```

Sai um `app-release-bundle.aab` — é esse arquivo que sobe na loja.

**Antes de subir, confira o `targetSdkVersion`.** Desde 31/08/2026 o Google só aceita app que
mire **Android 16 (API 36)**. Abra o `app/build.gradle` que o Bubblewrap gerou e procure
`targetSdkVersion`. Se estiver abaixo de 36, atualize o Bubblewrap e gere de novo. É por isso
que usamos o Bubblewrap local e **não** o PWABuilder na nuvem, que gerava API 35.

### A chave de assinatura

O `bubblewrap init` cria um `android.keystore` e pede uma senha.

🚨 **Perdeu o keystore ou a senha, perdeu a capacidade de atualizar o app.** Guarde os dois no
mesmo lugar onde está a chave do backup (`/root/padelizou-chave-backup.txt` tem o mesmo tipo de
risco). **Não commite o keystore neste repositório — ele é público.**

Aceite o **Play App Signing** quando o console oferecer. Com ele o Google guarda a chave final e
a sua vira só "chave de upload", que pode ser trocada se você perder. É a rede de segurança.

---

## Passo 3 — Ligar o assetlinks.json (o passo que ninguém lembra)

Sem este passo o app abre **com a barra de endereço do Chrome por cima**. Ele funciona, mas
deixa de parecer app — que é a única coisa que a loja compra. E a falha é muda: nada quebra,
nada aparece no log.

São **duas** impressões digitais, não uma:

1. A da sua chave de upload — `bubblewrap fingerprint list`, ou
   `keytool -list -v -keystore android.keystore -alias android`;
2. A que o Google gera ao reassinar — Play Console → **Configuração → Integridade do app →
   Assinatura de apps**. Copie os dois SHA-256 de lá.

Configurar só a primeira faz o app funcionar no seu teste e falhar para quem instalou pela
loja. É o pior jeito de descobrir.

No servidor, drop-in do systemd (não precisa republicar o site):

```bash
sudo mkdir -p /etc/systemd/system/padelizou.service.d
sudo tee /etc/systemd/system/padelizou.service.d/android.conf > /dev/null <<'EOF'
[Service]
Environment=Android__PackageName=br.com.padelizou.app
Environment=Android__Sha256Fingerprints=AA:BB:...,CC:DD:...
EOF
sudo systemctl daemon-reload && sudo systemctl restart padelizou
```

⚠️ **Crie um arquivo `android.conf` novo — não escreva por cima de `email.conf` nem do arquivo
principal.** Outras sessões já colocaram configuração nessa pasta.

Conferir:

```bash
curl -s https://padelizou.com.br/.well-known/assetlinks.json
```

Tem que sair a lista com as duas impressões digitais. Se sair **404**, nada foi configurado. Se
faltar uma, o app reclama no log do serviço dizendo qual valor foi ignorado.

O caminho `/.well-known` já está liberado no portão de Acesso Antecipado. Isso importa: se você
religar o portão pelo painel do admin, o robô do Google levaria um 302 e a verificação
quebraria — em todos os celulares de uma vez. Existe teste guardando isso
(`Padelizou.Tests/AppAndroidTests.cs`).

---

## Passo 4 — Texto da ficha da loja

**Nome do app** (máx. 30): `Padelizou`

**Descrição curta** (máx. 80):

```
Torneios, aulas e ranking de padel — tudo em um lugar só.
```

**Descrição completa** (máx. 4000):

```
O Padelizou junta num app só tudo o que acontece no seu padel.

TORNEIOS
Ache torneios com inscrição aberta perto de você, inscreva sua dupla e acompanhe a chave
em tempo real. Grupos, mata-mata, americano individual ou de duplas — cada formato com as
regras certas. Quem organiza monta o torneio, sorteia as chaves e controla os jogos pela
mesa de controle, do próprio celular, na beira da quadra.

JOGOS E PARCEIROS
Ache parceiro pelo nível, pela cidade ou pelo clube. Monte a panelinha, marque o jogo,
confirme presença e divida o custo da quadra sem planilha e sem discussão no grupo.

AULAS
Encontre professores na sua cidade, veja os horários livres e marque a aula. Professores
acompanham alunos, pacotes e agenda no painel deles.

RANKING
Cada jogo disputado move o seu ranking e o Padelímetro, que mede sua evolução ao longo do
tempo. Ranking por categoria, por cidade e por time.

SEU PERFIL
Histórico de partidas, elogios de outros jogadores, evolução e conquistas.

Criar conta é grátis.
```

Nada de mencionar o meio de pagamento pelo nome — nem aqui, nem em tela nenhuma. Existe teste
para isso.

**Categoria:** Esportes
**E-mail de contato:** o mesmo de `Suporte__Email`
**Política de privacidade:** `https://padelizou.com.br/Home/Privacy`

---

## Passo 5 — As imagens

Já estão em `loja-android/`:

- `icone-512x512.png` — ícone do app, 32 bits com transparência ✅
- `destaque-1024x500.png` — gráfico de destaque, 24 bits sem canal alfa ✅
- `tela-1-inicio.png`, `tela-2-torneios.png`, `tela-3-buscar.png` — capturas ✅

⚠️ **Ressalva sobre as capturas:** elas têm 504×1000. Está **dentro** do que a Play aceita
(mínimo 320, máximo 3840, proporção até 2:1), então dá para publicar assim. Mas a loja mostra
elas grandes e nessa resolução elas ficam moles.

**O melhor caminho é você tirar capturas novas no seu próprio celular** depois que o app
estiver instalado pelo teste interno: sai na resolução nativa do aparelho, mostra o app de
verdade — que é o que a política da Play pede — e leva uns 5 minutos. Telas que valem a pena:
início, um torneio com a chave montada, ranking e o seu perfil.

---

## Passo 6 — Formulário de Segurança dos Dados

O Google pergunta item a item o que o app coleta. **Este é um rascunho tirado da nossa política
de privacidade** (`Views/Home/Privacy.cshtml`) — confira cada linha no console antes de enviar,
porque declaração errada aqui derruba o app depois.

Vale para tudo: **coletado sim, vendido nunca**, trafega **cifrado (HTTPS)** e o usuário
**pode pedir exclusão** (`/Auth/ExcluirConta`).

| Categoria da Play | Coleta? | O quê |
|---|---|---|
| Informações pessoais → Nome | Sim | Nome e apelido |
| Informações pessoais → E-mail | Sim | Login e avisos |
| Informações pessoais → Telefone | Sim | Celular |
| Informações pessoais → Endereço | Sim | Cidade, estado e, se preenchido, CEP/rua/bairro |
| Informações pessoais → Outras | Sim | **CPF** (identifica o jogador entre torneios), sexo, lado da quadra |
| Informações financeiras → Histórico de compras | Sim | Valor, situação e data. **Dados de cartão nunca passam por nós** |
| Fotos e vídeos → Fotos | Sim | Foto de perfil, se enviada |
| Conteúdo do usuário → Outros | Sim | Comentários, elogios e avaliações |
| App: atividade e desempenho | Sim | Registro de erro, IP e navegador — segurança e diagnóstico |
| IDs de dispositivo | Sim | Endereço da inscrição de notificação, **só se a pessoa autorizar** |
| Localização precisa ou aproximada | **Não** | A cidade é digitada, não vem do GPS |

**Compartilhamento com terceiros** (item 5 da política): meio de pagamento, ViaCEP, Mundo do
Atleta (ranking estadual), equipe do ranking parceira, Google (e-mail e agenda) e WhatsApp.
Alguns entram como "prestador de serviço" e não como compartilhamento — leia a definição do
próprio console na hora de responder.

---

## Passo 7 — Publicar

1. Criar o app no console (nome, idioma pt-BR, "App", "Gratuito");
2. Subir o `.aab` no **teste interno** primeiro — instala na hora, sem espera, e é aí que você
   confirma que **a barra de endereço não aparece**. Se aparecer, é o passo 3;
3. Preencher ficha, imagens, classificação de conteúdo e segurança dos dados;
4. Conta pessoal: rodar o teste fechado com 12 testadores por 14 dias corridos;
5. Enviar para revisão. Costuma levar de alguns dias a duas semanas na primeira vez.

---

## Manutenção

- **Todo ano, por volta de agosto**, o Google sobe a API mínima. Em 2026 é a 36. Deixar passar
  não derruba o app do ar, mas trava as atualizações.
- **Atualizar o site já atualiza o app** — o conteúdo vem do servidor. Só é preciso gerar
  pacote novo quando muda ícone, nome, cor da barra ou a API alvo.
- **O US$ 25 não se repete.** Não existe renovação anual no Google.

## Armadilhas conhecidas

1. **Barra de endereço aparecendo** = assetlinks. Ou está 404, ou falta a impressão digital do
   Google (não só a sua), ou o portão de Acesso Antecipado foi religado.
2. **Keystore perdido** = fim das atualizações. Faça backup fora da máquina, hoje.
3. **Nome do pacote** não muda depois do primeiro envio.
4. **O Google não leva porcentagem** das inscrições: a política de pagamentos isenta serviço do
   mundo real e cita ingresso de evento ao vivo. O meio de pagamento atual continua como está.
   Isso mudaria se um dia vendêssemos assinatura digital dentro do app.
