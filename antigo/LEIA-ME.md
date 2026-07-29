# Arte que não é servida na web

Nada aqui é publicado. Esta pasta está **fora do `wwwroot`** de propósito: o que ficasse lá seria
baixável por qualquer pessoa e entraria no pacote de todo deploy, mesmo sem ninguém usar.

Os arquivos que o site realmente usa ficam em `Padelizou/wwwroot/image/`.

| Arquivo | O que é |
|---|---|
| `logo-novo.jpeg` | **A arte original do logo que está no ar hoje.** Não é antigo — está aqui porque é o arquivo de origem (1254×1254, JPEG sobre fundo branco), não uma imagem pra servir. É dele que sai todo o conjunto de ícones. |
| `logo-icon-anterior.png` | O logo anterior (quadrado verde com raquetes escuras), aposentado em 29/07/2026. |
| `logo-novo-padel.jpeg` | Uma versão de 22/07/2026 que nunca chegou a ser usada. |

## Pra regerar os ícones a partir da arte original

O conjunto servido (`logo-icon.webp`, `favicon-32.png`, `apple-touch-icon.png`, `icon-512.png`)
é derivado de `logo-novo.jpeg`. Cada um tem uma exigência diferente — transparência, opacidade
obrigatória no iOS, área segura no Android — e o enquadramento pequeno é mais aproximado que o
grande de propósito, porque as raquetes ocupam só 44% da largura do círculo.

⚠️ **Ao trocar qualquer ícone, subir o `CACHE_NAME` em `Padelizou/wwwroot/sw.js`.** O Service
Worker guarda esses arquivos pelo caminho, e sem virar a versão quem já instalou o app continua
vendo o logo antigo — para sempre.
