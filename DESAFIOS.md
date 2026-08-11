# DESAFIOS.md — a espec oficial dos Desafios

> Desenhada com o Felipe em 11/08/2026.
> Este arquivo é a fonte da verdade. Mudou a regra? Muda **AQUI** primeiro — e só depois no código.
>
> 🔒 **Nasce fechado.** Enquanto `Desafios__Habilitado` não for `true`, o módulo existe em
> produção e **só o admin do Padelizou enxerga** (mesmo mecanismo do Bar — ver
> `Services/DesafiosSettings`).

## A ideia numa frase

Uma **dupla** anuncia que quer jogar nesta semana, diz **onde** aceita e **contra que
categorias**; outra dupla clica em **Desafiar**; o jogo acontece; os dois lados confirmam o
placar; isso alimenta um **ranking próprio** de duplas e de jogadores.

## Por que existe

O Padelizou só sabia juntar gente por **evento**: torneio precisa de organizador, aula precisa
de professor, raquete livre precisa do clube abrir, grupo privado precisa você já ser da
panelinha. Não havia nada para quem **tem parceiro, quer jogar sábado e não sabe contra quem**
— que é o padel de 90% das semanas do ano.

Três efeitos pretendidos, nesta ordem de importância:

1. **Retenção entre torneios.** Hoje quem joga um torneio some por 30 dias.
2. **Quadra vazia.** Clube com `MarcacaoHorariosAtiva` ganha demanda de terça à noite — que é
   o que ele mais quer, e é a isca do plano do clube.
3. **Volume de dado medido.** Mais jogos por mês do que o calendário de torneios jamais dará.

## O que Desafio NÃO é (a fronteira com o que já existe)

Esta seção existe porque **regra duplicada é a origem dos bugs graves deste sistema**. Antes de
escrever qualquer coisa aqui, confira se não é uma das quatro abaixo:

| Já existe | O que é | Diferença |
|---|---|---|
| `AvisoJogo` ("Buscar jogo") | **Uma pessoa** avisa que vai jogar em tal clube e procura quem complete | Não tem dupla, não tem adversário definido, não tem placar nem ranking |
| `Parceiros` | Procura **parceiro** | Desafio pressupõe a dupla já formada |
| `RaqueteLivre` | Rodízio do **clube**, sem dupla fixa | Quem abre é o clube; não há confronto |
| `GrupoPrivado` / `JogoSemanal` | Panelinha fechada, ranking interno | Desafio é entre **desconhecidos**, e é essa a graça |

Desafio é a única coisa do sistema em que **duas duplas que não se conhecem marcam um jogo com
placar que vale ranking**.

---

## 1. O fluxo

```
[dupla A publica anúncio da semana]
        │  (o parceiro precisa confirmar por link — o anúncio fica em rascunho até lá)
        ▼
   MURAL  ── dupla B abre o cartão e escolhe categoria + clube + data/hora
        │       entre os que AQUELE anúncio aceita
        ▼
    Proposto ──48h sem resposta──▶ Expirado (some do mural; ninguém pontua)
        ├── Recusado
        └── Aceito ──▶ [qualquer um dos 4 lança o placar]
                          ├── a outra dupla CONFIRMA ─────▶ Confirmado  ✅ pontua
                          ├── 72h sem resposta ──────────▶ Confirmado  ✅ pontua
                          └── a outra dupla CONTESTA ────▶ Em disputa   ❌ ninguém pontua
```

**Não existe negociação dentro do sistema.** Contraproposta = recusar e mandar um desafio novo.
Um chat de negociação dobraria o tamanho da feature, e o WhatsApp já faz isso melhor do que
nós faríamos.

---

## 2. As regras duras

### 🔐 O anúncio precisa dos DOIS

Quem publica manda um link ao parceiro; o parceiro entra com a **própria conta** e confirma.
Enquanto não confirmar, o anúncio é **rascunho** e **não aparece no mural**.

