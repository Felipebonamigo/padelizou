# Punhos de Shaolin

Um beat-em-up de monges no estilo *Mortal Kombat: Shaolin Monks*, em HTML5 Canvas, sem um
único sprite, imagem ou arquivo de áudio: os lutadores são bonecos articulados desenhados na
hora, os cenários são gradientes e formas cacheadas, e o som é sintetizado com Web Audio.

**Pra jogar:** abra `jogo/index.html` no navegador (funciona por `file://`, sem servidor) — ou, no
desktop, `npm ci && npm run start` em `jogo/`, que abre o Electron servindo o jogo por `app://` com
CSP. Tudo é local, inclusive as fontes (`jogo/fontes/`, licença OFL): o jogo roda sem internet, e o
desktop não faz nenhuma requisição de rede (conferido por net-log).

⚠️ **Fica FORA do app .NET de propósito.** Nada aqui é servido pelo Padelizou nem entra no
deploy. Se um dia for pra `Padelizou/wwwroot/jogo/`, ele vira público em `/jogo/index.html`
na hora — o `UseStaticFiles` roda ANTES do portão de Acesso Antecipado (`Program.cs`).

## Como se joga

| | P1 (teclado) | P2 (teclado) | Controle | Toque |
|---|---|---|---|---|
| Mover | Setas | W A S D | analógico / d-pad | joystick (esquerda) |
| Correr | dois toques na direção | dois toques | empurrar até o fim | empurrar até o fim |
| Soco | Z | J | X | SOCO |
| Chute | X | K | B | CHUTE |
| Especial (gasta chi) | C | L | Y | CHI |
| Pular | Espaço | H | A | PULO |
| Agarrar / Finalizar | V | U | RB | AGARRA |
| Defender (segurar) | B | I | LB ou LT | DEF |
| P2 entra | — | J | Start no 2º controle | — |
| Pausa · mudo · tela cheia | P ou Esc · M · F ou F11 | | Start | ❚❚ |
| Menus | ↑ ↓ ← → · Enter · Esc | | analógico · A · B | toque no item |

- **Três socos seguidos lançam** o inimigo pro alto; chute no ar continua o combo (malabarismo).
- **Correndo + soco ou chute** é a investida (Voo do Dragão / Estocada).
- **Agarrar** de perto: soco arremessa (quem está no caminho apanha), chute é joelhada.
- **Defender** segura 80% do dano de quem vem pela frente. De costas, não segura nada.
- **Chi** enche a cada golpe de punho (projétil e arremesso não contam) e paga o especial:
  o Long solta o *Sopro do Dragão* (bola de fogo), o Shen gira a *Tempestade do Bastão*, e a Lian
  lança o *Puxão do Rio*: a corrente traz o primeiro inimigo da fila (nunca chefe, nem quem está no
  ar ou defendendo, que só levam o dano).
- **Três monges**: Long (equilibrado), Shen (bastão, mais vida e alcance) e **Lian** (corrente de
  alcance longo, rápida e frágil).
- **Quebra de guarda**: o chute do jogador quebra a guarda de quem está defendendo (atordoado curto).
  É a resposta ao **Monge Renegado**, que defende muito e contra-ataca depois de bloquear.
- **Finalização:** inimigo com pouca vida que levanta do chão fica **atordoado** (estrelinhas,
  "FINALIZE!"). Agarre-o nesse instante: vale 500 pontos + o dobro dos pontos dele, e enche o chi.
- **Vasos** quebram e soltam chá (vida) ou pergaminho (chi). Inimigo morto às vezes solta também.
- Só **dois inimigos atacam ao mesmo tempo** — é a régua de justiça do gênero. O resto cerca.
- **O Templo**: ao fim de cada fase o jogador ganha **karma** pelos pontos daquela fase (e só
  dela) e aprende golpes entre as fases, ou pelo item Templo do menu. Sete melhorias: Sequência de
  Cinco (a corrente vira cinco socos antes do lançador), Contra-golpe (começar a defender até 0,15 s
  antes do golpe inimigo ligar APARA: zero dano e o atacante atordoado), Especial no Ar, Agarrão
  pelas Costas (suplex, nunca em chefe), Vigor (+20% de vida), Respiração do Templo (+3 de chi por
  segundo sem atacar) e Punhos de Ferro (+15% de dano). Valem pro P2 que entra no meio.
