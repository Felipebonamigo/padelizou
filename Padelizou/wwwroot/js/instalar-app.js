// Instalação do app. O evento em si (`beforeinstallprompt`) é capturado lá no <head> do
// _Layout, porque o Chrome dispara ele cedo — um script no fim do <body> às vezes chega
// tarde e perde o evento, e aí o botão de instalar nunca aparece.
//
// Duas realidades diferentes, de propósito:
//   Android/Chrome — dá pra abrir a caixa NATIVA de instalar com um toque.
//   iPhone/Safari  — a Apple não expõe nada disso; só resta ensinar o caminho da mão.

function pdzAppInstalado() {
  return window.matchMedia("(display-mode: standalone)").matches || window.navigator.standalone === true;
}

function pdzPlataforma() {
  var ua = navigator.userAgent || "";
  if (/iPhone|iPad|iPod/.test(ua)) return "ios";
  if (/Android/.test(ua)) return "android";
  return null;
}

function pdzTemInstalacaoNativa() {
  return !!window.pdzEventoInstalacao;
}

// Onde o passo a passo do modal seria MENTIRA, porque o navegador da vez não instala nada.
//
// É o caso mais comum de todos e passou despercebido até 10/08/2026: o Padelizou circula em
// link de WhatsApp, e link de WhatsApp não abre no navegador da pessoa — abre num navegador
// de dentro do próprio WhatsApp. No iPhone, "Adicionar à Tela de Início" existe SÓ no Safari
// de verdade: naquela tela o item não está no menu, e o modal mandava procurar uma coisa que
// não existe ali. Quem procurou e não achou concluiu que o app não instala.
//
// `navigator.standalone` é a prova, e é melhor que farejar o User-Agent (que WhatsApp e
// Instagram não marcam de forma confiável no iOS): o Safari do iPhone declara essa
// propriedade — `false` navegando, `true` já instalado —, e WebView de outro app não a tem.
// Chrome e Firefox do iPhone caem aqui junto, e é o certo: o atalho criado por eles não
// recebe push, que é justamente o motivo de a gente querer a instalação.
//
// No Android o problema é menor (o Chrome abre de verdade na maioria dos casos), então aqui
// só o User-Agent das WebViews que sabidamente não instalam.
function pdzNavegadorParaTrocar() {
  if (pdzAppInstalado() || pdzTemInstalacaoNativa()) return null;

  var plataforma = pdzPlataforma();
  if (plataforma === "ios") return typeof navigator.standalone === "undefined" ? "safari" : null;
  if (plataforma === "android") return /Instagram|FBAN|FBAV|FB_IAB|Line\//.test(navigator.userAgent || "") ? "chrome" : null;
  return null;
}

// ————————————————————————————————————————————————————————————————
// A régua dos convites (instalar o app, ligar os avisos)
//
// Até 10/08/2026 QUALQUER fechamento gravava "dispensado" pra sempre, e isso derrubava
// justamente quem estava indo instalar: no iPhone os três toques acontecem FORA do modal, de
// modo que fechar é exatamente o que a pessoa faz PRA seguir o passo a passo. O botão que
// dizia "vou fazer agora" e o que dizia "nunca mais me pergunte" eram o mesmo botão.
//
// Agora fechar ADIA, e só o "Não quero" — dito com todas as letras, num botão próprio —
// encerra de vez. A espera cresce a cada vez pra não virar chateação: quem ignora três
// convites não recebe um quarto.
//
// Mora aqui, e não dentro de cada modal, porque são dois convites com a mesma regra: duas
// cópias significariam, inevitavelmente, uma delas voltando a tratar "agora não" como "não".
var PDZ_ESPERA_DIAS = [3, 10, 21];

function pdzGravarConvite(chave, valor) {
  try {
    localStorage.setItem(chave, JSON.stringify(valor));
  } catch (e) {
    // Armazenamento bloqueado (aba anônima, iPhone com rastreamento cortado). Sem memória, o
    // convite volta na próxima visita — errar pro lado de convidar demais é melhor que sumir.
  }
}

function pdzLerConvite(chave) {
  var vazio = { vezes: 0, em: 0, recusado: false };

  var bruto = null;
  try {
    bruto = localStorage.getItem(chave);
  } catch (e) {
    return vazio;
  }
  if (!bruto) return vazio;

  // Marca antiga ('1'), gravada por qualquer fechamento até 10/08/2026. Ela vale como UM
  // adiamento e não como recusa, porque a pessoa nunca teve como dizer "não quero" — o
  // aparelho dela decidiu isso por ela.
  //
  // ⚠️ A conversão é GRAVADA aqui de propósito: devolvê-la sem persistir faria `em` virar
  // "agora" a cada carregamento de página, o relógio nunca completaria os 3 dias, e o
  // reconvite não chegaria nunca — o mesmo defeito de antes, agora silencioso.
  if (bruto === "1") {
    var convertido = { vezes: 1, em: Date.now(), recusado: false };
    pdzGravarConvite(chave, convertido);
    return convertido;
  }

  try {
    var lido = JSON.parse(bruto);
    return { vezes: lido.vezes || 0, em: lido.em || 0, recusado: lido.recusado === true };
  } catch (e) {
    return vazio;
  }
}

