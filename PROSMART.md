# Prosmart — placar da quadra enchendo o placar do torneio

> **Status: conversa aberta, nada implementado.** Este documento é o que a gente já sabe, o que
> falta perguntar e como a integração entra no Padelizou quando o formato do retorno deles
> estiver na mão. Nenhuma linha de código foi escrita ainda de propósito — desenhar o mapeamento
> antes de ver o JSON deles é inventar um contrato que vai mudar.

## O que é

A **Prosmart** (contato: Gustavo Kuskoski) vende **placar eletrônico** instalado em quadras de
clubes. Eles oferecem um **link GET que devolve a pontuação de TODAS as quadras de um clube**.

O que isso muda no dia de torneio: hoje o placar do Padelizou é digitado na **Mesa de Controle**
(`Controllers/TorneiosController.Placar.cs`) por um ajudante do organizador, jogo a jogo, game a
game, a noite inteira — com 5 quadras rodando é a tarefa mais repetitiva do torneio. Quem já
marca o game de verdade é o **jogador, no placar da parede**, porque o ponto acabou de acontecer
ali. Se aquele número chega até nós sozinho, a Mesa deixa de ser digitação e vira só conferência
e finalização — e o **placar ao vivo** que os seguidores recebem
(`Services/AvisoDePlacarAoVivo`) passa a acompanhar o jogo em tempo real sem depender de alguém
lembrar de atualizar.

## As três perguntas do Gustavo (19/08/2026)

### 1. "Consegue me passar o IP do servidor que vai acessar?"

**`179.197.233.184`** — um IPv4 só, e é o mesmo pra tudo: produção
(`padelizou.com.br`) e o ambiente de teste (`dev.padelizou.com.br`) rodam **no mesmo VPS**, em
portas diferentes atrás do Caddy (ver `Padelizou/Caddyfile` e `infra/vps/README.md`). Não existe
proxy de saída nem função serverless no caminho: quem fizer o GET é o processo do site, direto da
máquina.

⚠️ **Confirmar o IP de SAÍDA antes de mandar.** O endereço acima é o de entrada (o que a gente
usa pra `ssh root@...`). Em VPS eles quase sempre são o mesmo, mas quem conferiu isso foi um
`ssh`, não uma chamada de saída. Antes de mandar pro Gustavo:

```bash
ssh root@179.197.233.184 'curl -s https://ifconfig.me; echo'
```

⚠️ **Pedir que a liberação não seja SÓ por IP.** IP de VPS muda quando se troca de plano, de
região ou de provedor — e no dia em que mudar, a integração cai calada no meio de um torneio.
O certo é IP **mais** uma chave no cabeçalho (é como a gente faz com o Ranking RS nas duas mãos —
ver `Services/RankingRsSettings`). Se eles só souberem trabalhar com IP, tudo bem — mas fica
anotado aqui que trocar de VPS passa a exigir avisar a Prosmart **antes**.

### 2. "Você atende algum clube que já tem o nosso sistema?"

**Sim: o ER Padel**, que fechou com o Padelizou e usa Prosmart. É o clube do teste — dá pra casar
o placar da parede com um torneio real nosso, no mesmo lugar, sem simular nada.

### 3. "Passo um link e veja o que sua equipe consegue coletar"

O que a gente precisa que venha (e o que faz falta em cada caso) está na seção seguinte. Vale
pedir o link e **uma amostra do retorno com jogo rolando** — placar parado em 0x0 esconde
exatamente as perguntas que importam.

## O que precisamos saber do retorno deles

Perguntas em ordem de importância. As três primeiras decidem se a integração é possível; as
outras decidem o quanto ela é boa.

1. **Como cada quadra vem identificada** — id numérico? nome digitado no painel do clube? Esse
   identificador é **estável** (sobrevive a renomear a quadra, a reiniciar o placar, a trocar o
   aparelho)? É com ele que a gente amarra "quadra 3 da Prosmart" a "Quadra 3 do torneio".
