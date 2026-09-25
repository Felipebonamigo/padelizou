# Punhos de Shaolin

Um beat-em-up de monges no estilo *Mortal Kombat: Shaolin Monks*, em HTML5 Canvas, sem um
único sprite, imagem ou arquivo de áudio: os lutadores são bonecos articulados desenhados na
hora, os cenários são gradientes e formas cacheadas, e o som é sintetizado com Web Audio.

**Pra jogar:** abra `jogo/index.html` no navegador — ou, no desktop, `npm ci && npm run start` em `jogo/` (Electron). Funciona por `file://`, sem servidor e sem
internet (as fontes do Google melhoram o visual, mas o jogo não espera por elas).

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
  o Long solta o *Sopro do Dragão* (bola de fogo), o Shen gira a *Tempestade do Bastão*.
- **Finalização:** inimigo com pouca vida que levanta do chão fica **atordoado** (estrelinhas,
  "FINALIZE!"). Agarre-o nesse instante: vale 500 pontos + o dobro dos pontos dele, e enche o chi.
- **Vasos** quebram e soltam chá (vida) ou pergaminho (chi). Inimigo morto às vezes solta também.
- Só **dois inimigos atacam ao mesmo tempo** — é a régua de justiça do gênero. O resto cerca.

Quatro fases (Pátio do Templo, Floresta Viva, Poço das Almas, Torre do Feiticeiro), cada uma
com ondas de inimigos e um chefe: Mestre Sombra (teleporta ao levar três golpes), Grão-Presa
(armadura: golpe leve não interrompe), o Gigante do Poço (pancada no chão dos dois lados) e o
Feiticeiro (teleporta, atira caveiras e invoca Sombras a cada terço de vida).

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
| `js/conquistas.js` | As 15 conquistas: definição, desbloqueio pelos eventos do motor, espelho pra Steam. | ✅ |
| `js/plataforma.js` | Onde salva e com quem fala: `localStorage` no navegador, `window.punhos` no Electron (arquivo + Steam). | ❌ |
| `js/principal.js` | Laço com passo fixo de 1/60 s, menus, opções, pausa, congelamento de acerto, câmera lenta, resolução nativa. | ❌ |
| `desktop/` | Electron: `main.js` (janela, arquivo de progresso, IPC), `preload.js` (a ponte), `steam.js` (`steamworks.js`, opcional, com fallback). | — |
| `steam/` | Os `.vdf` do SteamPipe e o passo a passo de publicação. | — |

O motor emite **eventos** por quadro (`mundo.eventos`: acerto, som, tremor, texto, morte,
finalização…) e quem desenha/toca consome. Nada de tela vaza pra dentro da regra.

## Conferência

```bash
node Padelizou.Tests/js/conferir-punhos-de-shaolin.js       # combate (40 checks)
node Padelizou.Tests/js/conferir-conquistas-do-shaolin.js   # progresso, conquistas, dificuldade (35 checks)
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
  `altura`, `recuo`, `dano`; opcionais `lanca`, `derruba`, `dosDoisLados`, `avanco`, `proximo`).
  Depois uma pose em `POSES_DE_GOLPE` no desenho, senão ele usa a do soco.
- **Inimigo novo**: uma entrada em `INIMIGOS` (com o bloco `ia`) e um estilo em `ESTILOS`.
- **Fase nova**: uma entrada em `FASES` (ondas com `x` de gatilho) e um cenário em `CENARIOS`.
- Se mudar regra, mude o conferidor **antes** e veja falhar. Regra 1 do `CLAUDE.md` vale aqui.
