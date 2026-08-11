// Copiar o link do convite do parceiro.
//
// Fica num arquivo, e não num onclick inline, pela regra do cache de script do projeto: todo
// <script> em Views precisa de asp-append-version, e código solto na página não tem versão
// nenhuma pra o Service Worker invalidar.
function pdzCopiarConvite() {
    const campo = document.getElementById('pdzLinkConvite');
    if (!campo) return;

    const aviso = (texto) => {
        const botao = campo.parentElement.querySelector('button');
        if (!botao) return;
        const original = botao.innerHTML;
        botao.innerHTML = texto;
        setTimeout(() => { botao.innerHTML = original; }, 2000);
    };

    // navigator.clipboard não existe fora de HTTPS (e o localhost do dev roda em http).
    // Sem o plano B, o botão não faria nada e ninguém saberia por quê.
    if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(campo.value)
            .then(() => aviso('<i class="bi bi-check2"></i> Copiado'))
            .catch(() => aviso('Copie à mão'));
        return;
    }

    campo.select();
    campo.setSelectionRange(0, 99999);
    try {
        document.execCommand('copy');
        aviso('<i class="bi bi-check2"></i> Copiado');
    } catch {
        aviso('Copie à mão');
    }
}
