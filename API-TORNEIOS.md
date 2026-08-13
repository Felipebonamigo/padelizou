# API de torneios do Padelizou — para o Ranking Brasil

> Documento para o parceiro. Escrito em 12/08/2026, a pedido deles:
> *"uma API que libera buscarmos os torneios pontuados no ranking — apenas informações dos
> torneios, como nome, clube, data, foto. Não precisa nenhum dado dos atletas."*

É uma porta **só de leitura**: um endereço, um cabeçalho, um JSON. Não precisa login, não
tem sessão, não tem paginação — a lista é curta por natureza (são os torneios com inscrição
aberta neste momento).

## O endereço

```
GET https://padelizou.com.br/api/ranking/torneios
```

É o único endereço ligado. Existe um ambiente de homologação (`dev.padelizou.com.br`, banco e
dados próprios), mas ele **não está com chave** — se em algum momento fizer falta testar sem
tocar na produção, a gente liga e manda uma chave separada. É pedir.

## A chave

Vai no cabeçalho **`x-api-key`** — o mesmo nome que a API de vocês usa com a gente, de propósito:
o código que vocês já têm para nos chamar serve para chamar aqui, trocando só a chave.

```bash
curl -H "x-api-key: SUA_CHAVE_AQUI" https://padelizou.com.br/api/ranking/torneios
```

A chave é entregue pelo Padelizou, fora deste documento. Ela identifica vocês e nada mais —
não há usuário, não há troca de token, não expira sozinha. Se precisar trocar, a gente troca e
avisa; a antiga para de valer na hora.

Ela abre a lista sozinha, sem login: guardem como segredo de sistema, não em repositório nem em
grupo de mensagem.

## A resposta

```json
{
  "geradoEm": "2026-08-12T12:47:12-03:00",
  "quantidade": 1,
  "torneios": [
    {
      "id": 34,
      "nome": "NATA PADEL TOUR",
      "link": "https://padelizou.com.br/Torneios/Details/34",
      "foto": "https://padelizou.com.br/uploads/capas/nata.webp",
      "clube": {
        "nome": "NATA Padel",
        "cidade": "Gravataí",
        "estado": "RS",
        "endereco": "Av. Dorival Cândido de Oliveira, 100"
      },
      "local": "Quadras cobertas",
      "dataInicio": "2026-08-22",
      "dataFim": "2026-08-24",
      "formato": "Padrao",
      "precoInscricao": 90,
      "inscricoesAte": "2026-08-18",
      "inscricaoRestrita": false,
      "categorias": [
        { "nome": "4ª Categoria Masculina", "rankingId": 108, "rankingNome": "4ª Masculina" },
        { "nome": "7ª Categoria Masculina", "rankingId": null, "rankingNome": null }
      ]
    }
  ]
}
```

### Campo a campo

| Campo | O que é |
|---|---|
| `geradoEm` | Quando esta resposta foi montada, com fuso. Serve para vocês saberem que não estão lendo um cache velho de algum intermediário. |
| `quantidade` | Quantos torneios vieram. É `torneios.length`, repetido por conveniência. |
| `id` | O identificador do torneio no Padelizou. **Estável** — é por ele que vocês reconhecem o mesmo torneio na chamada seguinte. |
| `nome` | O nome como o organizador o publicou. |
| `link` | A página pública do torneio. Pode ser usada como link direto na listagem de vocês. |
| `foto` | A capa do torneio, endereço completo. **`null` quando o organizador não subiu capa** — a maioria dos torneios novos começa assim. |
| `clube` | Onde o torneio acontece. **`null`** quando o torneio não tem clube cadastrado (acontece); `cidade`, `estado` e `endereco` também podem vir nulos separadamente. |
| `local` | Complemento escrito pelo organizador ("quadras cobertas", "ao lado do estacionamento"). Pode ser nulo. |
| `dataInicio` / `dataFim` | `AAAA-MM-DD`, **sem hora e sem fuso** — é data de calendário, não instante. `dataFim` pode ser nulo (torneio de um dia ou organizador que não preencheu). |
| `formato` | `"Padrao"` (duplas fixas, grupos + mata-mata), `"Americano"` (inscrição individual, parceiro troca a cada rodada) ou `"AmericanoDuplas"`. |
| `precoInscricao` | Valor **por pessoa**, em reais — é como o torneio anuncia. Uma dupla paga o dobro. |
| `inscricoesAte` | Data prevista de encerramento das inscrições, quando o organizador publicou uma. É **promessa dele**, não corte automático: o que fecha a inscrição é o botão, e enquanto o torneio estiver nesta lista ele está aberto. |
| `inscricaoRestrita` | `true` = torneio fechado, só entra quem tem a chave de acesso do organizador. Ele aparece aqui porque também aparece na nossa listagem, mas **não anunciem como aberto a todos**. |
| `categorias[].nome` | O nome da categoria como o organizador escreveu. Texto livre. |
| `categorias[].rankingId` | 🔑 **O de-para com o catálogo de vocês** (100 a 116). É por ele que dá para casar a categoria sem depender do nome digitado. |
| `categorias[].rankingNome` | O nome dessa categoria no catálogo de vocês ("4ª Masculina"), só para conferência humana. |

