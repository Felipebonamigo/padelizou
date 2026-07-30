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

document.addEventListener("DOMContentLoaded", pdzAtualizarMenuInstalar);
document.addEventListener("pdz:instalavel", pdzAtualizarMenuInstalar);
document.addEventListener("pdz:instalado", pdzAtualizarMenuInstalar);
