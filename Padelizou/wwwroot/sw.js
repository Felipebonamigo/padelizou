// Trocar de logo NÃO basta pra quem já usa o app: os arquivos abaixo ficam guardados aqui pelo
// caminho, e o `activate` só joga fora cache com nome diferente deste. Sem virar a versão, quem
// já instalou continuaria vendo o logo antigo pra sempre.
// **Ao trocar qualquer arquivo desta lista, suba o número.**
const CACHE_NAME = "padelizou-static-v18";
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

  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      icon: "/image/icon-512.png",
      badge: "/image/favicon-32.png",
      data: { url: data.url },
    })
  );
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
