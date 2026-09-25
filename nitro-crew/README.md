# Nitro Crew *(nome provisório)*

Corrida arcade em **pseudo-3D** no espírito dos clássicos de 16 bits (Top Gear), com **cooperativo local para
até 4 jogadores em tela dividida**: a equipe divide um cofre de nitro, empurra o companheiro que parou, ganha
vácuo atrás dele e pontua junta nas copas contra 19 pilotos de IA. Feito em TypeScript + Canvas 2D, com
simulação determinística pronta para multiplayer em lockstep, e empacotável com Electron para a Steam.

## Jogar agora

```bash
npm install
npm run dev        # abre em http://localhost:5174
```

Versão otimizada: `npm run build` e `npm run preview` (porta 4174).

## Como jogar (resumo)
- **Título → Campeonato / Corrida rápida / Contra-relógio.** No lobby, cada pessoa entra apertando um botão:
  teclado 1 (setas, Enter), teclado 2 (WASD, F) ou um controle (A/Start). Escolha o carro e marque "pronto".
- **Teclado 1**: setas viram/aceleram/freiam, **Espaço** nitro, **M/N** marcha (câmbio manual), **Esc** pausa.
  **Teclado 2**: WASD, **F** nitro, **E/Q** marcha. **Controle**: analógico/d-pad, A ou RT acelera, X/B/LT
  freia, RB nitro, Y/LB marcha, Start pausa.
- **Nitro**: 3 por corrida (no co-op, cofre da equipe). **Combustível**: acaba em ~2,4 voltas a fundo; entre
  no box (faixa à direita da reta de largada) devagar para abastecer. **Curva forte pede freio**; grama pune.
- **Campeonato**: solo/versus, termine entre os 5 para seguir; co-op, a equipe (2 melhores) precisa ficar entre
  as 3 melhores equipes. Copas destravam em sequência: Brasil → EUA → Japão → Europa.

## Conteúdo
- 12 pistas em 4 países, 6 cenários (litoral, tropical, cidade à noite, deserto, alpino, campos) × dia/entardecer/noite.
- 4 carros com trocas claras (velocidade, aceleração, curva, consumo).
- 19 pilotos de IA em 10 equipes, 3 dificuldades, elástico, nitro e box.
- Tela dividida 1–4, minimapa, HUD por jogador, PT-BR/EN, jukebox procedural com 4 músicas.

## Desenvolvimento

```bash
npm run typecheck              # TypeScript estrito
npm test                       # vitest: determinismo, pista, física, IA, co-op, campeonato, i18n, layout, áudio, opções
npm run smoke -- copacabana 2  # corrida completa sem interface (pista, nº de humanos em piloto automático)
npm run balance -- 150         # IA × IA em todas as pistas: voltas, grama, batidas, tempo de CPU
npm run preview & npm run playtest   # playtest no Chromium com capturas em scratch/ (exige preview no ar)
```

Estrutura: `src/core` (simulação, sem DOM) · `src/render` (Canvas 2D) · `src/ui` (menus/entrada) ·
`src/audio` · `src/game/session.ts` · `desktop/` (Electron/Steam) · `docs/` (design, roteiro, Steam).

## Roteiro
Concluído: fatia vertical (núcleo, IA, co-op, 12 pistas, tela dividida, menus, gamepads, áudio, Electron, testes).
Próximos: playtests no sofá e ajuste de sensação, arte e trilha finais, 32 pistas, carreira, Remote Play Together,
lockstep online, página da Steam. Cronograma completo em `docs/ROADMAP.md`.

## Licença
MIT.
