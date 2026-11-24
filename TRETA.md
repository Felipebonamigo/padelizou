# Treta — formato com chave DTN e Finalíssima

> **Design para aprovação (24/11/2026). Nenhum código escrito.** Nível *architectural*
> (gera migration). Base: o PDF "Treta Chaves e DTN" (proposta para discussão, 5 páginas).

## O que o PDF pede, em uma tabela

| Módulo (= categoria) | Duplas | Grupos | Principal | DTN (quem PERDE leva) |
|---|---|---|---|---|
| Branco | 8 | 2 de 4 | 1º/2º → semis cruzadas → final | 4ºA × 4ºB, 1 jogo |
| Preto | 9 | 3 de 3 | 1º/2º; 2 melhores líderes direto à semi, 4 nas quartas | 2 melhores 3ºs na semi; o **perdedor** pega o pior 3º na final |
| Verde | 7 | 3 + 4 | igual ao Branco | 3ºA × 4ºB, 1 jogo |

Em todos: **3º de grupo de 4 é eliminado.** Finalíssima: os perdedores do DTN Branco (A+B)
e do DTN Verde (C+D) trocam de parceiro (A+C × B+D), 1 jogo; os 2 vencedores votam em um
dos 2 perdedores — esse atleta leva o **Troféu DTN**.

**Uma regra só cobre os três módulos:** os `ClassificadosPorGrupo` do topo vão pro
Principal, os **últimos N** de cada grupo vão pro DTN, o meio é eliminado. Configurável
(as regras ainda mudam), não três módulos fixos no código.

## Decisão central: o DTN é uma CATEGORIA à parte, ligada à de origem

O motor inteiro é chaveado por `(CategoriaId, Fase)`: avanço (`AvancoDaChave`), índice
único `(CategoriaId, Fase, NumeroNaFase)`, desenho da chave (`_ChaveDoMataMata`), coroação
("Final" → `Campeao`). **Duas chaves na mesma categoria quebram tudo isso.** Uma categoria
filha reaproveita tudo — é o mesmo caminho do `ChaveDireta` (duplas remontadas em categoria
paralela, conflito por pessoa já tratado na grade).

- **DTN Branco** = categoria filha da Branco. Quando a fase de grupos da Branco fecha, o
  robô copia as duplas dos últimos N lugares pra ela e monta o mata-mata.
- **Finalíssima** = outra categoria, com as duplas mistas criadas na hora (padrão que o
  Americano já usa, `RoboDoChaveamento.cs:566`).

### Mata-mata invertido = "quem avança é o perdedor"

Sai da mesma geometria, com duas inversões:
1. **No avanço**, a vaga da próxima fase é do perdedor (`AvancoDaChave` hoje pega `VencedorId`).
2. **No bye**, quem passa direto é o **pior** colocado (no Preto: o pior 3º vai direto à final DTN).

Com isso o Preto (3 duplas → quadro de 4, 1 bye) sai sozinho: semi entre os 2 melhores 3ºs,
o perdedor enfrenta o pior 3º. Branco e Verde (2 duplas) são só a final.

**Coroação invertida:** na final de uma categoria DTN ninguém vira `Campeao`; o perdedor
recebe `UltimaFase = "DTN"`. Isso também evita que o DTN entre na cédula do MVP
(`MvpDoTorneio.CandidatosAsync` pega quem é `Campeao`).

## Formato: preset "Treta", gravado como Padrão + papéis de categoria

Você escolheu "formato novo inteiro". **Recomendo que pra quem organiza seja isso — uma
opção "Treta" na criação do torneio — mas que no banco o torneio continue `Padrao`**, com o
papel morando na categoria. Motivo: há ~60 pontos que testam `Formato` como texto; os
`== "Padrao"` (prévia da grade, `Chaves.cs:393`, `TorneiosController.cs:530`) **excluiriam**
um valor novo, e cada um viraria uma decisão. Como os módulos são categorias Padrão com
grupos e mata-mata normais, o que muda é só o que é DTN/Finalíssima — e isso é da categoria.