É o mesmo mecanismo do `Services/ConviteDeParceiro` (token de 32 bytes base64url, comparado em
tempo fixo) e existe pela mesma razão que ele: sem isso eu anuncio o Lucas para jogar sábado às
8h e ele descobre por um push de desafio **já aceito**.

### 📅 "A semana" expira — e expirar não mata nada junto

O anúncio vale até **domingo 23:59** da semana escolhida. Renovar é um clique.

Duas travas, e nenhuma é detalhe:

- **`Expirado` é LIDO do relógio, nunca gravado.** É a lição literal de
  `Services/LeadsComerciais`: um job que vira a chave à meia-noite e falha calado transforma
  anúncio vivo em anúncio morto sem ninguém saber.
- **Anúncio expirado NÃO cancela desafio já aceito.** Um desafio aceito na sexta para jogar na
  terça é compromisso entre quatro pessoas; ele sobrevive ao anúncio que o gerou. (A memória
  das *8 inscrições que sumiram* é sobre exatamente isto: perguntar **o que mais morre junto**
  antes de expirar qualquer coisa.)

### ✅ O silêncio confirma; a contestação para tudo

Exigir o "sim" do perdedor faria com que **sumir** fosse a jogada ótima: bastaria não responder
para congelar o ranking de quem te ganhou. Por isso 72h sem resposta confirmam sozinhas.

**Contestar, porém, zera os dois lados** e a linha fica visível para o admin resolver. Duas
duplas que discordam do placar são um problema humano; o sistema não escolhe um lado sozinho.

### 🚫 O anti-farm (obrigatório, não polimento)

O Americano já ensinou: *três amigos criavam um Americano, lançavam os placares que quisessem e
fabricavam ranking sem enfrentar ninguém de fora.* O Desafio tem o **mesmo furo, e maior** —
bastam quatro pessoas e nenhum organizador.

Três defesas, e as três precisam existir juntas:

1. **Desafio NÃO move o Padelímetro.** Trilha própria, exatamente como o Americano virou a
   Trilha C do `RANKING.md`. Um placar sem testemunha não pode mexer no número que decide em
   que categoria a pessoa pode se **inscrever**. Promover isso depois, com base histórica, é
   decisão do Felipe — mas não no dia 1.
2. **Confronto repetido vale menos.** Contra a **mesma dupla**, no mesmo mês: o 1º vale cheio,
   o 2º vale metade, o 3º em diante vale **só a presença**. Quatro amigos jogando entre si toda
   terça param de subir na terceira semana.
3. **Ponto só soma, nunca subtrai** — igual à trilha de pontos do `RANKING.md`. Perder não dói;
   farmar não paga.

### 👤 Privacidade

O cartão do mural mostra nome, apelido, foto, categoria, cidade/clubes e retrospecto.
**Nunca telefone.** Contato só depois do aceite, e sempre passando por
`Services/ContatoDoJogador` — que já sabe que visitante anônimo não vê contato e que
pré-cadastro não expõe o de ninguém.

O mural inteiro exige login. Ele é uma lista de gente disponível com dia, hora e lugar: não é
material para o índice do Google.

---

## 3. O ranking

### Pontuação de um desafio confirmado

| Situação | Pontos |
|---|---|
| Jogou (qualquer resultado) | **+1 de presença** |
| Venceu | **`round(20 × (1 − expectativa))`**, limitado entre **5** e **20** |
| Perdeu | 0 (fica com a presença) |
| Não compareceu | 0, e leva o selo de falta |

A `expectativa` sai de **`Services/Padelimetro.Expectativa`** — a classe é matemática pura, sem
banco. O Desafio **lê** dali e **nunca escreve**. Dupla sem Padelímetro entra com expectativa
0,5 → **10 pontos**, o valor neutro.

Na prática: ganhar de quem é muito melhor vale até **20**; ganhar de quem é muito pior vale
**5**; entre iguais, **10**. O piso de 5 existe pra que ganhar de dupla mais fraca não vire
zero — seria desestimular justamente quem topa jogar com gente nova.

