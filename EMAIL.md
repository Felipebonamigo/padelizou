# E-mail próprio: `@padelizou.com.br`

Como sair de `padelizou@gmail.com` e passar a receber **e** enviar pelo domínio, de graça.

Escrito em 09/08/2026, quando o domínio ainda não tinha **nenhum** registro MX — ou seja, o
`padelizou.com.br` simplesmente não recebia e-mail.

---

## As decisões, e por quê

**O domínio é o `.com.br`.** O `padelizou.com` não está registrado (consulta de DNS volta
"domínio inexistente"). Não vale a pena registrar agora só pelo e-mail.

**O DNS continua no Registro.br.** O domínio está com **DNSSEC ligado** (tem registro DS
publicado). Trocar os nameservers pra Cloudflare sem desligar o DNSSEC antes derruba o domínio
inteiro — site, `admin.` e `dev.` juntos, e o erro é de validação, o que é chato de diagnosticar.
O ganho do Cloudflare (encaminhamento sem limite de volume) não paga esse risco.

**Receber e enviar são serviços diferentes.** Encaminhador não envia; serviço de envio não
guarda caixa. Precisa dos dois.

| | Serviço | Custo | Teto |
|---|---|---|---|
| Receber (`contato@`) | ImprovMX | R$ 0 | 1 domínio, 25 apelidos, 500 e-mails/dia |
| Enviar (app e você) | Resend | R$ 0 | 3.000/mês, **100/dia**, 1 domínio, sem marca d'água |

Descartados: **Brevo** (300/dia, mas carimba o logo deles em todo e-mail — ruim pra quem cobra
pelo produto) e **Zoho grátis** (caixa de verdade, mas sem SMTP externo, então o app não podia
usar).

⚠️ **O teto que vai te alcançar é o 100/dia do Resend.** A base é ~72 pessoas: um aviso pra
todo mundo cabe hoje e para de caber quando passar de 100. Quando isso chegar, é US$ 20/mês
(Resend Pro). Não é limite mensal, é diário — estourou, o envio falha calado (o
`EmailService` engole a exceção de propósito, pra não quebrar a inscrição de quem clicou).

---

## Passo a passo

Cada conta é criada por você — não dá pra terceirizar cadastro nem senha.

### 1. Receber: ImprovMX

1. Em improvmx.com, criar conta grátis e adicionar o domínio `padelizou.com.br`.
2. Criar os apelidos, todos apontando pra `padelizou@gmail.com`:
   - `contato@padelizou.com.br` — o endereço público, o que vai no site
   - `suporte@padelizou.com.br`
   - `financeiro@padelizou.com.br` — pro Asaas, nota fiscal, MEI
3. O painel mostra **2 registros MX**. Anota, entram no passo 3.

Não crie apelido `nao-responda@` — esse só envia, nunca recebe.

### 2. Enviar: Resend

1. Em resend.com, criar conta grátis e adicionar o domínio `padelizou.com.br`.
2. O painel gera os registros de autenticação (um `TXT` de DKIM em
   `resend._domainkey`, mais `MX` e `TXT` de SPF num subdomínio tipo `send.`). **Copia os
   valores de lá** — são gerados por conta, não dá pra chutar aqui.
3. Depois que o DNS propagar, clicar em verificar até ficar verde.
4. Gerar uma **API key** (é a senha do SMTP). Guarda: ela só aparece uma vez.

O MX do Resend fica no subdomínio `send.`, e o do ImprovMX na raiz — **não brigam**.

### 3. Registro.br: publicar os registros

Painel do Registro.br → o domínio → editar zona DNS.

