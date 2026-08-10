# E-mail próprio: `@padelizou.com.br`

✅ **NO AR EM PRODUÇÃO DESDE 10/08/2026.** Recebe em `contato@`, `suporte@` e `financeiro@`
(ImprovMX → Gmail), e envia como `nao-responda@padelizou.com.br` pelo Resend. Provado nos dois
sentidos: e-mail de fora chegou no Gmail, e a **recuperação de senha de produção chegou na
CAIXA DE ENTRADA** — não no spam — num domínio que tinha nascido no mesmo dia.

Como sair do `padelizou@gmail.com` e passar a receber **e** enviar pelo domínio, de graça.

Escrito em 09/08/2026, no dia em que a cota do Gmail estourou: o disparo de "Novo torneio
aberto" (87 e-mails) queimou o limite e **130 e-mails morreram calados**, duas recuperações de
senha entre eles — um jogador ficou sem conseguir entrar e ninguém soube até horas depois.

⚠️ **A configuração de produção mora num DROP-IN**, não na unidade principal:
`/etc/systemd/system/padelizou.service.d/email.conf`. As linhas antigas do Gmail continuam no
`padelizou.service` e são vencidas pelo drop-in (ele é lido depois). Quem editar só o arquivo
principal vai mexer em linhas mortas e não vai entender por que nada muda. Reverter tudo é
apagar esse arquivo e reiniciar. ⏳ **O dev continua no Gmail** — só produção foi migrada.

---

## São dois serviços, porque são dois problemas

Encaminhador não envia; serviço de envio não guarda caixa. Ninguém vende as duas metades junto
de graça, então:

| | Serviço | Custo | Teto |
|---|---|---|---|
| **Receber** (`contato@`) | ImprovMX | R$ 0 | 25 apelidos, 500 encaminhamentos/dia |
| **Enviar** (app e você) | Resend, plano grátis | R$ 0 | **100/dia**, 3.000/mês, 1 domínio |

**O DNS FICA NO REGISTRO.BR.** Nenhum dos dois exige mexer em nameserver: são 2 registros MX pro
ImprovMX e 4 pro Resend, todos no editor de zona que já existe.

### ⚠️ Por que o Cloudflare foi descartado NA HORA H

O plano original era Cloudflare Email Routing (grátis, sem limite de volume), copiando o
`atomatiza.com.br`, que roda exatamente isso. Ele exige o DNS na Cloudflare, e o
`padelizou.com.br` tem **DNSSEC ligado** — então o roteiro era: desligar o DNSSEC, esperar o DS
sair do cache, e só então trocar o nameserver.

**No painel do Registro.br esse primeiro passo não existe.** Não há chave de desligar DNSSEC:
o que há é um botão `+ DNSSEC` **dentro do formulário de trocar servidores DNS**. Ou seja, a
remoção do DS e a troca de delegação acontecem **no mesmo salvamento**, e não dá pra separar.

O estrago disso, medido: o **TTL do DS é 3600 segundos**. Durante até uma hora, todo resolver
que tivesse o DS antigo em cache tentaria validar uma zona que a Cloudflare serve sem
assinatura — e o resultado, pra essas pessoas, é **SERVFAIL: o domínio não existe**. Não é o
site fora do ar, é o domínio sumindo, com inscrição paga aberta.

Como o Cloudflare só resolvia a metade de RECEBER — e os registros do Resend cabem no
Registro.br do mesmo jeito —, era arriscar o domínio inteiro por uma metade. Trocado pelo
ImprovMX, que faz o mesmo com 2 registros MX e zero downtime.

Se um dia o DNS for pra Cloudflare por outro motivo, o roteiro é: fazer **de madrugada**,
preencher os dois nameservers e **não tocar no `+ DNSSEC`**.

### Por que não os outros

**Amazon SES**: é o que roda por baixo do próprio Resend (o MX de envio do Atomatiza aponta pra
`feedback-smtp.sa-east-1.amazonses.com`). Sairia por ~US$ 0,16/1.000 — centavos no nosso
volume —, mas **exige cartão**, começa num sandbox de 200/dia só pra endereços verificados, e a
liberação leva de 1 a 3 dias úteis. Só passa a valer a pena acima de 3.000/mês.

**Brevo**: 300/dia, melhor teto, mas carimba o logo deles em todo e-mail que sai. Inaceitável
pra um produto que cobra do cliente.

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

### Como funciona o editor de zona do Registro.br

Vale ler antes, porque três detalhes dele confundem:

- O campo **Nome** recebe só o PREFIXO — o `.padelizou.com.br` é colado automaticamente do lado.
  Pra um registro na raiz do domínio, o Nome vai **vazio**. Ele não aceita `@` nem `*`.
- **Prioridade de MX é campo separado** do nome do servidor.
- Entrada nova aparece com **bolinha VERDE** e ainda não existe: só passa a valer depois do
  **SALVAR ALTERAÇÕES** no fim da lista. Fechar a tela antes disso descarta tudo.

⚠️ **Nunca apague nem edite os 4 registros A** (`padelizou.com.br`, `www`, `admin`, `dev` →
179.197.233.184). São o site, e nada neste documento encosta neles.

### 1. Receber: ImprovMX (feito em 10/08/2026)

Em improvmx.com, conta grátis, adicionar `padelizou.com.br` e criar os apelidos, todos
encaminhando pra `padelizou@gmail.com`:

- `contato@` — o endereço público, o que vai no site
- `suporte@`
- `financeiro@` — pro meio de pagamento, nota fiscal, MEI

⚠️ **APAGUE o apelido `*` (catch-all), que o ImprovMX cria sozinho.** Ele encaminha qualquer
endereço inventado do domínio, e o plano grátis **pausa o encaminhamento do domínio inteiro** ao
estourar 500/dia — um spammer descobrindo o catch-all derrubaria o `contato@` junto. Com
apelidos nomeados, e-mail pra endereço inexistente é recusado, que é o certo.

O banner de Upgrade ("Send Emails via SMTP, $9/m") **não serve pra nós**: quem envia é o Resend.

Depois, no Registro.br → Configurar zona DNS → NOVA ENTRADA, duas vezes:

| Tipo | Nome | Prioridade | Servidor |
|---|---|---|---|
| MX | (vazio) | 10 | `mx1.improvmx.com.` |
| MX | (vazio) | 20 | `mx2.improvmx.com.` |

**Salvar** e conferir. O cache negativo desta zona é de 15 minutos, então o Google DNS demora um
pouco a mostrar — o autoritativo responde na hora e é ele que diz a verdade:

```bash
nslookup -type=MX padelizou.com.br d.sec.dns.br
```

O selo vermelho *Setup* do ImprovMX vira verde sozinho quando ele enxergar os dois.

**Teste antes de seguir**: mande um e-mail de fora pro `contato@` e veja chegar no Gmail.

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

Todos vão no mesmo editor de zona do Registro.br, no campo **Nome** só com o prefixo (`send`,
`resend._domainkey`) — o domínio é colado sozinho.

⚠️ **O MX do Resend fica em `send.` e o do ImprovMX na RAIZ — eles não brigam**, e é justamente
por isso que dá pra receber por um e enviar pelo outro sem conflito. Não mexa nos MX da raiz ao
adicionar os do `send`.

### 5. Publicar o DMARC

Um TXT a mais, na mão, com o Nome `_dmarc`:

```
v=DMARC1; p=none; rua=mailto:contato@padelizou.com.br
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
O código de confirmação chega na própria caixa, encaminhado pelo ImprovMX.

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
