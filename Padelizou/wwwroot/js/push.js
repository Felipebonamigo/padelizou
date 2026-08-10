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

// "Eu DESLIGUEI os avisos neste aparelho." Existe por causa da reinscrição silenciosa lá
// embaixo: desligar pelo perfil cancela a inscrição no navegador, mas NÃO devolve a permissão
// que a pessoa concedeu um dia — o navegador não expõe como fazer isso. Sem esta marca, o
// próximo carregamento veria "permissão concedida, sem inscrição" e religaria tudo sozinho,
// desfazendo a escolha da pessoa na cara dela.
//
// localStorage é o escopo certo: inscrição de push é POR APARELHO, e desligar no celular não
// pode desligar no notebook.
const CHAVE_PUSH_DESLIGADO = "pdzPushDesligado";

function marcarPushDesligado(desligado) {
  try {
    if (desligado) localStorage.setItem(CHAVE_PUSH_DESLIGADO, "1");
    else localStorage.removeItem(CHAVE_PUSH_DESLIGADO);
  } catch (e) {
    // Armazenamento bloqueado. A escolha vale nesta visita e nada quebra: a reinscrição
    // silenciosa é o único leitor, e ela erra pro lado de religar — não de calar.
  }
}

function pushDesligadoNesteAparelho() {
  try {
    return localStorage.getItem(CHAVE_PUSH_DESLIGADO) === "1";
  } catch (e) {
    return false;
  }
}

async function assinarPushNesteAparelho(registration) {
  const { publicKey } = await fetch("/Push/PublicKey").then((r) => r.json());
  return registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToUint8Array(publicKey),
  });
}

// O /Push/Subscribe é upsert pelo Endpoint: repetir não duplica aparelho, e é exatamente
// isso que faz a linha apagada por um 410 voltar a existir.
async function guardarInscricaoNoServidor(subscription) {
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
}

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

  // Sem `await` antes daqui de propósito: no iPhone a caixa de permissão só abre enquanto o
  // toque da pessoa ainda vale, e um await no meio pode queimar esse gesto.
  const permissao = await Notification.requestPermission();
  if (permissao !== "granted") return false;

  const registration = await navigator.serviceWorker.ready;
  const subscription = await assinarPushNesteAparelho(registration);
  await guardarInscricaoNoServidor(subscription);

  marcarPushDesligado(false);
  return true;
}

async function desativarNotificacoesPush() {
  // Marca ANTES de qualquer rede: se o Unsubscribe falhar no meio, o que não pode acontecer
  // é a reinscrição silenciosa achar que foi o navegador que perdeu a inscrição sozinho.
  marcarPushDesligado(true);

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

// Religa o push sozinho pra quem já disse sim um dia — sem caixa de permissão, sem nada na
// tela. Não é conveniência: é conserto de um vazamento silencioso.
//
// A inscrição morre por fora da nossa vontade (a pessoa reinstala o app, o navegador roda o
// endpoint) e o servidor de push responde 410 no envio seguinte. O PushNotificationService
// então apaga a linha — o certo a fazer — e a pessoa some do canal PRA SEMPRE, porque o único
// lugar que inscrevia era um botão dentro de Preferências. Ninguém volta lá por conta própria.
//
// Nada aqui pergunta nada: com a permissão já concedida, `subscribe()` não abre caixa nenhuma.
// Quem nunca autorizou, ou autorizou e desligou depois, não é tocado.
async function pdzReinscreverPushSePreciso() {
  try {
    if (!window.pdzJogadorLogado) return; // deslogado, o POST cairia na tela de login
    if (motivoSemPush()) return;
    if (Notification.permission !== "granted") return;
    if (pushDesligadoNesteAparelho()) return;

    // Uma vez por aba: navegar dentro do app não precisa reconferir, e sem esta trava toda
    // página aberta viraria um POST.
    try {
      if (sessionStorage.getItem("pdzPushConferido")) return;
      sessionStorage.setItem("pdzPushConferido", "1");
    } catch (e) {
      // Armazenamento bloqueado: confere mesmo assim, o upsert aguenta a repetição.
    }

    const registration = await navigator.serviceWorker.ready;
    const subscription =
      (await registration.pushManager.getSubscription()) ||
      (await assinarPushNesteAparelho(registration));

    // Reenvia mesmo quando o navegador JÁ tinha a inscrição: o caso que interessa é
    // justamente inscrição viva aqui e linha ausente no servidor. Só o POST descobre.
    await guardarInscricaoNoServidor(subscription);
  } catch (e) {
    // Falha aqui não pode aparecer pra ninguém — a pessoa não pediu isto, e o botão do
    // perfil continua sendo o caminho manual.
  }
}

document.addEventListener("DOMContentLoaded", pdzReinscreverPushSePreciso);
