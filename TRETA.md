# Treta — formato com chave DTN e Finalíssima

> **Publicado em dev e prod** (#468, #469, #472, #473 — 25 a 27/11/2026). Implementado (aprovado: "pode fazer tudo") na branch
> `claude/determined-heisenberg-5212c2`. Base: o PDF "Treta Chaves e DTN" + respostas do
> organizador. Migrations `TretaChaveDtn` e `TieBreakNaPenultima` (só aditivas).
>
> ## Como ficou (difere do design abaixo nestes pontos)
>
> - **DTN usa CÓPIAS das duplas** (categoria `ChaveDireta` + `PapelDaChave = "DTN"`), não as
>   originais: ~10 caminhos (W.O., correção, reabrir) carimbam `UltimaFase` na dupla do jogo e
>   o DTN apareceria no pódio do módulo. Cópias nascem `Pago = true` (padrão dos times), do
>   pior pro melhor — o bye cai no pior. Serviço: `RoboDoChaveamento.Dtn.cs`, `ChaveDtn`.
> - **DTN e Finalíssima nascem só quando precisam** (grupos fechados; organizador confirma a
>   troca). O módulo guarda `ParDaFinalissimaId` em vez de a Finalíssima existir antes.
> - **Troféu**: sem tabela de votos — `Categoria.TrofeuDtnJogadorId`, registrado uma vez pela
>   dupla vencedora; o organizador corrige (`FinalissimaDtn`, `TorneiosController.Treta`).
> - **W.O. no DTN** ("joga a que veio jogar"): sem regra nova — o organizador troca o atleta da
>   dupla com o "Trocar parceiro" que já existe (`DuplasController.TrocarParceiro`).
> - **Tie no 5x5**: `Torneio.TieBreakNaPenultima` — o limite par para de estender.
> - **Preset**: caixa "Torneio Treta" na criação (fora do ranking + tie no 5x5 + DTN em toda
>   categoria); na gestão de cada categoria, "quantos pro DTN" e "Finalíssima com". As
>   categorias vêm do catálogo, então os módulos se chamam pela categoria, não Branco/Preto/Verde.
> - **Não visto rodando no navegador** (sem Postgres nesta sessão): só pela suíte.
>
> ---
>
> *Design original, para referência:*

## As regras, fechadas

| Módulo (= categoria) | Duplas | Grupos | Principal | DTN (quem PERDE avança) | Troféu DTN |
|---|---|---|---|---|---|
| Branco | 8 | 2 de 4 | 1º/2º → semis → final | 4ºA × 4ºB | vai pra Finalíssima |
| Verde | 7 | 3 + 4 | 1º/2º → semis → final | 3ºA × 4ºB | vai pra Finalíssima |
| Preto | 9 | 3 de 3 | 1º/2º; os 2 melhores líderes direto à semi | semi: 2 melhores 3ºs; o perdedor pega o pior 3º na final | **sem Finalíssima**: quem perde a final DTN é votado |

- **Regra geral** (vale se o número de duplas mudar): os 2 primeiros de cada grupo vão pro
  Principal, o **último** vai pro DTN e o meio é eliminado.
- **Finalíssima (Branco + Verde)**: as duas duplas que perderam o DTN trocam **o jogador do
  mesmo lado** — direita com direita —, e cada um mantém a posição que jogou no torneio.
  Branco (E₁, D₁) e Verde (E₂, D₂) viram **E₁ + D₂ × E₂ + D₁**. 1 jogo.
- **Troféu DTN (individual)**: a dupla vencedora do jogo decisivo (Finalíssima, ou a final
  DTN do Preto) escolhe **em consenso** um dos dois perdedores. É **uma escolha por dupla**,
  então não existe empate.
- **Cruzamento do Principal**: o padrão do sistema, inclusive no Preto.
- **Comparação entre chaves** (melhor líder, pior 3º): vitórias → saldo → games → sorteio.
- **Jogos**: 1 set até 6, tie-break no 5×5 (ver ⚠️ abaixo).
- **Ranking**: o torneio inteiro **não conta** pro ranking nem pro Padelímetro.

## Decisão central: o DTN é uma CATEGORIA à parte, ligada à de origem

O motor inteiro funciona com uma chave por categoria, `(CategoriaId, Fase)`: avanço
(`AvancoDaChave`), índice único `(CategoriaId, Fase, NumeroNaFase)`, desenho da chave
(`_ChaveDoMataMata`) e coroação ("Final" → `Campeao`). **Duas chaves na mesma categoria
quebram tudo isso.** Uma categoria filha reaproveita tudo — é o caminho do `ChaveDireta`, em
que duplas remontadas jogam numa categoria paralela e a grade já evita conflito por pessoa.

- **DTN Branco** = categoria filha da Branco. Quando os grupos da Branco fecham, o robô copia
  as duplas dos últimos lugares e monta o mata-mata.
- **Finalíssima** = outra categoria, com as 2 duplas mistas criadas na hora. O Americano já
  cria dupla nova assim (`RoboDoChaveamento.cs:566`).

### Mata-mata invertido = "quem avança é o perdedor"

Sai da mesma geometria, com duas inversões:
1. **No avanço**, a vaga da próxima fase é do perdedor (`AvancoDaChave` hoje pega `VencedorId`).
2. **No bye**, quem passa direto é o **pior** colocado. No Preto, o pior 3º vai direto à final.

**Coroação invertida:** na final de uma categoria DTN ninguém vira `Campeao`; o perdedor
recebe `UltimaFase = "DTN"`. Isso também evita que o DTN entre na cédula do MVP
(`MvpDoTorneio.CandidatosAsync` pega quem é `Campeao`).

### A troca de lado na Finalíssima

O lado de cada um **não é confiável no banco**: `Dupla.LadoJogador1` só existe pro jogador 1,
e o do jogador 2 vem do perfil, que pode estar vazio ou "Ambos". Então, quando as duas finais
DTN fecham, quem organiza vê a troca **proposta** pelos lados conhecidos e **confirma ou
corrige** antes de o jogo ser criado.

### O Troféu DTN: um campo, não uma votação

Como a dupla decide em consenso, não há voto a contar. Basta a categoria decisiva guardar
**quem levou** (`TrofeuDtnJogadorId`). Quem pode registrar: os 2 jogadores da dupla vencedora
e quem organiza (`EhOrganizadorAsync`); quem organiza pode corrigir. Sai a tabela de votos do
design anterior.

## Formato: preset "Treta", gravado como Padrão + papéis de categoria

Pra quem organiza, "Treta" é uma opção na criação do torneio. No banco o torneio continua
`Padrao`, com o papel morando na categoria. Motivo: há ~60 pontos que testam `Formato` como
texto, e os `== "Padrao"` (prévia da grade, `Chaves.cs:393`, `TorneiosController.cs:530`)
**excluiriam** um valor novo. Os módulos são categorias Padrão comuns; o que muda é só o que
é DTN ou Finalíssima.

**Fora do ranking**: hoje só o `Restrito` (que também exige chave de acesso na inscrição) e o
`TimeExclusivoId` tiram um torneio do ranking. Proponho uma coluna própria,
`Torneio.ContaNoRanking` (padrão `true`), lida em `EstatisticasService.ContaNoRanking` — serve
pra qualquer torneio-festa, não só o Treta.

## Migration (só aditiva)

`Categoria`:
- `PapelDaChave` `varchar(20)` nulo — `null` (como hoje) · `"DTN"` · `"Finalissima"`.
- `CategoriaDeOrigemId` `int` nulo, FK pra `Categoria` (`Restrict`) — a DTN aponta pro módulo.
- `DuplasNoDtnPorGrupo` `int` padrão 0 — na categoria de origem; 0 = sem DTN (tudo que existe).
- `FinalissimaId` `int` nulo, FK pra `Categoria` — a DTN aponta pra Finalíssima que alimenta;
  nulo = o troféu sai da própria final DTN (caso do Preto).
- `TrofeuDtnJogadorId` `int` nulo, FK pra `Jogador`.

`Torneio`:
- `ContaNoRanking` `bool` padrão `true`.

## O que NÃO pode contar (vai na primeira entrega, não depois)

Hoje não há filtro por fase ou por categoria: um jogo de DTN mexeria em tudo. Categoria com
`PapelDaChave != null`:
- fora do ranking, pontos e títulos — `EstatisticasService.ContaNoRanking` e `DuplaContaNoRanking`;
- fora do Padelímetro — `PadelimetroService.Conta`;
- fora do MVP — candidatos e eleitores;
- sem taxa ou cobrança — os mesmos pontos do `ChaveDireta` (`TaxaExterno`, `DiaDoJogo`, Admin);
- inscrição bloqueada (quem entra é o robô), como no `ChaveDireta`.

Cada um com teste visto vermelho antes.

## Entregas (uma de cada vez, cada uma publicada em dev)

1. **Migration + exclusões** (`ContaNoRanking` do torneio; ranking, Padelímetro, MVP, taxa e
   inscrição das categorias DTN/Finalíssima). Nada visível ainda.
2. **DTN**: campo "últimos N vão pro DTN" na categoria; robô cria a categoria filha ao fechar
   os grupos; avanço e bye invertidos; coroação "DTN"; a chave aparece com título "DTN" e o
   aviso "quem perde avança".
3. **Finalíssima e Troféu DTN**: ligar 2 DTNs a uma Finalíssima; troca de lado proposta e
   confirmada por quem organiza; registro do troféu pela dupla vencedora ou por quem organiza;
   o troféu aparece na página do torneio.
4. **Preset "Treta" na criação**: cria os módulos com grupos, DTN e "não conta no ranking".

Régua de autorização: nenhuma régua nova — tudo de quem organiza passa por
`EhOrganizadorAsync`; registrar o troféu exige `[HttpPost]` + `[Authorize]` + ser da dupla
vencedora do jogo decisivo ou organizador.

## ⚠️ Ainda em aberto

1. **W.O. no DTN** — resposta: *"não, joga a que veio jogar"*. Ambígua. Possibilidades:
   (a) quem faltou é trocado por outra dupla (qual? a eliminada do grupo?);
   (b) a que veio avança no lugar de quem faltou;
   (c) quem organiza troca a dupla do jogo na mão.
   Recomendo **(c)**: cobre qualquer caso sem regra nova.
2. **Tie-break no 5×5**: hoje o sistema joga o "até 6" assim: no 5×5 estende até 7, no 6×6
   abre tie-break e fecha 7×6 (regra de 22/11). "Tie no 5×5" quer dizer **fechar 6×5**, como
   o "até 9" fecha 9×8? Se for isso, é uma **opção nova no placar** (servidor + Mesa offline
   + telas), útil pra qualquer torneio, e vira uma entrega separada, antes da 4.
