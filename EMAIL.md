# E-mail próprio: `@padelizou.com.br`

Como sair do `padelizou@gmail.com` e passar a receber **e** enviar pelo domínio, de graça.

Escrito em 09/08/2026, no dia em que a cota do Gmail estourou: o disparo de "Novo torneio
aberto" (87 e-mails) queimou o limite e **130 e-mails morreram calados**, duas recuperações de
senha entre eles — um jogador ficou sem conseguir entrar e ninguém soube até horas depois.

---

## São dois serviços, porque são dois problemas

Encaminhador não envia; serviço de envio não guarda caixa. Ninguém vende as duas metades junto
de graça, então:

| | Serviço | Custo | Teto |
|---|---|---|---|
| **Receber** (`contato@`) | Cloudflare Email Routing | R$ 0 | sem limite de volume; 200 apelidos |
| **Enviar** (app e você) | Resend, plano grátis | R$ 0 | **100/dia**, 3.000/mês, 1 domínio |

É a mesma planta que o **atomatiza.com.br** já roda em produção — dá pra conferir a qualquer
momento com `nslookup -type=MX atomatiza.com.br`.

### Por que não os outros

**Amazon SES**: é o que roda por baixo do próprio Resend (o MX de envio do Atomatiza aponta pra
`feedback-smtp.sa-east-1.amazonses.com`). Sairia por ~US$ 0,16/1.000 — centavos no nosso
volume —, mas **exige cartão**, começa num sandbox de 200/dia só pra endereços verificados, e a
liberação leva de 1 a 3 dias úteis. Só passa a valer a pena acima de 3.000/mês.

**Brevo**: 300/dia, melhor teto, mas carimba o logo deles em todo e-mail que sai. Inaceitável
pra um produto que cobra do cliente.

**ImprovMX**: encaminha sem exigir troca de nameserver (seria mais fácil que o Cloudflare), mas
tem 25 apelidos e 500 encaminhamentos/dia, e é mais um fornecedor pra manter. Fica como plano B
se a troca de nameserver der errado.

---

## Cabe no grátis por causa dos cortes de 09/08

Nove avisos deixaram de mandar e-mail nesse dia (`AlcanceDoAviso.AppSemEmail` — push e caixa de
entrada continuam). O que mudou:

| | Antes | Depois |
|---|---|---|
| Pico de um dia | 150+ | **~40 a 60** |
| Estimativa mensal | ~1.000 | **~300 a 500** |

⚠️ **O que importa não é o tamanho do corte, é a FORMA dele**: nenhum aviso é mais proporcional
ao tamanho da base. O maior disparo possível hoje é "chaves saíram" de um torneio grande, que
cresce com o torneio e não com quantas pessoas se cadastraram. Era o "Novo torneio aberto" (87,
subindo a cada cadastro novo) que obrigava a pensar em plano pago.

---

## Passo a passo

Cada conta é criada por você — não dá pra terceirizar cadastro nem senha.

### 0. Desligar o DNSSEC no Registro.br ⚠️

**Este é o único passo perigoso do roteiro, e ele vem primeiro.** O `padelizou.com.br` tem
DNSSEC ligado (registro DS publicado). Trocar os nameservers com o DS no ar derruba o **domínio
inteiro de uma vez** — site, `admin.` e `dev.` —, e o erro é de validação de DNS, que quase não
parece erro de DNS.

No painel do Registro.br, desligar o DNSSEC do domínio e **esperar o DS sumir** antes de seguir:

```bash
nslookup -type=DS padelizou.com.br 8.8.8.8
```

Só continue quando não voltar mais registro DS. Pode levar algumas horas.

### 1. Criar o domínio no Cloudflare e conferir os registros A

Criar conta grátis no Cloudflare, adicionar `padelizou.com.br`. Ele importa a zona atual
sozinho — **confira antes de seguir** que estes quatro existem, todos apontando pra
`179.197.233.184`:

| Nome | Tipo | Valor |
|---|---|---|
| `@` | A | 179.197.233.184 |
| `www` | A | 179.197.233.184 |
| `admin` | A | 179.197.233.184 |
| `dev` | A | 179.197.233.184 |

⚠️ **Os quatro têm que ficar com a nuvem CINZA (DNS only), não laranja.** Com o proxy ligado, o
Cloudflare intercepta o tráfego e o Caddy perde o desafio HTTP-01 que ele usa pra renovar o
certificado sozinho — o site continua no ar até o certificado atual vencer, e então cai. É o
tipo de defeito que aparece 60 dias depois, quando ninguém lembra mais desta mudança.

### 2. Trocar os nameservers no Registro.br

O Cloudflare mostra dois nameservers dele. Trocar no Registro.br e esperar propagar:

```bash
nslookup -type=NS padelizou.com.br 8.8.8.8
```

Enquanto não aparecerem os do Cloudflare, não siga. Confira que o site continua abrindo.

### 3. Ligar o Email Routing (receber)

No painel do Cloudflare → Email → Email Routing. Ele cria os MX e o SPF sozinho.

Criar os endereços, todos encaminhando pra `padelizou@gmail.com`:

- `contato@padelizou.com.br` — o endereço público, o que vai no site
- `suporte@padelizou.com.br`
- `financeiro@padelizou.com.br` — pro meio de pagamento, nota fiscal, MEI

Não crie `nao-responda@` — esse só envia, nunca recebe.

**Teste agora**: mande um e-mail de fora pro `contato@` e veja chegar no Gmail. Só siga depois
que isso funcionar.

### 4. Criar a conta no Resend e verificar o domínio

⚠️ **Tem que ser uma conta NOVA** (o `padelizou@gmail.com` é o endereço natural): o plano
grátis aceita **1 domínio por conta**, e a conta que você já usa está ocupada pelo
`atomatiza.com.br`.

Adicionar `padelizou.com.br`, escolhendo a região **São Paulo (sa-east-1)**, igual ao Atomatiza.
O painel gera três registros — **copie os valores de lá**, são gerados por conta:

| Nome | Tipo | O que é |
|---|---|---|
| `resend._domainkey` | TXT | DKIM — a assinatura que prova que o e-mail é seu |
| `send` | MX | retorno de bounces (`feedback-smtp.sa-east-1.amazonses.com`) |
| `send` | TXT | SPF do subdomínio de envio (`v=spf1 include:amazonses.com ~all`) |

O MX do Resend fica em `send.`, e o do Email Routing na raiz — **não brigam**.

⚠️ Deixe todos com a nuvem **cinza**. MX e TXT o Cloudflare nunca proxia, mas se algum vier como
CNAME ele liga o proxy por padrão e a verificação falha sem dizer por quê.

### 5. Publicar o DMARC

Um TXT a mais, na mão:

```
_dmarc    TXT    "v=DMARC1; p=none; rua=mailto:contato@padelizou.com.br"
```

⚠️ **`p=none` de propósito**: ele só pede relatório, não manda ninguém rejeitar nada. Depois de
umas semanas vendo que tudo que sai está assinado certo, aí sobe pra `p=quarantine`. Subir
direto pra `p=reject` com configuração recém-nascida é cortar o e-mail de inscrição de quem já
pagou.

### 6. Gerar a chave e apontar o app

No Resend, gerar uma **API key** — ela é a senha do SMTP e **só aparece uma vez**.

O código já aceita ([`EmailSettings.cs`](Padelizou/Services/EmailSettings.cs)): `SmtpUsuario`
existe porque o Gmail autentica com o próprio endereço do remetente e o Resend não — lá o
usuário é a palavra fixa `resend`.

No `padelizou.service`, **primeiro no dev**:

```
EmailSettings__SmtpHost          = smtp.resend.com
EmailSettings__SmtpPort          = 587
EmailSettings__SmtpUsuario       = resend
EmailSettings__RemetenteSenhaApp = re_...            (a API key)
EmailSettings__RemetenteEmail    = nao-responda@padelizou.com.br
EmailSettings__RemetenteNome     = Padelizou
EmailSettings__ResponderPara     = contato@padelizou.com.br
Suporte__Email                   = contato@padelizou.com.br
```

