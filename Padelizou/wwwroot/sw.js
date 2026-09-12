// Trocar de logo NÃO basta pra quem já usa o app: os arquivos abaixo ficam guardados aqui pelo
// caminho, e o `activate` só joga fora cache com nome diferente deste. Sem virar a versão, quem
// já instalou continuaria vendo o logo antigo pra sempre.
// **Ao trocar qualquer arquivo desta lista, suba o número.**
// ⚠️ v28 PORQUE O v27 FOI USADO DUAS VEZES (11/09/2026). Dois branches subiram v26 → v27
// no mesmo dia — o do escudo/chave e o da bolinha do saque —, e o git juntou os dois como
// "mesma mudança", sem conflito. O número ficaria igual com DOIS site.css diferentes, e quem
// tivesse guardado o primeiro v27 nunca baixaria o segundo: a bolinha do saque simplesmente
// não apareceria pra quem usa o app instalado, sem erro nenhum em lugar nenhum.
//
// ⚠️ E v29 PELO MESMO MOTIVO, no mesmo dia: aquele branch subiu v27 → v28 antes de mesclar o
// `main`, que já tinha ido a v28 pela bolinha do saque — e o v28 JÁ ESTÁ EM PRODUÇÃO com
// outro `site.css`. Mantê-lo deixaria a tabela do grupo repartida do jeito velho pra quem já
// guardou aquele v28, sem erro nenhum. O conflito aqui é a única pista: quando o git NÃO
// conflita (as duas pontas escrevem o mesmo número), a colisão passa calada.
//
// ⚠️ E v30 PELA TERCEIRA VEZ, no MESMO DIA (verde do card AO VIVO). Este branch tinha subido
// pra v29 antes de mesclar o `main`, que já estava em v29 — e o `const` NÃO conflitou, porque
// as duas pontas escreveram o mesmo número. Quem avisou foi o comentário acima, que conflitou:
// é literalmente a pista que o v29 deixou escrita aqui pra quem viesse depois. **Quem sobe o
// número confere o `main` ANTES de escolher qual.**
//
// ⚠️ v34: o anel da bola apagada do saque (o alvo de "passar o saque pra cá") era invisível no
// card AO VIVO — e o arquivo que conserta isso é o `site.css`, que está na lista abaixo. Sem
// virar o número, quem usa o app instalado continuaria com o alvo apagado, sem erro em lugar
// nenhum. Conferido no `origin/main` ANTES de escolher o número, como manda o parágrafo acima.
// ⚠️ v35 POR UM ARQUIVO QUE NÃO ESTÁ NA LISTA, e é o caso que o parágrafo de cima não cobria:
// o `placar-ao-vivo.js` e o `jogos-ao-vivo-atualiza.js` não são `STATIC_ASSETS`, mas caem na
// regra de baixo (`isStaticAsset`), que serve a CÓPIA GUARDADA e só busca a nova em segundo
// plano — quem tem o app instalado rodaria o JavaScript velho por mais uma abertura. Como o
// que mudou é o salvamento do placar de quem está marcando AGORA, essa abertura é um game
// perdido. Virar o número apaga o cache inteiro no `activate` e a próxima carga vem da rede.
// Conferido no `origin/main` ANTES de escolher o número (estava em v34).
// ⚠️ v36 pelo mesmo motivo do v35, agora pelo `mesa-offline.js`: ele também não está na lista
// abaixo, mas cai na regra de `isStaticAsset`, que serve a CÓPIA GUARDADA e só busca a nova em
// segundo plano. A Mesa é a tela de quem está com o celular na mão no meio do jogo — rodar o
// JavaScript velho por mais uma abertura ali é um game perdido. Conferido no `origin/main`
// ANTES de escolher o número (estava em v35).
// ⚠️ v37 pelo mesmo motivo do v35 e do v36, agora pelo `jogos-ao-vivo-atualiza.js` de novo — e
// desta vez o arquivo mudado É a correção de quem usa o app instalado. 🗣️ Felipe: *"Pessoal que
// tem o app no celular, disse q ao abrir ele fica desatualizado as vezes no aovivo"*. Deixar o
// número parado seria entregar a correção justamente pra quem não a receberia na próxima
// abertura: o arquivo não está na lista abaixo, mas cai na regra de `isStaticAsset`, que serve a
// CÓPIA GUARDADA e só busca a nova em segundo plano. Conferido no `origin/main` ANTES de escolher
// o número (estava em v36).
const CACHE_NAME = "padelizou-static-v37";
const PAGINA_OFFLINE = "/offline.html";
const STATIC_ASSETS = [
  PAGINA_OFFLINE,
  "/css/site.css",
  "/js/site.js",
  "/lib/bootstrap/dist/css/bootstrap.min.css",
  "/lib/bootstrap/dist/js/bootstrap.bundle.min.js",
  // A FONTE DOS ÍCONES entra aqui desde 13/08/2026, quando saiu do CDN. Ela precisa vir
  // junto com o CSS: sem o .woff2 guardado, a Mesa de Controle offline abriria com o estilo
  // certo e um quadradinho no lugar de cada ícone — que é o que já acontecia com o CDN.
  "/lib/bootstrap-icons/font/bootstrap-icons.min.css",
  "/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2",
  "/lib/jquery/dist/jquery.min.js",
  "/image/logo-raquetes.webp",
  "/image/logo-icon.webp",
  "/image/favicon-32.png",
  "/image/icon-192.png",
  "/image/icon-512.png",
  // Os ORNAMENTOS das molduras (o site.css aponta pra eles): sem eles no cache, a coroa do
  // Campeão sumiria da foto justamente na tela que funciona offline.
  "/img/molduras/coroa.svg",
  "/img/molduras/gema-rubi.svg",
  "/img/molduras/gema-safira.svg",
  "/img/molduras/gema-ametista.svg",
  "/img/molduras/gema-esmeralda.svg",
  "/img/molduras/louros.svg",
  "/img/molduras/chamas.svg",
  "/img/molduras/estrela-mvp.svg",
  "/img/molduras/asas-coracao.svg",
  "/img/molduras/raquetes.svg",
  "/img/molduras/calendario.svg",
  "/img/molduras/bussola.svg",
  "/img/molduras/medalha.svg",
  "/img/molduras/foguete.svg",
  "/img/molduras/escudo-time.svg",
  "/img/molduras/apito.svg",
  "/img/molduras/coracao-aula.svg",
  "/img/molduras/gatinha.svg",
  "/img/molduras/sapinho.svg",
  "/img/molduras/raposa.svg",
  "/img/molduras/bruxinha.svg",
  "/img/molduras/gato-preto.svg",
  "/img/molduras/fada.svg",
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_ASSETS))
  );
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((names) =>
      Promise.all(
        names
          .filter((name) => name !== CACHE_NAME)
          .map((name) => caches.delete(name))
      )
    )
  );
  self.clients.claim();
});

