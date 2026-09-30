# Assistências e acessibilidade (passo 3.7)

Para jogar com criança ou iniciante no mesmo sofá, e para quem é daltônico ou se incomoda com efeitos.
Tudo fica nas Opções (Opções › Corrida › **Acessibilidade › Ajustar**) e é salvo em `Settings`; a direção
assistida também se escolhe no cartão de cada jogador do lobby e, no online, no cartão de cada jogador da sala.

## Direção assistida (por assento)

| Nível | O que faz | O jogador |
|---|---|---|
| Nenhuma | nada | faz tudo |
| Freio automático | tira o pé e freia antes das curvas fortes (o ponto de frenagem da IA, com margem maior) | esterça, acelera, nitro |
| Volante assistido | compensa o empurrão da curva (o contra-esterço da IA), empurra de volta para o asfalto a partir de \|x\| 0,7 e só freia onde nem o volante todo seguraria a curva | esterça por cima, acelera, nitro |
| Completa | tudo acima com o volante inteiro: segura a faixa, desvia de quem está na frente e vai sozinha ao box quando o tanque não chega à próxima passagem (a mesma conta da IA, `sim/fuel.ts`) | só acelera e usa o nitro |

**O jogador sempre por cima**: o freio dele vale em todo nível; o volante dele (acima de 0,15) sobrepõe o da
assistência completa e se soma ao contra-esterço no volante assistido; acelerar e o nitro são sempre dele — a
assistência só tira velocidade, nunca põe. A borda direita se abre no trecho do box para quem esterça para lá
(o volante assistido não tranca a entrada do box).

No HUD, quem joga com assistência tem o selo discreto **ASSIST** ao lado do nome. O selo também aparece onde o
HUD mostra o nome de outro jogador — companheiros do co-op (tela dividida) e a classificação da célula livre
(3 jogadores) — e no resultado da corrida (local e online) uma marca **ASSIST** discreta fica ao lado do nome de
quem correu assistido (o nível inteiro no título). O resultado lê o nível da config da corrida, a que a simulação
usou (`withRaceAssists`), e não os humanos da sessão: copa retomada e carreira montam os humanos do save sem ele.

### Como funciona (núcleo, determinístico)

- O nível vai na config do humano: `HumanEntry.assist` (`'none' | 'brake' | 'steer' | 'full'`, ausente = nenhuma).
  `src/core/sim/assist.ts` (`assistInput`) mistura a entrada do jogador com a lógica da IA dentro do `stepRace`,
  antes da física. Não tem estado próprio — tudo sai do carro e da pista no tick —, então salvar/retomar e o
  hash do lockstep não mudam de forma.
- Reaproveita a IA sem duplicar: `brakingTarget` (ponto de frenagem) e `counterSteer` (contra-esterço) foram
  extraídos de `aiInput` em `sim/ai.ts` sem mudar uma conta (hash de corridas IA×IA idêntico antes e depois), e
  `nearestAhead` passou a ser exportado.
- Constantes em `constants.ts` (`ASSIST_*`): margens de curva 0,9 (freio, o jogador esterça), 1,1 (volante
  assistido: só o limite físico) e 1,05 (completa), olhar 26 segmentos (+30 em velocidade máxima), borda 0,7 com
  ganho 6, faixa da completa ±0,55. Entram na impressão do conteúdo do online (`CONTENT_FINGERPRINT`).
- De onde vem o nível: lobby (cartão de cada assento) e, para o que monta os humanos a partir do save (copa
  retomada, carreira), a opção do assento (`Settings.seatAssists`) — `src/access/humans.ts`.

### Online

Na tela da sala, cada jogador local tem a linha **Direção** no próprio cartão (Nenhuma, Freio automático,
Volante assistido, Completa), como no lobby local. A escolha é a do assento local nas opções (P1, P2 deste
computador) e fica salva lá (`OnlineController.assistOf`/`cycleAssist`); trocar publica o `info` na hora e a
sala de todos mostra, ao lado do nome de cada jogador (remoto inclusive), o selo **ASSIST · Freio/Volante/
Completa** (`assistTagText`). Muda à vontade até o "pronto" (travada com ele, como o carro) e, para o
anfitrião, até o LARGAR; depois da largada não muda mais. O anfitrião larga com o que declara naquele instante,
não com o eco da sala: trocar e apertar LARGAR em seguida não perde a troca.

Na rede, vai no `info` do lobby (`LobbyPlayer.assist`), o anfitrião a copia para a largada
(`SeatAssignment.assist`) e todo computador monta a mesma `HumanEntry.assist` (`raceConfigFrom`). Como a
mistura roda dentro do `stepRace`, entra no lockstep como qualquer entrada: funciona online. O campo só aparece
quando há assistência (a mensagem de quem não usa é a de antes); valor desconhecido invalida a mensagem. O
formato das mensagens não mudou com o seletor (o campo já existia): `PROTOCOL_VERSION` continua o mesmo. Build
com outra regulagem tem outra impressão de conteúdo e não entra na mesma sala.

No HUD online só aparecem os jogadores deste computador (viewports, companheiros do co-op e etiquetas 3D vêm dos
jogadores locais), então o selo de um jogador remoto aparece na sala e no resultado, não durante a corrida.

### Medido (`tests/assist.test.ts` e medições com `createRace`/`stepRace`)

18 pistas de dificuldade 4–5, 2 voltas de contra-relógio, carro Falcão GT:

| Quem pilota | Entradas na grama | Tempo médio |
|---|---|---|
| Iniciante (pé no fundo, volante só reagindo ao desvio), sem assistência | 264 | 153,4 s |
| O mesmo iniciante com freio automático | 0 | 168,6 s |
| Mãos fora do volante, volante assistido | 0 | 150,3 s |
| Mãos fora do volante, assistência completa | 0 | 154,3 s |

Numa corrida inteira com 11 carros da IA (profissional) e combustível, a completa sem volante termina todas as
32 pistas, para no box quando precisa e só sai do asfalto na faixa entre a pista e o box (2 entradas por parada).

## Cores dos jogadores (daltonismo)

Três paletas (`src/core/data/drivers.ts`): **Padrão**, **Vermelho/verde** (protanopia e deuteranopia) e
**Azul/amarelo** (tritanopia). Toda cor de jogador passa por `seatColor(seat, palette)`/`seatColors(palette)` —
lobby, garagem, online, resultado, classificação, HUD, minimapa e as etiquetas 3D (a cor chega pelo viewport).
A tela usa sempre a paleta das opções *deste* computador, não a cor gravada na config/save (que é a de quem montou
a corrida). Não há mais tabela fixa (`SEAT_COLORS` saiu); um teste varre `src/` e `scripts/`.

As de daltonismo foram escolhidas por busca, maximizando a menor distância ΔE (CIE76 em Lab) entre as quatro na
visão simulada (Machado, Oliveira e Fernandes 2009, severidade 1), sem perder distância na visão normal nem do cinza
dos carros da IA no minimapa (`#9aa3b5`), todas claras (L* ≥ 55) para o vidro escuro do HUD:

| Paleta | P1..P4 | menor ΔE (normal / protan / deutan / tritan) |
|---|---|---|
| Padrão | `#ffd23f #3ddc84 #4fc3f7 #ff7ab6` | 75 / 20 / 29 / 13 |
| Vermelho/verde | `#f7ee2a #2ef5c6 #2f8fff #ff5c5c` | 86 / 38 / 40 / 35 |
| Azul/amarelo | `#f7ee2a #5cf52a #d45cff #ff5c5c` | 57 / 3 / 15 / 47 |

O teste exige ΔE ≥ 30 entre jogadores e ≥ 20 até o cinza da IA na visão a que cada paleta se destina.

## HUD, texto e efeitos

- **Tamanho do HUD** 80–150% (passos de 10%): `--hud-scale` no `#hud`; o `--s` de cada viewport vira
  `calc(<escala do viewport> * var(--hud-scale))`.
- **Texto grande** nos menus: `--text-scale` 1,2 no `#ui` (redefine `--fs`, `--fs-sm`, `--fs-md`, `--fs-lg`).
- **Reduzir efeitos**: sem tremor de câmera e sem faíscas de batida (pelo `RenderOptions.reduceEffects`), sem
  linhas de velocidade e sem piscadas no HUD (classe `reduce-fx` no `#hud`), animações dos menus instantâneas.
- **Tremor de tela** (liga/desliga só o tremor da câmera nas batidas): morava em Opções › Geral e veio para esta
  tela em 30/09/2026, quando o Visual (Moderno/Retrô) entrou lá — cada coluna de Opções comporta dez linhas.
- `src/access/apply.ts` aplica tudo isso na criação da sessão e a cada mudança de opções; CSS em `src/access/access.css`.

## Arquivos

`src/core/sim/assist.ts` · `src/access/` (tela, textos, aplicação, humanos, CSS) · ganchos em `sim/race.ts`,
`sim/ai.ts`, `types.ts`, `constants.ts`, `data/drivers.ts`, `game/contracts.ts`, `settings.ts`, `session.ts`,
`online-session.ts`, `net/protocol.ts`, `ui/menus.ts`, telas (lobby, opções, resultado, garagem, online),
`render/hud.ts`, `renderer.ts`, `effects.ts` · `tests/assist.test.ts`, `tests/online-assist.test.ts` (seletor
na sala, largada, trava, rótulos) e o playtest `scratch/pt-online-assist.mjs` (dois navegadores no mesmo relay).

## Em aberto (decisão do dono)

- A completa anda a 100% da velocidade nas retas e vence a IA profissional na maioria das pistas. Para brincar
  com criança é o esperado; se incomodar, dá para limitar a reta da completa ou tirar corridas assistidas dos
  recordes e conquistas (hoje contam como qualquer outra). Medido na onda C (só acelerador, Falcão, 20 carros;
  posição média · vitórias): hoje amador 1,0 · 100%, profissional 1,45 · 70%, campeão 5,1 · 2% (27% pódio). Um teto
  de velocidade nas retas (`car.speed > topSpeed × teto` corta o acelerador, × `NITRO_SPEED_MULT` com nitro) daria:
  0,95 → profissional 1,7 · 61%, campeão 7,0 (5% pódio); 0,92 → profissional 2,5 · 27%, campeão 11,4. O amador
  não muda com nenhum teto a partir de 0,88 (a IA amadora anda a ~0,74 na reta). Com teto, o teste "completa vai
  sozinha ao box" precisa de uma volta a mais (gasta menos).
- Texto grande em 1024×640 deixa o lobby apertado (o nome do carro encosta na linha do nome).
- Online, o selo de um jogador remoto não aparece durante a corrida: o HUD não mostra o nome de um remoto em lugar
  nenhum (os companheiros do co-op vêm só dos viewports deste computador). Ele aparece na sala e no resultado. Se
  fizer falta, os companheiros do co-op online podem passar a listar os remotos (com o selo).