- **O cenário mata**: braseiros, espinhos e a beira do poço, encostados no muro ou na frente, fora
  do caminho obrigatório. Inimigo comum que cai numa zona arremessado, lançado ou derrubado morre
  na hora, com bônus. Chefe é empurrado pra fora; o jogador leva dano fixo e nunca morre de uma vez.
- **Pontos de controle**: "tentar de novo" recomeça na última onda disparada, com os pontos de
  quando ela disparou.
- **Arena** (menu): uma tela, ondas infinitas, chefe a cada cinco, recorde próprio, e karma pela
  metade do da campanha.
- **A régua da finalização é a vida**, não o estado: só finaliza abaixo de 22% (10% em chefe). O
  atordoado do contra-golpe num inimigo com vida cheia é agarrão comum.

Quatro fases (Pátio do Templo, Floresta Viva, Poço das Almas, Torre do Feiticeiro), cada uma
com ondas de inimigos e um chefe: Mestre Sombra (teleporta ao levar três golpes), Grão-Presa
(armadura: golpe leve não interrompe), o Gigante do Poço (pancada no chão dos dois lados) e o
Feiticeiro (teleporta, atira caveiras e invoca Sombras a cada terço de vida). **Todo chefe tem duas
fases**: ao cruzar 50% da vida entra em FÚRIA (1 s invulnerável, onda de choque) e volta mais duro.
Inimigos comuns: Sombra, Garra, Bruto (armadura), Arqueiro, **Lanceiro** (estoca de longe) e
**Monge Renegado**. Com a tela travada, inimigo que entrou na arena não sai mais dela, e ninguém
ataca de onde o jogador não alcança (defeito achado pelo simulador).

## O que fica guardado

`progresso.json` (Electron: `%APPDATA%/punhos-de-shaolin/` no Windows, `~/.config/punhos-de-shaolin/`
no Linux) ou `localStorage` no navegador: fase alcançada (**Continuar** no menu), recorde,
dificuldade (Fácil · Normal · Difícil — vida e dano dos inimigos), opções (música, efeitos, tremor
de tela, tela cheia, qualidade visual — que também cai sozinha se a máquina não segurar 45 fps), as 15 conquistas e estatísticas cumulativas. Tudo passa por `normalizar`
ao carregar: salvamento velho ou corrompido vira padrão no que faltar.

## Como é feito

| Arquivo | O que é | Roda no Node? |
|---|---|---|
| `js/motor.js` | **Toda a regra**: posição, golpes, caixas de acerto, dano, defesa, armadura, lançamento, agarrão, arremesso, finalização, ondas, câmera, IA, itens. Sem `Math.random`: o acaso sai de `mundo.rng`, semeado — mesma semente, mesma luta. | ✅ `module.exports` |
| `js/figura.js` | O lutador com volume: anatomia de 7,5 cabeças, membros afunilados sombreados (luz quente, sombra fria), tronco com peitoral, rosto, cabelo, capuz, faixa e sash que balançam, sombra projetada, rastro. As poses por estado moram aqui. | ❌ |
| `js/cenario.js` | Os quatro cenários em camadas: céu com profundidade, arquitetura em ladrilho, luzes que tremulam e pintam parede e chão, chão com textura de ruído, névoa atrás e na frente, vinheta, gradação e grão. | ❌ |
| `js/desenho.js` | Projéteis, itens, partículas, textos, HUD, telas de menu; monta a cena com os dois de cima. | ❌ |
| `js/som.js` | Efeitos e música sintetizados (Web Audio). Um sequenciador pentatônico com taiko, um humor por cenário. | ❌ |
| `js/entrada.js` | Teclado, Gamepad API e toque (joystick + botões). Calcula a borda "apertou" por quadro. | ❌ |
| `js/progresso.js` | O que fica guardado entre partidas, com versão e migração (`normalizar`). | ✅ |
| `js/conquistas.js` | As 21 conquistas: definição, desbloqueio pelos eventos do motor e da compra no Templo, espelho pra Steam. | ✅ |
| `js/loja.js` | O Templo: catálogo das sete melhorias, karma da fase, compra. O motor não importa a loja; ele exporta `Motor.MELHORIAS` e recebe `liberados` no `criarMundo`. | ✅ |
| `js/plataforma.js` | Onde salva e com quem fala: `localStorage` no navegador, `window.punhos` no Electron (arquivo + Steam). | ❌ |
| `js/principal.js` | Laço com passo fixo de 1/60 s, menus, opções, pausa, congelamento de acerto, câmera lenta, resolução nativa. | ❌ |
| `desktop/` | Electron endurecido pela checklist de segurança: `main.js` só liga os fios (janela, `app://`, sandbox, bloqueio de navegação e permissão, IPC); `caminho-seguro.js` tem a lógica pura (caminho sem travessia, CSP, porteiro do IPC, salvamento com `.bak` e fsync) e roda no CI; `preload.js` é a ponte; `steam.js` fala com `steamworks.js`, opcional, com fallback. Fuses do Electron no `package.json`. | ✅ `caminho-seguro.js` |
| `ferramentas/` | `simular.js` + `bot.js`: simulação de lutas no Node com um jogador artificial, pra balancear por dados (ver o README de lá). `conferir.js`: o `npm run conferir`. | ✅ |
| `steam/` | Os `.vdf` do SteamPipe e o passo a passo de publicação. | — |