// Só faz cache de assets estáticos (css/js/imagens). Páginas .cshtml renderizadas
// no servidor (torneios, aulas, agenda etc.) sempre vão direto pra rede, pra não
// mostrar dado desatualizado quando offline vira online de novo.
//
// UMA página é exceção: a Mesa de Controle. No ginásio sem sinal, o celular trava a
// tela, o navegador descarta a página e o organizador recarrega — sem cache ele veria
// erro de conexão e perderia a Mesa no meio do torneio. Rede primeiro (dado fresco
// quando há rede); a cópia só aparece quando a rede FALHA. O placar que ela mostra é
// corrigido na hora pelo mesa-offline.js, que guarda no aparelho o que foi marcado.
self.addEventListener("fetch", (event) => {
  const { request } = event;
  if (request.method !== "GET") return;

  const url = new URL(request.url);

  if (url.pathname.startsWith("/Torneios/MesaControle")) {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
          return response;
        })
        // Sem cópia guardada (primeiro acesso já sem sinal) sobrava uma resposta vazia,
        // que o navegador mostra como erro cru. Cai na tela offline como todo o resto.
        .catch(() => caches.match(request).then((c) => c || caches.match(PAGINA_OFFLINE)))
    );
    return;
  }

  // Navegação (a pessoa abriu o app ou tocou num link) sem rede: sem isto o Chrome
  // desenha o dinossauro DENTRO do app instalado, e parece que o Padelizou quebrou.
  // A rede vem sempre primeiro — a tela offline só entra quando a rede falha.
  if (request.mode === "navigate") {
    event.respondWith(fetch(request).catch(() => caches.match(PAGINA_OFFLINE)));
    return;
  }

  const isStaticAsset = /\.(css|js|png|jpg|jpeg|svg|ico|woff2?|webp)$/.test(url.pathname);
  if (!isStaticAsset) return;

  event.respondWith(
    caches.match(request).then((cached) => {
      const fetchPromise = fetch(request)
        .then((response) => {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
          return response;
        })
        .catch(() => cached);
      return cached || fetchPromise;
    })
  );
});