function pdzAdiarConvite(chave) {
  var atual = pdzLerConvite(chave);
  pdzGravarConvite(chave, { vezes: atual.vezes + 1, em: Date.now(), recusado: atual.recusado });
}

function pdzRecusarConvite(chave) {
  pdzGravarConvite(chave, { vezes: pdzLerConvite(chave).vezes, em: Date.now(), recusado: true });
}

// "Eu não quero isto", dito no botão próprio. Separado de pdzPodeConvidar porque o convite
// que sai LOGO DEPOIS de uma inscrição (ver Services/ConviteDeInstalarApp) atropela de
// propósito a espera do adiamento — a pessoa acabou de fazer justamente a coisa que os avisos
// acompanham. O que ele não pode atropelar é a recusa: fechar por pressa e dizer "não quero"
// são as duas coisas que este arquivo inteiro existe pra parar de confundir.
function pdzConviteRecusado(chave) {
  return pdzLerConvite(chave).recusado;
}

function pdzPodeConvidar(chave) {
  var estado = pdzLerConvite(chave);

  if (estado.recusado) return false;
  if (estado.vezes === 0) return true;
  if (estado.vezes > PDZ_ESPERA_DIAS.length) return false;

  return Date.now() - estado.em >= PDZ_ESPERA_DIAS[estado.vezes - 1] * 24 * 60 * 60 * 1000;
}

// Abre a caixa nativa do Android. Devolve true se a pessoa aceitou instalar.
async function pdzInstalarApp() {
  var evento = window.pdzEventoInstalacao;
  if (!evento) return false;

  evento.prompt();
  var escolha = await evento.userChoice;

  // O evento é de uso único: depois de mostrado, o Chrome não deixa reaproveitar.
  window.pdzEventoInstalacao = null;
  return escolha && escolha.outcome === "accepted";
}

// O item "Instalar o app" do menu só faz sentido pra quem ainda não instalou. Aparece
// quando o Chrome avisa que dá pra instalar, ou quando é um celular (no iPhone o aviso
// nunca vem, mas o passo a passo manual continua valendo).
function pdzAtualizarMenuInstalar() {
  var item = document.getElementById("pdzMenuInstalar");
  if (!item) return;
  item.style.display = !pdzAppInstalado() && (pdzTemInstalacaoNativa() || pdzPlataforma()) ? "" : "none";
}

// Conta pro servidor que este jogador já instalou. Só o navegador sabe disso — a requisição
// que chega no servidor é idêntica instalado ou não —, então quem avisa é esta função.
//
// Sem isto, os Primeiros Passos usavam "aceitou notificação" como se fosse "instalou", e
// quem instalava sem liberar aviso ficava com o passo pendente pra sempre.
//
// São DOIS sinais e eles chegam em momentos diferentes:
//   `display-mode: standalone` — a pessoa abriu pelo ícone. Vale em qualquer carregamento.
//   evento `appinstalled`      — acabou de instalar, com a página ainda na aba do navegador.
// No segundo caso a tela AINDA NÃO está em standalone, então quem chama por ali passa
// `certeza = true`; conferir o display-mode aqui perderia justamente a instalação recém-feita.
function pdzAvisarQueInstalou(certeza) {
  if (!certeza && !pdzAppInstalado()) return;

  // Uma vez por aba: o carimbo no servidor não muda depois do primeiro, e sem esta trava
  // toda navegação dentro do app dispararia um POST que não faz nada.
  try {
    if (sessionStorage.getItem("pdzInstalacaoAvisada")) return;
    sessionStorage.setItem("pdzInstalacaoAvisada", "1");
  } catch (e) {
    // Navegador com armazenamento bloqueado: avisa mesmo assim. O servidor é idempotente.
  }

  fetch("/AppInstalado/Registrar", {
    method: "POST",
    headers: typeof cabecalhoAntifalsificacao === "function" ? cabecalhoAntifalsificacao() : {}
  }).catch(function () {
    // Visitante deslogado responde 401 e app offline nem sai — os dois são normais aqui, e
    // nenhum merece erro no console de quem só queria abrir a tela.
  });
}

document.addEventListener("DOMContentLoaded", pdzAtualizarMenuInstalar);
document.addEventListener("pdz:instalavel", pdzAtualizarMenuInstalar);
document.addEventListener("pdz:instalado", pdzAtualizarMenuInstalar);

document.addEventListener("DOMContentLoaded", function () { pdzAvisarQueInstalou(false); });
// O Android instala com o app aberto: sem escutar isto, o carimbo só sairia na próxima vez
// que a pessoa abrisse pelo ícone.
document.addEventListener("pdz:instalado", function () { pdzAvisarQueInstalou(true); });
