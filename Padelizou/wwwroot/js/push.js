function urlBase64ToUint8Array(base64String) {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = window.atob(base64);
  return Uint8Array.from([...rawData].map((c) => c.charCodeAt(0)));
}

// Por que o push não está disponível. A distinção existe por causa do iPhone: lá o
// PushManager só passa a existir DEPOIS que a pessoa adiciona o Padelizou à tela de
// início. Enquanto tratávamos tudo como "não suporta", o usuário de iPhone lia que o
// aparelho dele não servia — quando na verdade faltava um passo — e desistia pra sempre.
// Depende do instalar-app.js, carregado antes deste no _Layout.
function motivoSemPush() {
  if (!("serviceWorker" in navigator)) return "sem-suporte";
  if ("PushManager" in window) return null;
  return pdzPlataforma() === "ios" && !pdzAppInstalado() ? "precisa-instalar" : "sem-suporte";
}

const AVISO_PRECISA_INSTALAR =
  "No iPhone, os avisos só funcionam com o Padelizou instalado na tela de início.\n\n" +
  "Toque em Compartilhar (o quadrado com a seta pra cima), depois em \"Adicionar à Tela de Início\". " +
  "Abra o Padelizou pelo ícone novo e ative os avisos por aqui.";

async function ativarNotificacoesPush() {
  const motivo = motivoSemPush();
  if (motivo === "precisa-instalar") {
    alert(AVISO_PRECISA_INSTALAR);
    return false;
  }
  if (motivo) {
    alert("Seu navegador não suporta notificações push.");
    return false;
  }

  const permissao = await Notification.requestPermission();
  if (permissao !== "granted") return false;

  const registration = await navigator.serviceWorker.ready;
  const { publicKey } = await fetch("/Push/PublicKey").then((r) => r.json());

  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(publicKey),
  });

  const json = subscription.toJSON();
  await fetch("/Push/Subscribe", {
    method: "POST",
    headers: cabecalhoAntifalsificacao({ "Content-Type": "application/json" }),
    body: JSON.stringify({
      endpoint: json.endpoint,
      p256dh: json.keys.p256dh,
      auth: json.keys.auth,
    }),
  });

  return true;
}

async function desativarNotificacoesPush() {
  if (!("serviceWorker" in navigator)) return;
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  if (!subscription) return;

  const json = subscription.toJSON();
  await fetch("/Push/Unsubscribe", {
    method: "POST",
    headers: cabecalhoAntifalsificacao({ "Content-Type": "application/json" }),
    body: JSON.stringify({
      endpoint: json.endpoint,
      p256dh: json.keys.p256dh,
      auth: json.keys.auth,
    }),
  });
  await subscription.unsubscribe();
}

async function statusNotificacoesPush() {
  const motivo = motivoSemPush();
  if (motivo) return motivo === "precisa-instalar" ? "precisa-instalar" : "unsupported";
  const registration = await navigator.serviceWorker.ready;
  const subscription = await registration.pushManager.getSubscription();
  return subscription ? "subscribed" : "not-subscribed";
}
