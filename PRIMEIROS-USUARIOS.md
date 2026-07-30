# Primeiros usuários — o que mandar, e o que esperar

> Escrito em 30/07/2026 para a liberação do **primeiro organizador de torneio** e do
> **primeiro professor**. O sistema está em beta fechado: o portão continua ligado, e quem
> entra precisa da senha compartilhada.

## Como funciona a entrada (importante entender antes de mandar)

São **duas portas em sequência**, e elas são coisas diferentes:

1. **O portão do beta** (`padelizou` + a senha compartilhada). É uma senha só, igual pra todo
   mundo, e serve só pra manter o site fora do ar público. Passa uma vez e o navegador lembra
   por 90 dias.
2. **A conta da pessoa** (login e senha que *ela* escolhe, criados por ela no Cadastro). É essa
   que diz quem ela é — o torneio, as aulas e o dinheiro ficam pendurados nela.

✅ Verificado em 30/07: quem passa pelo portão entra **deslogado** e cria a própria conta. O
antigo "modo demonstração" (que fazia todo visitante entrar como o Felipe) está **desligado**
em produção — sem isso, o organizador entraria na sua conta de administrador.

**As credenciais do portão** (definidas em 30/07): usuário **`Corneteiros`**, senha **`corneta`**.
Maiúscula/minúscula no usuário tanto faz, e espaço colado ao copiar não atrapalha.

## Mensagem pronta pro organizador

> Fala! O Padelizou tá no ar pra você já ir montando teu torneio de verdade.
>
> 1. Entra em **padelizou.com.br**
> 2. Vai pedir uma senha de acesso (é do beta, pra todo mundo): usuário `Corneteiros`, senha `corneta`
> 3. Clica em **Cadastre-se** e cria a **tua** conta (login e senha que você escolher)
> 4. Depois é só ir em **Torneios → Criar torneio**
>
> Se marcar **Torneio Restrito**, você escolhe a chave que os jogadores vão digitar pra se
> inscrever (a nossa vai ser **virgili10**) — quem não tiver a chave não entra.
>
> Qualquer coisa estranha, me chama que eu resolvo. Tá em beta, então tua opinião vale ouro.

## Mensagem pronta pro professor

> Fala! O Padelizou tá no ar pra você começar a receber aluno.
>
> 1. Entra em **padelizou.com.br**
> 2. Vai pedir uma senha de acesso (é do beta, pra todo mundo): usuário `Corneteiros`, senha `corneta`
> 3. Clica em **Cadastre-se**, cria a **tua** conta e marca a opção **"sou professor"**
> 4. O sistema vai te pedir, nessa ordem: **cidade → local → horário**. Enquanto faltar um
>    desses, nenhum aluno consegue te achar — por isso ele insiste.
>
> Você tem **15 dias de teste** com as condições de assinante. Depois escolhe: mensalidade de
> R$ 49,90 + 3% por aula no Pix (6% no cartão), ou sem mensalidade pagando 10% por aula.

## O que eles VÃO encontrar (pra você não ser pego de surpresa)

- **O site está vazio.** A produção foi zerada em 28/07: 0 torneios, 1 jogador (você). Eles vão
  ver telas vazias com o Pnatinha até criarem as próprias coisas. É esperado.
- **O professor é obrigado a cadastrar cidade, local e horário** antes de o painel abrir. Não é
  travamento: é a escada que impede professor invisível.
- **O primeiro torneio vai ser "por fora"** (decisão de 30/07): o organizador cobra a inscrição
  direto dos jogadores, e os 5% do Padelizou ficam pra acertar no fim. Assim ele não precisa
  abrir conta em meio de pagamento nenhum pra começar hoje.
  **Como isso termina:** quando ele encerrar as inscrições e for sortear as chaves, aparece a
  tela da taxa com a conta feita (pessoas × preço × 5%). Aí **você entra como admin nessa mesma
  tela e registra a negociação**, com uma observação do que foi combinado — as chaves liberam
  na hora. É o caminho de verdade sendo exercitado, sem dinheiro trocando de mão nesse primeiro.
- Se um dia ele quiser **receber pelo site**, aí sim precisa da conta dele no meio de pagamento
  (tela Pagamentos → Configurar pede o identificador de recebimento).
- **Muitas tentativas seguidas** no portão ou no login mostram um aviso pedindo 5 minutos. É a
  proteção contra robô, ligada em 30/07 — cada ação tem sua própria contagem, então errar a
  senha do portão não atrapalha o cadastro.

## Se algo travar

- **"Usuário ou senha incorretos" no portão** — a senha do portão não é a da conta dele.
- **"Já tem alguém cadastrado com esse e-mail/login"** — o formulário volta preenchido, é só
  trocar o campo apontado.
- **Esqueceu a senha da conta** — tem "Esqueci minha senha" na tela de entrar; chega por e-mail.
- **Erro de verdade (tela de Ops)** — o link "Sugestão, bug ou crítica" na faixa de beta manda
  direto pro seu WhatsApp com a mensagem já começada.

## ✅ O que já foi ensaiado (30/07, no ambiente publicado)

A corrente inteira do torneio foi percorrida com uma conta criada na hora, no dev — que roda o
mesmo build da produção:

| Passo | Resultado |
|---|---|
| Criar conta pelo portão | entra **deslogado**, cria a própria conta, já sai logado |
| Criar torneio restrito com chave `virgili10` | chave gravada e mostrada na tela do torneio |
| Inscrição com a chave **errada** | recusada |
| Inscrição com `VIRGILI10` (maiúscula) | aceita — maiúscula/minúscula tanto faz |
| Encerrar inscrições | torneio vai pra "Chaves em Sorteio" |
| Tentar sortear sem acertar a taxa | **barrado**, inclusive por requisição feita à mão |
| Tela da taxa | 4 pessoas × R$ 50 × 5% = **R$ 10,00** |
| Admin registra a negociação | observação guardada, chaves liberadas |
| Sortear | jogos criados, **nenhum sem horário**, torneio em Fase de Grupos |

Nesse ensaio apareceu **um defeito, já corrigido**: dava pra criar torneio **sem categoria
nenhuma**, e aí ninguém conseguia se inscrever — sem aviso nenhum. Agora o sistema recusa e
explica.

⚠️ **Pra você entrar:** a sua conta de produção está com o campo *login* vazio — entre pelo
**e-mail**, não por um nome de usuário.

## Depois da primeira noite, vale conferir

- `/Admin/Metricas` — cadastros e inscrições que apareceram.
- `/Admin/Feedbacks` — o que eles escreveram (nada aparece no site até você publicar).
- O journal do servidor, se alguém relatar erro: `journalctl -u padelizou --since '1 hour ago'`.
