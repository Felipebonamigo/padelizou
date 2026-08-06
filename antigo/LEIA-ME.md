# Arte que não é servida na web

Nada aqui é publicado. Esta pasta está **fora do `wwwroot`** de propósito: o que ficasse lá seria
baixável por qualquer pessoa e entraria no pacote de todo deploy, mesmo sem ninguém usar.

Os arquivos que o site realmente usa ficam em `Padelizou/wwwroot/image/`.

| Arquivo | O que é |
|---|---|
| `logo-novo2.jpeg` | **A arte original do logo que está no ar hoje** (1024×1024, JPEG). Raquetes verdes sobre fundo escuro, com a palavra "Padelizou" embaixo — **a palavra não entra em ícone nenhum**, é recortada fora. É deste arquivo que sai todo o conjunto. |
| `logo-novo.jpeg` | A arte anterior (1254×1254, JPEG sobre fundo branco), aposentada em 06/08/2026. Mesmo desenho, com os furos das raquetes menos alinhados. |
| `logo-icon-anterior.png` | O logo de antes desse (quadrado verde com raquetes escuras), aposentado em 29/07/2026. |
| `logo-novo-padel.jpeg` | Uma versão de 22/07/2026 que nunca chegou a ser usada. |
| `gerar-icones/` | A ferramenta que gera o conjunto inteiro a partir da arte original. |

## Pra regerar os ícones a partir da arte original

```bash
dotnet run --project antigo/gerar-icones -- antigo/logo-novo2.jpeg Padelizou/wwwroot
```

O projeto está **fora do `Padelizou.slnx`** de propósito: é ferramenta de mesa, não entra no build
nem no deploy. Ele reescreve os 7 arquivos servidos de uma vez.

Com um **terceiro argumento** ele grava também o logo **em alta, PNG sem perda**, pra uso fora do
site (camiseta, banner, Instagram, apresentação) — `padelizou-logo-redondo.png` e
`padelizou-logo-quadrado.png` em 1024, e `padelizou-raquetes.png` com fundo transparente no
tamanho **nativo** do recorte (655×534: ampliar daqui pra cima não inventa nitidez, só aumenta o
arquivo). Essa pasta não é servida.

```bash
dotnet run --project antigo/gerar-icones -- antigo/logo-novo2.jpeg Padelizou/wwwroot ~/Desktop/logo-alta
```

⚠️ No PNG transparente em alta a borda é **erodida** (alfa remapeado 0,45..0,80), o que **não**
acontece nos arquivos do site. A arte tem um brilho claro no contorno das raquetes: sobre o azul do
site ele vira um contorno discreto, mas sobre fundo **claro** vira halo de recorte mal feito — e
esse é o único arquivo que pode cair em qualquer fundo.

Como funciona: as raquetes são recortadas por **máscara de cor** (`G − média(R,B)`, rampa 15..55) e
recompostas sobre um disco/placa escura sintetizada a partir do próprio fundo da arte. Recortar por
cor é o que faz a palavra "Padelizou" sumir sem deixar rastro, e é por isso que o verde da máscara é
`G − média(R,B)` e não `G − max(R,B)`: o verde da arte é amarelado (`#b5d33a`) e no segundo critério
dá só ~30, o que deixaria o **miolo** da raquete meio transparente.

A cor observada vai pro recorte **sem des-misturar do fundo**. Dividir pelo alfa na borda clareia o
contorno e desenha um halo — e todo lugar que usa a versão transparente tem fundo escuro, onde
franja escura some e halo claro apareceria.

Cada arquivo tem uma exigência diferente:

| Arquivo | Tamanho | Como é |
|---|---|---|
| `logo-raquetes.webp` | 400×326 | Só as raquetes, fundo transparente. Barra, rodapé e capa de torneio sem imagem — onde o fundo já é o azul do site. WebP porque carrega em **toda** página. |
| `logo-icon.webp` | 256×256 | Logo completo em disco. Vai onde o fundo é claro: login, portão, relatório impresso. |
| `favicon-32.png` | 64×64 | Enquadramento **mais aproximado** (raquetes em 86%): a 32px o enquadramento normal vira uma mancha verde. O nome diz 32 mas o arquivo tem 64 (é a versão 2x). |
| `apple-touch-icon.png` | 180×180 | **Opaco**, placa cheia — o iOS pinta preto atrás de transparência e arredonda os cantos sozinho. |
| `icon-192.png` | 192×192 | PWA. |
| `icon-512.png` | 512×512 | Serve de `any` **e** de `maskable`: o Android recorta a maskable num círculo, então as raquetes ficam nos **60% centrais**. |
| `../Padelizou/wwwroot/favicon.ico` | 16/32/48 | O navegador pede esse caminho sozinho, mesmo com o `<link>` apontando pro PNG. |

⚠️ **Ao trocar qualquer ícone, subir o `CACHE_NAME` em `Padelizou/wwwroot/sw.js`.** O Service
Worker guarda esses arquivos pelo caminho, e sem virar a versão quem já instalou o app continua
vendo o logo antigo — para sempre.
