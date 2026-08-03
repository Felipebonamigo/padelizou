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
