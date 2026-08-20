# Como a camisa do simulador é gerada

A camisa do `index.html` não é desenho: é um **render 3D** feito pelo
`gera-camisa.py` (Blender como biblioteca Python — `pip install bpy pillow`).

O script modela a peça a partir do molde 2D (corpo, mangas e punhos como
almofadas separadas, gola como tubo no decote), com dobras de ruído e luz
de estúdio, e renderiza 4 imagens:

- `base-frente.png` / `base-costas.png` — a foto da peça em cinza
- `mask-frente.png` / `mask-costas.png` — as regiões (R=corpo, G=mangas, B=gola)

Rodar: `python3 gera-camisa.py 128 ./saida/` (amostras, pasta de saída).

Depois a base vira **relevo cinza-médio** (128 + (v − mediana) × 6) e as
quatro imagens entram no `index.html` como data-URIs WebP (~70KB no total).
No navegador, pintar é compor no canvas: cor chapada por zona + o relevo em
`hard-light` por cima — sombra escurece, brilho clareia, em qualquer cor.

Pra trocar o modelo da peça (gola V, manga longa, regata...), é editar o
molde no topo do script e renderizar de novo — o site não muda.