self.addEventListener("push", (event) => {
  let data = { title: "Padelizou", body: "Você tem uma novidade.", url: "/" };
  if (event.data) {
    try {
      data = { ...data, ...event.data.json() };
    } catch {
      data.body = event.data.text();
    }
  }

  // SONDA da varredura de fantasmas: não é aviso pra ninguém, é uma pergunta feita ao servidor
  // de push ("este registro ainda existe?"). Quem responde é ele, com 2xx ou 410 — o aparelho
  // não tem nada a ver com isso e não pode virar notificação.
  //
  // ⚠️ A inscrição nasce com `userVisibleOnly: true`, então o navegador PODE mostrar um aviso
  // genérico dele quando a gente não mostra nenhum. Não é uma garantia de silêncio; é o que dá
  // pra fazer do nosso lado. Quem limita o estrago é a varredura, que só sonda suspeito.
  if (data.tipo === "sonda") return;

  const opcoes = {
    body: data.body,
    icon: "/image/icon-512.png",
    badge: "/image/favicon-32.png",
    data: { url: data.url },
  };

  // PLACAR AO VIVO: o mesmo jogo manda um aviso por game, e sem `tag` cada um empilharia —
  // a tela de notificações viraria uma lista "4x3", "4x4", "5x4"... `tag` faz este aviso
  // SUBSTITUIR o anterior do mesmo jogo (mesma tag = mesma notificação, conteúdo novo).
  // `renotify: false` (o padrão, explícito aqui) é o que faz a substituição ser SILENCIOSA —
  // sem ele o navegador tocaria/vibraria a cada game, e ninguém quer o celular avisando
  // ponto a ponto. Ver Services/PushNotificationService.EnviarPushAsync.
  if (data.tag) {
    opcoes.tag = data.tag;
    opcoes.renotify = false;
    opcoes.silent = true;
  }

  // O CARD DO PLACAR (12/09/2026). 🗣️ Felipe, com o print da bolha do Google e o do placar na
  // Dynamic Island: "as notificações estao acontecendo, mas eu queria algo tipo esses prints".
  // Os dois exigem app nativo; `image` é o que a notificação da WEB tem — no Android, puxando-a
  // pra baixo, ela abre este PNG (Services/CartaoDoPlacarAoVivo). Onde não houver suporte, a
  // chave é ignorada e sobra o texto de sempre: nada quebra.
  if (data.image) opcoes.image = data.image;

  event.waitUntil(self.registration.showNotification(data.title, opcoes));
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = event.notification.data?.url || "/";

  event.waitUntil(
    self.clients.matchAll({ type: "window" }).then((clients) => {
      const existente = clients.find((c) => c.url.includes(url));
      if (existente) return existente.focus();
      return self.clients.openWindow(url);
    })
  );
});