2. **Como o placar vem** — games e sets separados? histórico set a set (6x4, 3x6, 7x5) ou só o
   set corrente? ponto dentro do game (15/30/40/AD)? Hoje o Padelizou guarda **games e sets do
   jogo** (`Models/Partida`: `GamesDupla1/2`, `SetsDupla1/2`) — é o mínimo necessário. Ponto e
   saque a gente ainda não guarda, mas é o tipo de coisa que deixa o "placar ao vivo" muito
   melhor, então vale saber se existe.
3. **Como saber que ali começou um JOGO NOVO** — o placar zera sozinho entre uma partida e outra?
   Vem algum `iniciado_em` / `id da partida` / contador que muda? ⚠️ **Esta é a pergunta que mais
   pode estragar o resultado**: sem ela, um placar que ficou 6x3 da partida anterior entra no
   jogo seguinte como se fosse dele, e o organizador vai atrás de um erro que não é dele.
4. **De que lado é "time 1"** — o placar sabe qual lado da quadra é qual? A gente precisa amarrar
   time 1/time 2 deles à Dupla 1/Dupla 2 nossa, e errar isso inverte o resultado do jogo (ver
   "Riscos", abaixo).
5. **Frequência e limite** — de quantos em quantos segundos podemos chamar? O retorno tem carimbo
   de hora (e em qual fuso)? Existe rate limit? A gente **não** vai ficar batendo o dia inteiro:
   a ideia é chamar só enquanto há jogo ao vivo em torneio naquele clube.
6. **Autenticação** — só IP, ou dá pra ter uma chave? Ambiente de teste separado?
7. **Mão contrária (fase 2)** — dá pra a gente **mandar** coisa pro placar? Nome das duplas,
   categoria, tempo de jogo. Um placar eletrônico escrevendo "Felipe/Gabriel × João/Pedro —
   Semifinal 3ª Masculina" é o que faz o torneio parecer profissional, e a informação já é nossa.

## Como isso entra no Padelizou

Desenho pretendido, seguindo o que o sistema já faz — nada aqui é novidade de arquitetura:

**Quem chama.** Um `PlacarProsmartBackgroundService`, no molde dos vigias que já existem (ver
`Services/QuadraAtrasadaBackgroundService`): tick curto (5–10s), acordando **só** quando existe
partida `AoVivo` num torneio cujo clube tem Prosmart ligado. Sem jogo ao vivo, zero chamada — o
servidor deles não tem que pagar pelo nosso relógio.

**Onde mora a configuração.** `ProsmartSettings` no padrão do
`Services/RankingRsSettings`: `BaseUrl`, chave, timeout; chave só via `Environment=` no systemd,
nunca em `appsettings.Development.json` (que é versionado). **Chave vazia = integração
desligada**, que é o padrão certo pra localhost e pros testes.

**O mapeamento de quadra.** Uma coluna/tabela ligando o identificador da Prosmart ao nome da
quadra do clube (`Models/QuadraClube`) — e a partida é achada por `NomeQuadra` + `Status ==
"AoVivo"` + torneio daquele clube. ⚠️ **Não dá pra casar por nome cru**: o cadastro do clube diz
"Quadra 1" e o torneio pode estar rodando com "Quadra A" (é justamente o caso que
`Services/NomesDeQuadra` documenta). Quadra sem par no mapa fica de fora, calada — gente jogando
avulso na quadra 4 não é jogo de torneio.

**Por onde escreve.** `Services/PlacarDaMesa.Aplicar`, o mesmo lugar por onde a Mesa escreve, e
por dois motivos: ele já recusa placar em partida **finalizada** e já resolve "quem chegou
depois" pelo carimbo de hora (o `marcadoEm`), que é exatamente o conflito que passa a existir
entre o placar da parede e o celular do organizador. Rota nova escrevendo direto na `Partida`
seria uma segunda regra pra mesma coisa — e a que ficasse de fora é que ia machucar.

⚠️ **O teto da fase continua mandando** (`Services/FormatoDaPartida.PlacarValido`): torneio
marcado pra jogo até 4 não pode receber um 6x3. Só que aqui **cortar calado é errado** — um 6x3
chegando num torneio até 4 não é digitação torta, é o **placar da parede configurado diferente do
torneio**, e o organizador precisa saber disso agora, não no fim do jogo. Nesse caso: não aplica
e avisa.