⚠️ **`rankingId` nulo é informação, não falha.** Significa que aquela categoria não é conferida
contra o ranking — ou porque ela não existe no catálogo de vocês (7ª, Iniciantes, Mista D), ou
porque o organizador não fez o de-para. Atleta que se inscreve nela não passa pela validação.

## Quais torneios aparecem

Três condições, **todas** obrigatórias:

1. **Aderiu ao ranking** — o organizador marcou "conferir as inscrições no Ranking Brasil" na
   criação do torneio. É exatamente a mesma marca que faz aquele torneio entrar no acerto de
   R$ 1 por inscrito: a lista e a conta enxergam o mesmo conjunto.
2. **Inscrições abertas agora** — foi o pedido ("os torneios atuais abertos"). Torneio que
   ainda não abriu, que já sorteou as chaves, que está em andamento ou que acabou **não vem**.
   Ele simplesmente some da lista no dia em que o organizador encerra as inscrições.
3. **Já é público no Padelizou** — aprovado por nós, não escondido pelo organizador e não
   cancelado. Todo torneio passa por uma aprovação antes de aparecer em qualquer lugar; esta
   API respeita a mesma régua da nossa própria listagem.

## O que a API não devolve, e não vai devolver

**Nenhum dado de atleta.** Nem nome, nem CPF, nem contato — e nem a **contagem** de inscritos.
Foi o combinado, e do nosso lado ele está preso por teste automatizado: a resposta inteira é
conferida contra os dados de quem está inscrito no torneio, e o teste fica vermelho se um campo
novo arrastar isso junto. Também não vai a chave de acesso de torneio restrito, nem nada
financeiro além do preço que já é público na página.

Se um dia vocês precisarem de algo que não está aqui, é conversa — não é uma limitação técnica,
é um escopo combinado.

## Erros

| Código | O que aconteceu | O que fazer |
|---|---|---|
| `401` | `{"erro":"Chave de API inválida ou ausente."}` — o cabeçalho `x-api-key` não veio, ou veio errado. | Conferir a chave e o nome do cabeçalho. |
| `503` | `{"erro":"A integração de torneios ainda não foi ligada deste lado..."}` — o problema é **nosso**: a chave não está configurada no nosso servidor. | Nos avisar. Não adianta mexer aí. |
| `429` | Passou do limite de chamadas. Vem com `Retry-After` em segundos. | Esperar e reduzir a frequência. |

O `503` existe separado do `401` de propósito: sem ele, uma configuração que faltasse do nosso
lado faria vocês passarem a tarde conferindo uma chave que estava certa.

## Frequência

O limite é de **60 chamadas a cada 5 minutos** por IP. Não é desconfiança — é a mesma trava das
consultas do site, e ela existe para o caso do laço infinito que toda integração ganha um dia
por engano.

Na prática, **de 5 em 5 minutos é de sobra**: torneio não abre nem fecha inscrição de segundo
em segundo. A resposta vai com `Cache-Control: no-store` porque carrega chave; o cache, se
quiserem, é do lado de vocês.

## Estabilidade

- **Campos podem ser acrescentados** sem aviso — o cliente de vocês deve ignorar o que não
  conhece.
- **Campos existentes não somem nem mudam de significado** sem a gente combinar antes.
- O endereço `/api/ranking/torneios` é para durar. Se um dia houver uma versão nova incompatível,
  ela nasce em outro endereço e as duas convivem.

## O que dá para acrescentar, se fizer sentido

Nada disso está feito — é só para vocês saberem que é barato, se precisarem:

- **Os torneios que ainda vão abrir e os que estão acontecendo**, como um parâmetro
  (`?situacao=todos`). Hoje a lista é só de inscrição aberta, que foi o pedido.
- **Filtro por cidade ou estado**, se a listagem de vocês for regional.
- **Aviso ativo** (webhook) quando um torneio novo aderir ao ranking, em vez de vocês
  perguntarem de tempos em tempos.

Qualquer uma dessas, é só pedir.
