# Carros

## Modelos

Cada um dos 13 estilos de carroceria (`CarDef.body`, lista em `CAR_BODIES`) tem um modelo 3D procedural
próprio, com silhueta que se reconhece de longe. Não existe modelo genérico de reserva: estilo sem
construtor é erro (`buildModel`), e `tests/car-models.test.ts` confere que as 13 malhas são diferentes.

| Estilo | Silhueta | O que o denuncia de longe | Roda |
|---|---|---|---|
| `gt` | grã-turismo de motor dianteiro, fastback | capô longo entre para-lamas que sobem, "ducktail", 4 lanternas redondas, saída de ar atrás da roda | `sport5` |
| `muscle` | muscle car | frente alta e chata com grade de ponta a ponta e faróis redondos cromados, tomada de ar no capô, traseira curta, pneus traseiros maiores | `mag` |
| `hatch` | hot hatch | cabine alta até o fim do carro, tampa quase vertical, aerofólio grande no teto, para-lamas alargados com borda preta, escape central duplo | `multi` |
| `sedan` | sedã econômico | três volumes (capô, cabine, porta-malas), friso cromado das janelas, placas, calotas | `hubcap` |
| `electric` | fastback elétrico | arco único e liso, bico fechado, teto de vidro, faixa de luz na frente e atrás, sem escapamento | `aero` |
| `rally` | carro de rali | suspensão alta, para-lamas em caixa, asa na tampa, 4 faróis de milha, painel de número, para-barros | `dish` (branca) |
| `hyper` | hipercarro em cunha | muito baixo, cabine-bolha avançada, entradas laterais enormes, asa em pescoço de cisne, difusor | `center` |
| `classic` | esportivo dos anos 60 | capô longo abaulado, para-lamas bojudos, boca oval cromada, para-choques cromados partidos, faixa branca no pneu | `wire` |
| `wedge` | cunha dos anos 80 | planos retos, faróis escamoteáveis (sobem à noite), frisos nas portas, venezianas e grade preta atrás | `aero` |
| `pickup` | picape | cabine simples alta, caçamba aberta (com o interior), santantônio com faróis, grade e para-choques cromados | `steel` |
| `prototype` | protótipo de endurance | bico rente entre para-lamas altos, cockpit-bolha, barbatana de tubarão, asa de ponta a ponta | `center` |
| `micro` | microcarro | 3,4 m (o único mais curto), cabine alta e redonda, "olhos" redondos, teto em outra cor | `hubcap` |
| `roadster` | conversível de dois lugares | sem teto: cockpit aberto com bancos, volante e piloto de capacete, para-brisa baixo, santantônio, corcovas | `mag` |

Todos ocupam a pegada de colisão (~4,4 × 1,9 m; o teste prende o casco em |x| ≤ 1,0 m e |z| ≤ 2,35 m,
com as rodas dentro da largura).

### Como um modelo é feito (`src/render/cars/`)

- `kit.ts` — o construtor de malha (`MeshBuilder`) e as primitivas fechadas com as faces para fora:
  cuboide de 8 cantos, caixa, viga, torno, cilindro, prisma de perfil (`extrudeZY`), caixa por dentro
  (caçamba, cockpit) e o loft de anéis. Faces planas (sombreado plano), triângulo degenerado descartado.
- `body.ts` — a carroceria por **seções-chave** (`Sec`: fundo, topo, meia largura, abaulado, ombro,
  vinco). O loft de 22 pontos por anel já sai com os **arcos das rodas recortados** (poço escuro, arco
  de 6 facetas), o para-lama que alarga (`flare`) e sobe (`rise`) sobre a roda, a faixa lateral (camada
  B) no vinco e as duas faixas do capô (camada A). A cabine é outro loft (para-brisa, teto, vidro
  traseiro). `BodyShape.topAt` responde a altura da superfície para os detalhes pousarem nela.
