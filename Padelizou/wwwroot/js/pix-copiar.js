// O botão "Copiar" da tela do Pix. Arquivo próprio (e não <script> inline) porque todo
// script de view passa pelo cache do service worker — precisa do asp-append-version.
(function () {
    var botao = document.getElementById('botao-copiar');
    var campo = document.getElementById('pix-copia-e-cola');
    if (!botao || !campo) return;

    botao.addEventListener('click', function () {
        campo.select();
        // O caminho moderno primeiro; execCommand de reserva pra WebView antiga de app de
        // banco, que é exatamente onde esta tela mais abre.
        var copiar = navigator.clipboard
            ? navigator.clipboard.writeText(campo.value)
            : Promise.reject();

        copiar.catch(function () { document.execCommand('copy'); }).then(function () {
            botao.innerHTML = '<i class="bi bi-clipboard-check"></i> Copiado!';
            setTimeout(function () {
                botao.innerHTML = '<i class="bi bi-clipboard"></i> Copiar';
            }, 2000);
        });
    });
})();
