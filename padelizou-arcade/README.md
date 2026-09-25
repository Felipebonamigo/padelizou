# Padelizou Arcade

Jogo de padel 2x2 no browser. JavaScript puro, sem build, sem dependência: abre o
`index.html` e joga. Você controla um jogador, um parceiro de IA cobre a outra metade, e dois
rivais de IA (fácil, médio ou difícil) jogam do outro lado.

**As regras são as do padel de verdade**, não as do tênis:

- A bola precisa quicar no chão do outro lado **antes** de tocar o vidro. Vidro direto é ponto contra.
- Depois de quicar, ela pode bater nas paredes à vontade e continua em jogo; o **segundo quique** dá o ponto.
- Bater na **própria** parede antes de cruzar a rede é permitido.
- Saque por baixo, na **diagonal**, dentro da caixa. Duas faltas é ponto; toque na rede que cai na caixa é let.
- Quem recebe o saque **não pode voleiar**. O mesmo time não bate duas vezes seguidas.
- Placar 0/15/30/40, **ponto de ouro** (ou vantagem, se preferir), tie-break em 6-6, 1 set ou melhor de 3.
- O saque alterna entre os times a cada game e entre os jogadores do time; no tie-break, 1 ponto e depois 2 a 2.

## Rodar

```bash
# opção 1: abrir index.html direto no browser (funciona pelo disco)
# opção 2: servir na rede local pra testar no celular
npm start          # http://localhost:8080
npm test           # 34 testes em node --test, sem instalar nada
```

## Controles

| Ação | Teclado | Celular |
|---|---|---|
| Mover | Setas ou WASD | Arrastar o dedo na quadra (joystick aparece onde encostar) |
| Sacar | Espaço | Botão SACAR |
| Lob | Segurar Espaço na hora do golpe | Segurar o botão LOB |
| Escolher o canto | ← / → no momento do golpe | Joystick pra esquerda/direita |
| Atacar curto / jogar fundo | ↑ / ↓ no momento do golpe | Joystick pra cima/baixo |
| Pausar | P ou Esc | Botão ❚❚ |

O golpe é **automático** quando a bola entra no alcance: o jogo é posicionamento e escolha de
golpe, não timing de botão. Bola alta perto da rede vira smash sozinha. Golpe esticado (bola
longe do corpo, rente ao chão ou muito rápida) sai com mais erro — pra você e pra IA.

Você joga sempre embaixo, na metade direita. Não há troca de lado (simplificação de arcade).

## Como está organizado

Tudo em `js/`, scripts clássicos carregados em ordem no `index.html` (sem módulo ES, pra abrir
pelo disco). Cada arquivo expõe seu pedaço em `window.Padel` e, no Node, em `module.exports`.
O motor não sabe que existe tela, e é por isso que roda inteiro no `node --test`:

| Arquivo | O que é |
|---|---|
| `quadra.js` | Medidas da quadra em metros (20 x 10, rede a 0,90 m, vidro 3 m + grade 1 m) e as caixas de saque. |
| `regras.js` | `Placar`: pontos, games, sets, tie-break, ordem e lado do saque. Puro. |
| `fisica.js` | `Bola` (gravidade, arrasto, quique, paredes, rede) e `calcularGolpe`, que acha a velocidade pra cair num alvo passando a rede. |
| `arbitro.js` | Transforma os eventos da física (quique, parede, rede, saiu) em decisão: ponto, falta, let. |
| `jogadores.js` | `Jogador` e a `IA`: simula a bola pra frente, decide quem busca e onde, e escolhe o golpe (buraco, lob, smash, erro). |
| `partida.js` | A máquina de estados: saque → rally → fim do ponto → … → fim. Recebe a entrada do humano por parâmetro. |
| `render.js` | Canvas 2D, visão de cima com altura falsa (sombra no chão, bola sobe e cresce). |
| `entrada.js` | Teclado e toque (joystick virtual + botão). |
| `som.js` | Sons sintetizados com WebAudio; nenhum arquivo de áudio. |
| `main.js` | Menu, laço de passo fixo (120 Hz), HUD, pausa, fim. |

`?auto=1` na URL põe os quatro jogadores na IA (demonstração). `?semente=N` fixa o sorteio.

## Testes

`tests/` usa só o `node:test` da plataforma:

- `regras.test.js` — placar: ponto de ouro, vantagem, set, tie-break, melhor de 3, rotação de saque.
- `fisica.test.js` — quique, paredes, rede, saída por cima, e `calcularGolpe` caindo perto do alvo.
- `arbitro.test.js` — cada regra do padel vira um caso.
- `simulacao.test.js` — uma partida inteira entre IAs, com semente fixa: termina, tem rally, tem ponto de todo tipo, bola nunca sai da quadra, e a mesma semente reproduz o mesmo jogo.

## O que ficou de fora (por enquanto)

- Troca de lado a cada game ímpar (o humano fica sempre embaixo).
- Bola devolvida de fora da quadra depois de sair por cima (regra real, rara).
- Diferença entre vidro e grade (a bola trata a parede inteira como vidro até 4 m).
- Dois humanos no mesmo teclado / online.
- Placar, ranking ou qualquer integração com o Padelizou.
