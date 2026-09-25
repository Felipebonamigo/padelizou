# Jogar online

O online do Padelizou Arena segue a decisão D2 (`DECISOES.md`): um jogador hospeda e roda a partida de verdade
(host autoritativo, 120 passos por segundo); os outros mandam só o que apertam e desenham o que o host manda
(30 instantâneos por segundo), prevendo o próprio jogador e interpolando o resto. O protocolo mora no
`Padel.Core.Rede` e não sabe que o Godot existe; o Godot só entrega bytes pelo ENet. O delta da física vira passos de
1/120 s pelo `PassoFixo` (Padel.Core): contado em passos, sem deriva — somar segundos contra o `Protocolo.Passo` (float)
deixava um quadro sem passo a cada ~19 s —, e o aperto de um quadro que não deu passo vale no passo seguinte.

## Na rede local

```bash
# Quem hospeda (porta UDP 7777, espera 20 s pelos outros; vagas vazias ficam com a IA):
godot --path Padel.Godot -- --host 7777 --esperar 20 --nome Felipe

# Quem entra (no mesmo Wi-Fi, com o IP do host):
godot --path Padel.Godot -- --conectar 192.168.0.10:7777 --nome Joao
```

Os lugares são dados na ordem 2, 1, 3: o primeiro que entra joga **contra** o host (1x1 com duplas completadas
pela IA), o segundo vira parceiro do host, o terceiro completa o 2x2. Quem cai por 3 s vira IA e a partida segue.
Quem chega com a sala cheia ou a partida em andamento ouve o motivo e sai (o ENet do host tem 4 pares de folga pra
isso; com mais gente ainda chegando ao mesmo tempo, o excedente sai em 10 s com "o host não respondeu").

## Pela internet

Enquanto a Steam não entra, é o host abrir a porta UDP no roteador (redirecionamento pra máquina dele) e liberar
o executável no firewall. O cliente conecta no IP público do host. É o suficiente pra testar com amigos, e é
exatamente o problema que o Steam Datagram Relay resolve (D5): sem porta aberta, sem IP exposto.

## Testar sem ninguém

```bash
# Dois processos na mesma máquina, sem tela, cada um com o humano simulado nos controles:
godot --headless --path Padel.Godot -- --host 7777 --esperar 5 --sair-apos 50 --bot &
godot --headless --path Padel.Godot -- --conectar 127.0.0.1:7777 --sair-apos 45 --bot

# O mesmo com rede ruim de propósito (+100 ms na saída de cada lado e 5% de perda dos pacotes não confiáveis):
... --rede-ruim 100 0.05
```

No fim cada lado imprime a linha `Saindo após …` com o placar, os golpes por jogador (no host), o ping (no
cliente) e os problemas do ENet: `recusados` (envio que o ENet recusou a um par conectado — deve ser 0), `errosDoEnet`
(falha do socket) e `descartadosNoTeste` (a perda do `--rede-ruim`). Os dois primeiros também viram aviso no log, um na
hora e depois no máximo um a cada 5 s. A ponte com o ENet de verdade (conexão, queda, Dispose, sala cheia, passo fixo
das sessões, e o fim do cliente — que sai do placar DESENHADO, 100 ms atrás do instantâneo mais novo, senão a tela de
fim mostrava o placar de antes do último ponto) é conferida por
`godot --headless --path Padel.Godot res://cenas/TesteRede.tscn -- --conferir`.

Medido em 25/09/2026 nesta máquina: sem rede ruim, os dois terminam com o mesmo placar e ping de ~21 ms (a
granularidade do protocolo, não do cabo); com +100 ms e 5% de perda, o jogador do cliente deu 7 golpes aplicados no
host, os placares batem e o ping medido é ~238 ms.

## O que muda com a Steam (marco M2)

- O transporte troca de `TransporteEnet` pra um transporte sobre as Steam Networking Sockets (Facepunch.Steamworks,
  D5), implementando a mesma `ITransporte` — protocolo, host, cliente e desenho não mudam.
- A sala vira um **lobby da Steam**: convite de amigo, lista de salas públicas, e o endereço deixa de existir (é o
  SteamId do host).
- Reconexão (voltar pra vaga depois de cair) e servidor dedicado ficam pro 1.0.
