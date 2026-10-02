# Carros da arte

Cada `<estilo>.glb` desta pasta (ex.: `gt.glb`, `pickup.glb`) substitui o carro procedural daquele estilo no
jogo — no navegador, no build e no Electron. Sem arquivo, o estilo continua procedural.

Antes de pôr um arquivo aqui: `npm run check-car -- caminho/estilo.glb`. Arquivo recusado não quebra o jogo:
fica de fora, com o motivo no console (`[carros] … recusado`). Convenção, modelos-base e passo a passo em
`docs/ARTE.md`.