`ResponderPara` não é enfeite: com remetente `nao-responda@`, quem responder o e-mail de
inscrição fala com o vazio. O cabeçalho joga a resposta na caixa que você lê.

Depois: `systemctl daemon-reload && systemctl restart padelizou`. Testar no dev pedindo uma
recuperação de senha e vendo o e-mail chegar. **Só então** repetir em produção.

### 7. Passar a responder como `contato@`

No Gmail: Configurações → Contas → *Enviar e-mail como* → adicionar `contato@padelizou.com.br`
com o SMTP do Resend (`smtp.resend.com`, porta 587, usuário `resend`, senha = a mesma API key).
O código de confirmação chega na própria caixa, via Email Routing.

A partir daí você lê e responde tudo de dentro do Gmail de sempre, e quem está do outro lado só
vê `contato@padelizou.com.br`.

### 8. Revogar a senha de app do Gmail

Com o envio pelo Resend de pé, revogar em myaccount.google.com → Segurança → Senhas de app.

Ela está em claro no `appsettings.json` local — que é **ignorado pelo git e nunca esteve em
commit nenhum** (conferido com `git log -S` do valor, apesar de o repositório ser público). Não
houve vazamento; revogar aqui é higiene de credencial aposentada, não incêndio.

---

## Conferir depois

```bash
nslookup -type=MX padelizou.com.br 8.8.8.8
```

```bash
nslookup -type=TXT _dmarc.padelizou.com.br 8.8.8.8
```

O teste que vale mesmo: mandar um e-mail de `contato@padelizou.com.br` pra uma conta de fora e
abrir *Mostrar original*. Tem que aparecer `SPF: PASS`, `DKIM: PASS` e `DMARC: PASS`. DKIM
falhando com SPF passando é o caso mais comum — quase sempre é o TXT do DKIM colado com quebra
de linha no meio.

## O que vigiar

⚠️ **O teto de 100/dia é rígido e falha CALADO.** O `EmailService` engole a exceção de propósito,
pra não quebrar a inscrição de quem clicou — foi exatamente assim que os 130 e-mails de hoje
morreram sem ninguém perceber. O sinal no log é `Falha ao enviar e-mail para`.

Ainda não existe um vigia pra isso. O molde pronto é o `VigiaDoWhatsAppBackgroundService`, que
avisa quando o WhatsApp chega perto do teto e quando para de enviar.

**Detalhe que não incomoda, mas é bom saber**: o Resend limita a 10 requisições por segundo. O
`EntregadorDeAvisosBackgroundService` entrega um aviso por vez, abrindo conexão SMTP nova a cada
um, então ele já se espaça sozinho bem abaixo disso.

## Quando este arranjo deixa de servir

Dois gatilhos, e só eles:

- **Religar o e-mail do "Novo torneio aberto"** (`AdminController.Organizadores`). Volta a ser
  87 numa tacada, e o teto diário aperta de novo. A alternativa já estudada é mirar por estado
  (`Jogador.Estado`), em vez de reabrir pra base inteira — mas o campo é anulável, e quem nunca
  preencheu precisa continuar recebendo.
- **Um torneio com mais de ~100 inscritos**, em que só o "chaves saíram" já encosta no teto.

Nos dois casos a saída é o Amazon SES (sem teto mensal, ~US$ 0,16/1.000) ou o Resend Pro
(US$ 20/mês, que cobriria o Atomatiza junto). Trocar de provedor é mudar quatro variáveis no
systemd — não se refaz nada.

## Ordem importa

Publicar o `Suporte__Email` novo **antes** de o encaminhamento funcionar põe no site um endereço
que não recebe nada. A ordem segura é a deste documento: DNSSEC, nameserver, receber, conferir
que chega, e só então trocar o que o site mostra.