- `details.ts` — peças comuns: faróis e lanternas (caixa inclinada ou redondos), grade, retrovisores,
  escapamento, aerofólio com suportes, placa, pinça de freio, difusor, divisor, peças deitadas no capô.
- `wheels.ts` — 9 desenhos de roda (pneu com ombro e flanco, aro, poço e o desenho), em escala unitária.
- `styles/<estilo>.ts` — um arquivo por estilo: só números (seções, eixos) e a lista de peças.
  É a porta para a arte final: trocar um arquivo por um glTF não mexe no resto.
- `paints.ts` — os pincéis (pintura, acento, faixas, cromo, vidro, lentes, borracha) e a escolha pura
  da segunda cor e da pintura de cada carro. `material.ts` — o material único. `flame.ts` — a chama.

### Cor, segunda cor e pintura

- A cor do carro (`CarDef.color`) pinta a carroceria; a segunda cor (`CarDef.accent`, ou, sem ela,
  `defaultAccent`: azul-marinho no branco, quase preto nas cores quentes, branco nas frias) pinta
  aerofólio, asa, trava das rodas, pinças, capacete e as faixas.
- **Pintura (camadas A e B)**: faces marcadas com a camada A (faixas duplas do capô ao teto, teto do
  micro) ou B (faixa lateral no vinco, aro de algumas rodas) viram a segunda cor quando o carro usa a
  camada. O n-ésimo carro de um estilo (na ordem de `CARS`) usa a n-ésima pintura da lista do modelo —
  por isso o **Falcão GT** (1º GT) sai com faixas no capô e o **Boitatá GT** (2º) com a faixa lateral.
  Carro novo de um estilo já usado ganha a próxima pintura da lista sem mudar nada no renderizador.

### Material (um programa para todos os carros)

Um `MeshPhysicalMaterial` com o shader estendido (`material.ts`) serve carroceria, vidro, cromo, lentes
e rodas de todos os estilos. Por vértice: cor, se recebe pintura/acento, camada, rugosidade, metal,
emissivo de farol e de lanterna, farol escamoteável. Por instância: cor, segunda cor e bits (freio,
camada A, camada B). O freio acende as lanternas da própria instância (sem trocar malha). Verniz
(clearcoat) só nas faces pintadas e só na qualidade alta; o reflexo do céu vem do env map (PMREM) que o
céu já monta, reforçado nas peças metálicas e no vidro e contido na pintura (a cor manda).

### Instanciamento e animação

Os carros são agrupados por estilo: cada estilo presente é **uma** chamada de desenho para todos os
carros dele, cada desenho de roda presente é outra, mais a sombra de contato e a chama (uma cada). Na
qualidade baixa e nos carros a mais de 30 m à frente, as rodas viram a roda simples (pneu e disco liso,
112 triângulos), todas numa chamada; rodas não fazem sombra. Orçamento e medidas antes/depois em
`docs/DESEMPENHO.md` (seção 5).
Giro de roda (limitado a 40% do passo do desenho por quadro, para os raios não "andarem para trás"),
rolagem, mergulho no freio, chacoalho na derrapagem, chama do nitro (saindo dos escapes de cada modelo;
no elétrico, do difusor), etiquetas de nome (acima do teto de cada modelo) e o fantasma translúcido (com
a malha do estilo do carro dele) continuam como antes.

### Como ver

`npm run dev` e `node tools/render-harness.mjs <porta> <filtro>`:
- `showroom` / `showroom_tras` — os 13 estilos em grade na pista (`?showroom=1[&carview=rear34]`);
- `folha_frente34`, `folha_tras34`, `folha_lado`, `folha_noite` — folha de contato com os 13 estilos e o
  2º GT (`?sheet=front34|rear34|side|rear`, 1920×1080);
- `carro_<estilo>_<vista>` — um estilo de perto (`?carview=front34|rear34|side|rear&body=<estilo>`; ou
  `&car=<id>`). Estilos sem carro nos dados ganham um carro de demonstração só no harness.
