// Recusa foto de perfil que não tem rosto nenhum.
//
// Pedido do Felipe em 09/09/2026 — "não suba, se nao tiver um rosto" — porque a foto de perfil
// vinha virando logo de time, print e paisagem, e o perfil é a cara da pessoa no site inteiro
// (busca, chave, cartão, chip do torneio).
//
// ⚠️ ISTO É CONVENIÊNCIA, NÃO SEGURANÇA. Roda no navegador da pessoa: quem quiser burlar,
// burla em dois cliques no DevTools. Serve contra o ENGANO, que é o problema real — ninguém
// está atacando o Padelizou pra pôr a logo do time no avatar. Por isso ela é fail-open em todo
// erro (ver abaixo) e por isso o servidor continua aceitando qualquer imagem: pôr essa trava no
// servidor custaria 147,8 MB de ONNX Runtime na VPS, ou mandar a foto de todo mundo pra uma API
// de terceiro. Nenhum dos dois se paga pra evitar erro de quem está distraído.
//
// ===================== POR QUE VARRE EM PEDAÇOS =====================
//
// O TinyFaceDetector ENCOLHE a foto inteira pra 416px antes de olhar. Então o que decide não é
// o tamanho do rosto em pixels — é a FRAÇÃO da foto que ele ocupa. Medido no Chromium em
// 09/09/2026, com foto real, afastando a pessoa aos poucos:
//
//     olhando a foto inteira:        rosto com 8,8% da altura -> acha; com 8,3% -> NÃO acha
//     olhando também em 5 pedaços:   desce até ~3,9% da altura
//
// 8,8% é a pessoa do peito pra cima. Uma foto de corpo inteiro na quadra tem o rosto em ~4% —
// seria recusada, e é foto legítima de gente de verdade. Os 5 pedaços ampliados existem pra
// isso: um rosto que na foto inteira virava 30px na entrada da rede vira 60px no recorte, e
// passa a ser visto. Abaixar o `scoreThreshold` NÃO substitui isso — medido: 0.3 e 0.5 dão
// exatamente o mesmo corte, porque o problema não é confiança baixa, é a rede não enxergar.
//
// O preço é tempo, e ele cai justamente no caso da recusa: 0,4s quando acha na primeira passada
// (o retrato comum), até 3,9s pra dizer "não" num celular modesto (medido com CPU 6x mais
// lenta). Por isso a tela avisa que está conferindo, e por isso o envio espera.
(function () {
    "use strict";

    var CAMINHO_LIB = "/lib/face-api/face-api.min.js";
    var CAMINHO_MODELO = "/lib/face-api";

    // 416 é o padrão da biblioteca e o que foi medido. Subir pra 608 desce o piso da foto
    // inteira pra 5,7%, mas dobra o tempo de TODA foto — inclusive das que iam passar de
    // primeira. Sai mais barato deixar os pedaços resolverem o caso difícil.
    var LADO_DA_REDE = 416;
    var CONFIANCA = 0.5;

    var promessaDaLib = null;

    // A biblioteca e o modelo somam ~850 KB. Baixar no carregamento da página seria cobrar
    // isso de todo mundo que abre o cadastro — inclusive de quem nem vai pôr foto. Só desce
    // quando a pessoa MEXE no campo da foto (ver aquecer).
    function carregarLib() {
        if (promessaDaLib) return promessaDaLib;

        promessaDaLib = new Promise(function (ok, erro) {
            var tag = document.createElement("script");
            tag.src = CAMINHO_LIB;
            tag.onload = ok;
            tag.onerror = function () { erro(new Error("não deu pra baixar o face-api.js")); };
            document.head.appendChild(tag);
        }).then(function () {
            return faceapi.nets.tinyFaceDetector.loadFromUri(CAMINHO_MODELO);
        }).then(function () {
            // A PRIMEIRA detecção compila os shaders do WebGL e sozinha custa ~3s num celular
            // modesto — as seguintes levam 0,8s. Rodando uma passada em branco aqui, esse
            // preço é pago enquanto a pessoa ainda está escolhendo a foto na galeria.
            var vazio = document.createElement("canvas");
            vazio.width = LADO_DA_REDE;
            vazio.height = LADO_DA_REDE;
            return faceapi.detectAllFaces(vazio, opcoes());
        });

        return promessaDaLib;
    }

    // Medido com CPU 6x mais lenta: 1ª foto 4,5s, 2ª foto 0,8s — a diferença é toda preparo.
    // Entre tocar no campo e escolher a foto passam vários segundos, e é onde esse tempo cabe
    // sem ninguém esperar por ele. `focus` e `click` porque no celular nem sempre vêm os dois;
    // como carregarLib() guarda a promessa, chamar duas vezes não baixa duas vezes.
    //
    // O catch aqui é obrigatório: sem ninguém esperando por esta promessa, uma falha de rede
    // viraria "unhandled rejection" no console. Quem trata a falha de verdade é o change, que
    // chama carregarLib() de novo e libera a foto (fail-open).
    function aquecer() {
        carregarLib().catch(function () { /* o change decide o que fazer; aqui é só adiantar */ });
    }

    function opcoes() {
        return new faceapi.TinyFaceDetectorOptions({
            inputSize: LADO_DA_REDE,
            scoreThreshold: CONFIANCA,
        });
    }

    // Recorta um pedaço e AMPLIA pro tamanho da entrada da rede. É a ampliação que faz o rosto
    // pequeno ser visto — não o número de passadas.
    function recorte(imagem, x, y, largura, altura) {
        var tela = document.createElement("canvas");
        tela.width = LADO_DA_REDE;
        tela.height = LADO_DA_REDE;
        tela.getContext("2d").drawImage(imagem, x, y, largura, altura, 0, 0, LADO_DA_REDE, LADO_DA_REDE);
        return tela;
    }

    // Foto inteira primeiro (é o caso comum e o mais barato). Só se não achar nada é que vale
    // pagar os 5 pedaços — 4 cantos e o centro, com sobreposição pra um rosto em cima da divisa
    // não ser cortado ao meio nas duas vezes.
    async function varrer(imagem) {
        var achados = await faceapi.detectAllFaces(imagem, opcoes());
        if (achados.length > 0) return true;

        var L = imagem.naturalWidth || imagem.width;
        var A = imagem.naturalHeight || imagem.height;
        var pedacos = [
            [0, 0], [L * 0.4, 0], [0, A * 0.4], [L * 0.4, A * 0.4], [L * 0.2, A * 0.2],
        ];

        for (var i = 0; i < pedacos.length; i++) {
            var tela = recorte(imagem, pedacos[i][0], pedacos[i][1], L * 0.6, A * 0.6);
            var noPedaco = await faceapi.detectAllFaces(tela, opcoes());
            if (noPedaco.length > 0) return true;
        }

        return false;
    }

    function abrirImagem(arquivo) {
        return new Promise(function (ok, erro) {
            var url = URL.createObjectURL(arquivo);
            var imagem = new Image();
            imagem.onload = function () { URL.revokeObjectURL(url); ok(imagem); };
            imagem.onerror = function () {
                URL.revokeObjectURL(url);
                erro(new Error("o navegador não conseguiu abrir essa imagem"));
            };
            imagem.src = url;
        });
    }

    function ligar(campo) {
        var recado = document.createElement("div");
        recado.className = "small mt-2";
        recado.hidden = true;
        campo.insertAdjacentElement("afterend", recado);

        var conferindo = false;
        var esperandoEnvio = false;
        var form = campo.form;

        campo.addEventListener("focus", aquecer, { once: true });
        campo.addEventListener("click", aquecer, { once: true });

        function dizer(texto, classe) {
            recado.className = "small mt-2 " + classe;
            recado.textContent = texto;
            recado.hidden = false;
        }

        campo.addEventListener("change", async function () {
            recado.hidden = true;
            var arquivo = campo.files && campo.files[0];
            if (!arquivo) return;

            conferindo = true;
            dizer("Conferindo a foto…", "text-muted");

            var temRosto;
            try {
                await carregarLib();
                var imagem = await abrirImagem(arquivo);
                temRosto = await varrer(imagem);
            } catch (erro) {
                // fail-open: navegador antigo, WASM bloqueado, modelo que não baixou no 4G ruim,
                // memória curta num celular velho. A foto SOBE. Recusar por motivo técnico
                // deixaria a pessoa sem conseguir trocar de foto, sem nada que ela possa fazer a
                // respeito — e a conferência existe contra o engano, não contra ninguém.
                console.warn("Conferência de rosto não rodou; a foto passou sem ela.", erro);
                recado.hidden = true;
                temRosto = true;
            }

            conferindo = false;

            if (!temRosto) {
                // Limpa SÓ a foto. O formulário do perfil tem nome, telefone, time e sedes:
                // derrubar o envio inteiro faria a pessoa perder tudo por causa do campo que
                // menos importa — a mesma escolha que a carência de nome já faz aqui.
                campo.value = "";
                dizer("Não encontramos um rosto nessa foto. A foto de perfil precisa ser sua — "
                    + "se a pessoa aparecer de longe, tente uma foto mais de perto.", "text-danger");
            } else {
                recado.hidden = true;
            }

            if (esperandoEnvio) {
                esperandoEnvio = false;
                if (form) form.requestSubmit();
            }
        });

        // A conferência leva de 0,4s a 3,9s, e o pior caso é justamente a foto que vai ser
        // recusada. Sem segurar o envio, quem apertar Salvar nesse meio-tempo sobe a foto sem
        // ela ter sido conferida — a trava viraria sorte de cronometragem.
        if (form) {
            form.addEventListener("submit", function (evento) {
                if (!conferindo) return;
                evento.preventDefault();
                esperandoEnvio = true;
                dizer("Conferindo a foto… o cadastro vai seguir assim que terminar.", "text-muted");
            });
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        var campos = document.querySelectorAll('input[type="file"][name="foto"]');
        for (var i = 0; i < campos.length; i++) ligar(campos[i]);
    });
})();