Se preferir mesmo um `Formato = "Treta"` gravado, dá — custa revisar esses ~60 pontos.

## Migration (só aditiva)

`Categoria`:
- `PapelDaChave` `varchar(20)` nulo — `null` (hoje) · `"DTN"` · `"Finalissima"`.
- `CategoriaDeOrigemId` `int` nulo, FK pra `Categoria` (`Restrict`) — a DTN aponta pro módulo.
- `DuplasNoDtnPorGrupo` `int` padrão 0 — na categoria de origem; 0 = sem DTN (tudo que existe).
- `FinalissimaId` `int` nulo, FK pra `Categoria` — a DTN aponta pra Finalíssima que alimenta.

Tabela nova `VotoDaFinalissima` (`CategoriaId`, `VotanteId`, `CandidatoId`, `Em`), **PK
(CategoriaId, VotanteId)** — o banco segura o voto duplo, como no `TorneioMarcador`.

## O que NÃO pode contar (vai junto com a primeira entrega, não depois)

Sem filtro por fase/categoria hoje, um jogo de DTN mexeria em tudo. Categoria com
`PapelDaChave != null`:
- fora do ranking/pontos/títulos — `EstatisticasService.ContaNoRanking` e `DuplaContaNoRanking`;
- fora do Padelímetro — `PadelimetroService.Conta`;
- fora do MVP — candidatos e eleitores;
- sem taxa/cobrança — mesmos pontos do `ChaveDireta` (`TaxaExterno`, `DiaDoJogo`, Admin);
- inscrição bloqueada (quem entra é o robô), como no `ChaveDireta`.

Cada um com teste visto vermelho antes.

## Entregas (uma de cada vez, cada uma publicada em dev)

1. **Migration + exclusões** (ranking, Padelímetro, MVP, taxa, inscrição). Nada visível ainda.
2. **DTN**: campo "últimos N vão pro DTN" na categoria; robô cria a categoria filha ao fechar
   os grupos; avanço e bye invertidos; coroação "DTN"; a chave aparece com título "DTN" e o
   aviso "quem perde leva".
3. **Finalíssima**: ligar 2 DTNs a uma Finalíssima; quando as duas finais DTN fecham, a tela
   de quem organiza propõe a troca (A+C × B+D, editável) e cria o jogo; votação dos 2
   vencedores; resultado na página do torneio.
4. **Preset "Treta" na criação**: cria os módulos com grupos, cruzamento e DTN preenchidos.

Régua de autorização: nenhuma régua nova — tudo de quem organiza passa por
`EhOrganizadorAsync`; voto exige `[HttpPost]` + `[Authorize]` + ser vencedor da Finalíssima.

## Pontos que precisam de decisão sua

1. **Empate na votação** (o PDF deixa "a definir"). Com **2 eleitores** o 1×1 é provável.
   Opções: (a) quem organiza desempata; (b) sorteio do sistema; (c) votação incompleta =
   empate. Recomendo **(a)**.
2. **E o Preto?** A Finalíssima é "masculina: Branco + Verde". A dupla que perde o DTN Preto
   leva o troféu como dupla, ou há uma Finalíssima feminina?
3. **Quem é A e quem é B** na troca — sugestão: o sistema propõe, quem organiza confirma.
4. **Principal do Preto**: o PDF quer `1º líder × vencedor(3º líder × 3º segundo)`. A
   geometria atual dá `1º × vencedor(4º × 5º)`, cruzado. Ou ajusto o cruzamento, ou fica o
   da geometria. O PDF também pede "evitar repetição de chave" — o aviso já existe.
5. **"Ranking entre chaves"** (melhor líder, pior 3º): uso o critério que já existe
   (vitórias → saldo → games). Serve?
6. **O torneio Treta conta pro ranking/Padelímetro** (os jogos de Principal e grupos)? Se não,
   basta marcá-lo `Restrito`.