O motor emite **eventos** por quadro (`mundo.eventos`: acerto, som, tremor, texto, morte,
finalização…) e quem desenha/toca consome. Nada de tela vaza pra dentro da regra.

## Conferência

```bash
cd jogo && npm run conferir        # todos de uma vez (varre conferir-*shaolin*.js)

node Padelizou.Tests/js/conferir-punhos-de-shaolin.js       # combate
node Padelizou.Tests/js/conferir-conquistas-do-shaolin.js   # progresso, conquistas, dificuldade
node Padelizou.Tests/js/conferir-loja-do-shaolin.js         # Templo: catálogo, compra, karma e o efeito de cada melhoria no motor
node Padelizou.Tests/js/conferir-desktop-do-shaolin.js      # caminho seguro, porteiro do IPC, salvamento com .bak, fios do main.js, fuses
node Padelizou.Tests/js/conferir-simulacao-do-shaolin.js    # o bot conclui a fase 1, nada trava, a semente reproduz (~3 s)
node Padelizou.Tests/js/conferir-conteudo-do-shaolin.js     # Lian, Lanceiro, Renegado, fúria dos chefes, inimigos presos na arena
node Padelizou.Tests/js/conferir-palco-do-shaolin.js        # cenário que mata, pontos de controle, Arena
```

Roda o motor no Node, quadro a quadro, e confere 40 pontos: soco tira o dano certo e só em quem
está na frente e na mesma linha; o terceiro soco lança; defesa segura o golpe; ondas travam e
liberam a câmera; finalização só em atordoado; arremesso atropela; IA se aproxima e bate; vida
nunca fica negativa; chi enche e o especial consome; determinismo por semente; P2 entra no meio;
fase conclui depois da última onda; morte renasce ou é fim de jogo; armadura do Bruto. O CI roda
junto com os outros `conferir-*.js` — o glob é o mesmo.

Três mutações foram vistas VERMELHAS antes de fechar (defesa sem redução, armadura desligada,
terceiro soco sem lançamento): o conferidor trava esses comportamentos, não só os descreve.

## Pra mexer

- **Visual**: cores em `def.cores` (motor) e o resto em `ESTILOS` (`figura.js`): cabelo, roupa, capuz, lâminas, largura do corpo. Cenário novo é uma entrada em `CENARIOS` (`cenario.js`) com `fundo`, `meio`, `luzes` e `chao`.
- **Golpe novo**: uma linha em `PERSONAGENS[x].golpes` (`inicio`, `ativo`, `total`, `alcance`,
  `altura`, `recuo`, `dano`; opcionais `lanca`, `derruba`, `dosDoisLados`, `avanco`, `proximo`,
  `proximoCinco` (a corrente com a Sequência de Cinco), `mergulho: [vx, vz]` (golpe que cai do ar)).
- **Melhoria nova do Templo**: o id em `Motor.MELHORIAS` com o efeito no motor, e a entrada no
  `CATALOGO` da `loja.js` com preço e texto. O conferidor da loja reprova id de catálogo que o
  motor não conhece.
  Depois uma pose em `POSES_DE_GOLPE` no desenho, senão ele usa a do soco.
- **Inimigo novo**: uma entrada em `INIMIGOS` (com o bloco `ia`) e um estilo em `ESTILOS`.
- **Fase nova**: uma entrada em `FASES` (ondas com `x` de gatilho) e um cenário em `CENARIOS`.
- Se mudar regra, mude o conferidor **antes** e veja falhar. Regra 1 do `CLAUDE.md` vale aqui.