**Finalizar continua sendo humano.** Encerrar partida dispara mata-mata, carimba fase, avisa
gente (`Services/EncerramentoDaPartida`, `AvancoDaChave`). Placar automático **preenche**; quem
diz "acabou" é o organizador. Automatizar isso é conversa pra depois de a primeira parte estar
rodando sem susto.

**Quando o organizador corrige na mão.** Decisão do Felipe, e ela precisa ser tomada antes do
teste: a recomendação daqui é que a **correção manual desliga o automático daquela partida**
(com o organizador podendo religar). O "último carimbo vence" sozinho não resolve — o placar da
parede continua ali, e no próximo tick ele desfaz a correção que o organizador acabou de fazer,
que é a maneira mais rápida de o organizador desistir da integração inteira.

**Quando eles caem.** A integração nunca pode travar a Mesa: timeout curto, nunca lança, erro vai
pro `Services/RegistroDeErros`, e o organizador segue digitando como sempre. O caminho manual
continua existindo inteiro — placar automático é atalho, não dependência.

## Riscos conhecidos (nossos, não deles)

- **Lado invertido.** Se "time 1" da Prosmart não for a Dupla 1 da partida, o jogo entra ao
  contrário — e um 6x2 vira derrota de quem ganhou. Não dá pra deduzir do número; precisa de
  convenção clara (lado da quadra) ou de um botão "inverter" na Mesa.
- **Placar que não zera.** Ver pergunta 3 acima.
- **Dois torneios no mesmo clube ao mesmo tempo.** Raro, mas o mapa é por clube: a partida
  precisa ser procurada dentro do torneio certo.
- **Relógio.** O `marcadoEm` da Mesa vem do **celular de quem marca**, não do servidor. Se o
  carimbo da Prosmart vier do relógio deles e um dos dois estiver adiantado, o mais "novo" pode
  ser o mais velho. Se eles mandarem carimbo, usar o deles; se não, usar o nosso — e ter isso
  escrito num lugar só.
- **Jogo que não é torneio.** Quadra com gente jogando avulso devolve placar do mesmo jeito. Sem
  partida `AoVivo` casada, ignora.

## Teste no ER Padel — o roteiro

1. Gustavo libera o IP e manda o link do ER Padel.
2. A gente chama o GET **com jogo rolando** e guarda o retorno cru (é o que responde metade das
   perguntas acima).
3. Amarra as quadras do ER Padel no mapa e roda **em modo leitura**, sem escrever placar: um log
   comparando "o que a parede diz" × "o que a Mesa digitou", num torneio real. É aqui que dá pra
   ver se o número bate, quanto atrasa e o que acontece entre um jogo e outro.
4. Só depois de o log bater, liga a escrita — primeiro num torneio, com o organizador avisado.

## Rascunho da resposta ao Gustavo

> Bom dia, Gustavo! Tudo ótimo.
>
> O IP do nosso servidor é **179.197.233.184** — é uma VPS só, e é dela que sai a chamada
> (produção e ambiente de teste rodam na mesma máquina). Se der, além do IP a gente prefere
> autenticar com uma chave no cabeçalho: IP de VPS pode mudar, e aí a integração cairia calada.
>
> Clube com o sistema de vocês pra testar: o **ER Padel**, que fechou com a gente agora — perfeito
> pro teste, é torneio nosso na quadra de vocês.
>
> Pode mandar o link. O ideal é a gente ver um retorno **com jogo rolando**. E já aproveito as
> perguntas que a equipe vai fazer de qualquer jeito:
>
> 1. Como cada quadra vem identificada, e esse identificador é estável?
> 2. O placar vem com games e sets separados? Vem set a set e o ponto do game (15/30/40)?
> 3. Como a gente sabe que começou uma partida NOVA na quadra (o placar zera? vem algum
>    id/horário de início)?
> 4. De quantos em quantos segundos podemos consultar, e o retorno tem carimbo de hora?
> 5. Existe algum jeito de saber qual lado da quadra é o "time 1"?
> 6. E uma que interessa muito pro torneio: dá pra a gente MANDAR informação pro placar? Nome das
>    duplas e a fase ("Semifinal 3ª Masculina") aparecendo na parede deixa o torneio com outra
>    cara — a informação já é nossa, é só ter por onde entregar.
