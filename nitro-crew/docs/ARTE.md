# Arte em glTF (passo 2.4)

Briefing de arte (direção, paleta, carros, cenário, especificação, lotes): https://claude.ai/code/artifact/049e138a-cb57-4adb-baef-d3084b4f0d03

## Carros — pronto
1. **Modelo-base**: `art/templates/cars/<estilo>.glb` (13), o carro atual do jogo na convenção. `npm run car-templates` regera.
2. **Redesenhar no Blender** mantendo a convenção: metros, +Y para cima, frente em +Z; nós `body`, `wheel_fl`,
   `wheel_fr`, `wheel_rl`, `wheel_rr`, vazios `exhaust_l`/`exhaust_r`; materiais pelo nome:
   - `paint*`, `accent*`, `stripe_a*`, `stripe_b*`, `stripe_ab*`: recebem a cor do carro; a cor do material é o tom
     (branco = cor pura, cinza = um tom abaixo);
   - `headlight*`, `taillight*`: acendem; força = emissivo ÷ cor (sem emissivo = 1);
   - "popup" no nome: sobe com o farol escamoteável;
   - qualquer outro nome: peça fixa com a cor, rugosidade e metal do material. **Sem textura** nos carros.
3. **Conferir**: `npm run check-car -- arquivo.glb` (mesmo validador do jogo: pegada, rodas, luzes, escape, ≤ 2.000
   triângulos no casco, faixas que os carros do estilo usam).
4. **Pôr no jogo**: copiar para `src/assets/cars/<estilo>.glb`. Entra no navegador, no build e no Electron
   (embutido como data URL: `src/render/cars/assets.ts`, atalho com teto anotado). Recusado = fica procedural, motivo no console.

Por ora a malha das rodas do .glb só dá posição e tamanho; o desenho continua o do estilo.

Código: `src/render/cars/gltf.ts` (leitura), `check.ts` (validador), `template.ts` (modelo-base), `assets.ts` (carga).
Testes: `tests/car-gltf.test.ts` — os 13 modelos-base voltam idênticos com e sem os extras (como saem do Blender).
Provado em 02/10: `gt.glb` no navegador (0 pixels diferentes do procedural), no build (`vite preview`) e no Electron (`file://`).

## Cenário e horizonte — falta
Mesmo caminho (convenção na seção Especificação do briefing); o carregador ainda não existe.