**Os pontos são gravados no desafio no momento do encerramento**, não recalculados na leitura —
mesmo padrão do `PrecoPorJogoCotado`. Sem isso, mexer na fórmula em novembro reescreveria o
ranking de agosto inteiro, em silêncio.

### Duas tabelas — e elas NUNCA se somam

- **Duplas** — o par fixo, identificado pela chave canônica `min(id)-max(id)`
  (`Services/ChaveDaDupla`). É a tabela com alma: *"os Loberos são 14-3 nesta temporada"*.
- **Jogadores** — os pontos que a pessoa fez com **qualquer** parceiro. Existe porque troca-se
  de parceiro o tempo todo.

⚠️ São recortes diferentes das **mesmas** partidas. Somar as duas conta a mesma pessoa duas
vezes — que foi exatamente o erro do Ranking Americano individual × duplas.

Colunas: `Pos · Dupla · Desafios · V · D · % · Pontos`. Filtros por **categoria** e por **clube**.

> ⚠️ **O filtro do ranking é por CLUBE, e não por cidade** — ao contrário do mural. A razão é o
> buraco da seção 8: `Clube.CidadeId` existe e nada preenche, então um filtro por cidade aqui
> devolveria tabela vazia sem dizer por quê. O mural filtra por cidade porque lê a cidade que as
> **pessoas** digitaram (via `CidadesSemRepetir`); o desafio guarda o **clube** onde foi jogado.
> Quando a coluna do clube for preenchida, o filtro por cidade vira uma linha a mais aqui.
>
> E o `<select>` de clube só oferece clubes que **já receberam desafio contado**: um filtro que só
> sabe esvaziar a tela ensina a pessoa a não usar filtro.

**Expira em 12 meses**, igual ao ranking anual: mata o "campeão eterno" e dá motivo para voltar
todo ano. O corte é lido do relógio em `RankingDeDesafios.QueContam` — o **mesmo** método que o
retrospecto do cartão do mural e a linha do perfil usam. Três leituras discordando sobre o
retrospecto da mesma dupla é como se deixa de acreditar nas três.

**Onde ele aparece:** tela própria em `/Desafios/Ranking`; um **cartão** no hub
`/Jogadores/Ranking` (não uma nona aba — a frase do topo daquela tela promete que tudo ali sai de
resultados de **torneio**, e o desafio não é isso); e uma **linha no perfil**
(`🥊 8 desafio(s) · 6 V · 75% · 71 pts`), que só existe quando a porta do módulo está aberta para
quem está olhando.

---

## 4. 🥊 O Cinturão

Em cada **categoria** existe **um dono do cinturão**. Quem vencer o dono num desafio **leva o
cinturão**.

> 🔒 **O recorte é a categoria, e não "categoria × cidade"** como esta espec dizia no primeiro
> desenho (decisão do Felipe, 11/08/2026). Dois motivos, e o segundo é o que manda:
>
> 1. `Clube.CidadeId` existe e nada preenche (seção 8) — o cinturão por cidade nasceria vazio ou
>    errado, calado.
> 2. **Densidade.** Com a base de hoje, um cinturão por categoria é o único recorte em que ele
>    troca de mão com alguma frequência. Cinturão que ninguém disputa é enfeite.
>
> Virar categoria × cidade depois é **uma coluna a mais**, não uma tabela nova.

Três regras que impedem o cinturão de morrer no primeiro mês:

- **O dono é obrigado a defender.** Recusou ou ignorou **3 desafios em 14 dias** → perde o
  cinturão para o **primeiro** que desafiou e não foi atendido ("o primeiro", e não "o melhor
  colocado": quem tentou antes esperou mais). Sem isso, ganhar e sumir seria a estratégia ótima.
- **Perder para qualquer um vale.** Nada de "só o top 10 desafia": o cinturão é a porta de
  entrada do jogador novo, não o clube fechado dos veteranos.
- **É da DUPLA, não do jogador.** Trocou de parceiro, começa de novo — é o que torna a dupla um
  personagem.

**Detalhes que a implementação fixou:**

- **O cinturão vago vai para o vencedor do primeiro desafio confirmado da categoria.** Sem isso
  ele nunca nasceria: não há dono para alguém tomar.
- **Cancelar um jogo já aceito NÃO conta** como fuga de defesa. A regra fala em *recusou ou
  ignorou*, e desmarcar tem motivo legítimo demais (chuva, lesão) para custar um cinturão.
- **Recusa de antes do reinado não conta.** Recusar quando não se tinha o cinturão é uma dupla
  qualquer dizendo que não pode jogar — sem esse corte, quem ganhasse hoje poderia perder amanhã
  por recusas de ontem.
- **O dono vê que está em risco antes de perder** (*"mais 1 e o cinturão passa adiante"*), lido
  do relógio. Regra que só aparece depois de executada vira *"o site tirou meu cinturão"*.
- **Uma tabela só** (`ReinadoNoCinturao`): o dono de hoje é a linha com `TerminouEm` nulo, e as
  demais são o histórico. Duas tabelas obrigariam toda troca a escrever nas duas.

⚠️ **Não lançar em cidade com menos de ~15 duplas ativas.** Cinturão com dois participantes é
piada, e piada em produção não volta atrás. Quem segura isso é `Desafios__Habilitado`.

## 4b. 🎾 A quadra do desafio

Desafio **aceito** num clube com `MarcacaoHorariosAtiva` mostra **"Reservar a quadra"**, que cai
no fluxo do `MarcarJogo` já no dia e no clube certos, com o rateio sugerido (*"o normal é cada
dupla pagar metade"*).

É só um atalho, de propósito: quem reserva, cobra e confirma continua sendo o `MarcarJogo` — nada
aqui inventa preço nem horário. É o gancho que faz o desafio encher a terça vazia do clube, e é
o argumento concreto da conversa comercial (seção 6).

---

## 5. Avisos

Um mural é uma máquina de spam esperando para ser ligada, e a cota de e-mail já estourou duas
vezes. A régua (`Services/AlcanceDoAviso`):

| Evento | Alcance | Por quê |
|---|---|---|
| Recebi um desafio | `AppEWhatsApp` | Pessoal, urgente (48h) e acionável — passa nos três critérios |
| Meu desafio foi aceito / recusado | `SoApp` | Acionável, mas não urgente |
| Placar lançado, preciso confirmar | `SoApp` | Idem |
| Subiu no ranking, ganhou/perdeu cinturão | `AppSemEmail` | Bilhete social puro |
| **"Nova dupla aberta na sua cidade"** | ❌ **não existe** | É broadcast — exatamente o que a Meta chama de spam e o que queimou o número |

**O mural é pull, não push.** No máximo um **resumo semanal** (quinta de manhã, `SoApp`):
*"7 duplas abertas em Gravataí nesta semana"*.

---

## 6. Dinheiro

**O MVP não cobra nada do jogador.** Cobrar por desafio mataria o volume, e o volume é o
produto. O valor aparece em três lugares:

1. **Reserva de quadra** — desafio aceito num clube com `MarcacaoHorariosAtiva` mostra
   *"Reservar a quadra"* e cai no `MarcarJogo`, com rateio sugerido (cada dupla paga metade).
   Aí sim nasce `Pagamento`, com a comissão de jogo (10%).
2. **Argumento de venda do clube** — *"o Padelizou traz jogo para a sua terça vazia"* é
   concreto, e o preço do plano do clube ainda está em aberto.
3. **Ranking de desafios por clube** — *"o clube onde mais se joga desafio em Gravataí"*.
   Vaidade de dono de clube é ativo comercial.

---

## 7. Modelo de dados

```
AnuncioDeDesafio
  Id, Jogador1Id, Jogador2Id?, ConviteToken?,
  CriadoEm, ValeAte, Status, Observacao

AnuncioDesafioCategoria (AnuncioId, CategoriaPadraoId)   ← espelha JogadorCategoria
AnuncioDesafioCidade    (AnuncioId, CidadeId)            ← espelha JogadorCidade
AnuncioDesafioClube     (AnuncioId, ClubeId)             ← espelha JogadorClube

Desafio
  Id, AnuncioId?,
  DesafianteJ1Id, DesafianteJ2Id, DesafiadoJ1Id, DesafiadoJ2Id,
  CategoriaPadraoId, ClubeId?, DataHora, Formato,
  Status, PropostoEm, RespondidoEm,
  SetsDesafiante, SetsDesafiado, GamesDesafiante, GamesDesafiado,
  LancadoPorId, LancadoEm, ConfirmadoPorId, ConfirmadoEm,
  PontosDesafiante, PontosDesafiado
```

Três decisões que precisam sobreviver a quem ler o código daqui a seis meses:

**① A dupla do desafio NÃO é `Dupla`.** `Dupla` é casada com `Torneio`, `Categoria`,
`GrupoTorneio` e o motor de mata-mata. Reusá-la aqui criaria a segunda cópia de meia dúzia de
regras de torneio. O Desafio guarda os quatro `JogadorId` diretamente, e a identidade da dupla
para o ranking é a chave canônica de `Services/ChaveDaDupla`.

**② As preferências são COPIADAS para o anúncio, não lidas do perfil.** O formulário nasce
marcado com o que a pessoa já respondeu (`JogadorCategoria` / `JogadorCidade` /
`JogadorClube`), mas grava no anúncio. Lendo do perfil, alguém mudando as preferências na
quinta faria o anúncio publicado na segunda passar a dizer outra coisa — e receberia desafio
para categoria que não aceitou.

**③ O placar guarda os pontos já calculados.** Ver a seção 3.

---

## 8. Buracos conhecidos

**`Clube.CidadeId` existe e nada preenche.** Filtrar o mural por "clubes da minha cidade"
devolveria **lista vazia**, e o defeito seria mudo: a tela abre e não vem ninguém. Enquanto
essa coluna não for preenchida, o casamento por cidade é feito pela cidade **do jogador**
(texto livre, via `NomeDeCidade` / `CidadesSemRepetir`).

**Densidade.** Mural vazio é pior do que mural nenhum: a pessoa entra uma vez, não vê nada e
não volta. O lançamento é **por cidade**, começando por onde já há base (Gravataí / Porto
Alegre), e a tela vazia precisa dizer *"seja o primeiro — publique e mande o link pro grupo"*,
com link compartilhável do anúncio. Nunca um "nenhum resultado encontrado".

---

## 9. Fases

| Fase | O que entra | Estado |
|---|---|---|
| **1 — O mural** | Anúncio com convite do parceiro, mural filtrado, desafiar/aceitar/recusar, placar com dupla confirmação, avisos | ✅ **feita** (11/08/2026) |
| **2 — O ranking** | As duas tabelas, filtros, cartão no hub, linha no perfil | ✅ **feita** (11/08/2026) |
| **3 — O cinturão** | Cinturão por categoria, defesa obrigatória, selo no perfil e no mural, reserva de quadra | ✅ **feita** (11/08/2026) |

A fase 1 é útil sozinha — é a que responde *"contra quem eu jogo sábado?"*. O ranking é o que
faz voltar; o cinturão é o que vicia.

**O módulo está completo e continua fechado.** O que falta não é código: é `Desafios__Habilitado=true`
e uma cidade com densidade suficiente para o mural não nascer morto.

> **A pontuação e o anti-farm entraram já na fase 1**, e não aqui. O motivo é que o ponto é
> **congelado** na linha do desafio no fechamento: deixá-lo para a fase 2 faria o ranking nascer
> sem histórico nenhum, com todos os desafios anteriores valendo nulo. A fase 2 é, por isso, só
> leitura — nada nela calcula ponto.