```
; --- receber (ImprovMX) ---
@                    MX    10  mx1.improvmx.com.
@                    MX    20  mx2.improvmx.com.

; --- enviar (Resend): valores EXATOS saem do painel do Resend ---
resend._domainkey    TXT   "p=MIGfMA0GCS..."      ; DKIM
send                 MX    10  feedback-smtp...   ; conforme o painel
send                 TXT   "v=spf1 include:amazonses.com ~all"

; --- SPF da raiz: só quem pode assinar em nome do domínio ---
@                    TXT   "v=spf1 include:_spf.resend.com ~all"

; --- DMARC: começa observando, sem rejeitar nada ---
_dmarc               TXT   "v=DMARC1; p=none; rua=mailto:contato@padelizou.com.br"
```

Sobre o **DMARC em `p=none`**: é de propósito. Ele só pede relatório, não manda ninguém
rejeitar. Depois de umas semanas vendo que tudo que sai está assinado certo, aí sim sobe pra
`p=quarantine`. Subir direto pra `p=reject` com a configuração nova é como cortar o e-mail de
inscrição de quem já pagou.

⚠️ **Não mexa nos registros A** (`@`, `www`, `admin`, `dev` → 179.197.233.184). São o site.

### 4. Apontar o app pro Resend

O código já aceita ([`EmailSettings.cs`](Padelizou/Services/EmailSettings.cs)): `SmtpUsuario`
existe porque o Gmail autentica com o próprio endereço do remetente e o Resend não — lá o
usuário é a palavra fixa `resend` e a senha é a chave de API.

No `padelizou.service` (prod **e** dev), trocar:

```
EmailSettings__SmtpHost         = smtp.resend.com
EmailSettings__SmtpPort         = 587
EmailSettings__SmtpUsuario      = resend
EmailSettings__RemetenteSenhaApp = re_...            (a API key do Resend)
EmailSettings__RemetenteEmail   = nao-responda@padelizou.com.br
EmailSettings__RemetenteNome    = Padelizou
EmailSettings__ResponderPara    = contato@padelizou.com.br
Suporte__Email                  = contato@padelizou.com.br
```

`ResponderPara` não é enfeite: com remetente `nao-responda@`, quem responder o e-mail de
inscrição fala com o vazio. O cabeçalho joga a resposta na caixa que você lê.

Depois: `systemctl daemon-reload && systemctl restart padelizou`. **Testa no dev primeiro** —
lá dá pra errar.

### 5. Passar a responder como `contato@` (opcional, mas é o ponto todo)

No Gmail: Configurações → Contas → *Enviar e-mail como* → adicionar
`contato@padelizou.com.br`, usando o SMTP do Resend (`smtp.resend.com`, 587, usuário `resend`,
senha = a mesma API key). O Gmail manda um código de confirmação, que chega na própria caixa
via ImprovMX.

A partir daí você lê e responde tudo de dentro do Gmail de sempre, e a pessoa do outro lado só
vê `contato@padelizou.com.br`.

### 6. Aposentar a senha de app do Gmail

Assim que o envio pelo Resend estiver de pé, **revoga a senha de app** em
myaccount.google.com → Segurança → Senhas de app. Ela está em claro no `appsettings.json`
local (que é ignorado pelo git e nunca foi commitado — não há histórico pra limpar), mas
credencial que não é mais usada não deve continuar válida.

---

## Conferir depois

```bash
nslookup -type=MX padelizou.com.br 8.8.8.8
```

```bash
nslookup -type=TXT _dmarc.padelizou.com.br 8.8.8.8
```

O teste que vale mesmo: mandar um e-mail de `contato@padelizou.com.br` pra uma conta de fora
(um Gmail qualquer) e abrir *Mostrar original*. Tem que aparecer `SPF: PASS`, `DKIM: PASS` e
`DMARC: PASS`. DKIM falhando com SPF passando é o caso mais comum — quase sempre é o TXT do
DKIM colado com quebra de linha no meio.

## Ordem importa

Publicar o `Suporte__Email` novo **antes** do MX existir põe no site um endereço que não
recebe nada. A ordem segura é: MX primeiro (passos 1 e 3), confirmar que chega e-mail, e só
então trocar o que o site mostra.
