// "Cadastre um local novo" — o campo que aparece embaixo do seletor de clube em Criar Aviso,
// Configurações do grupo, Registrar Jogo e, desde 08/09/2026, no editor de sedes do torneio.
//
// Existe como arquivo porque as três primeiras telas tinham o MESMO código, copiado byte a
// byte. Quando a cidade passou a ser perguntada (11/08/2026), esse era o preço: a mesma
// mudança em três lugares, e o dia em que alguém mexesse só em dois ninguém saberia — o local
// nasceria sem cidade numa tela e com cidade nas outras, sem erro nenhum.
//
// A cidade importa porque o clube é o único lugar que sabe ONDE um torneio acontece
// (ver Services/UfDoTorneio), e é dela que sai a mira do aviso de torneio novo.

// ⚠️ A CHAMADA AO SERVIDOR MORA AQUI, UMA VEZ SÓ — é o motivo de o arquivo existir. Quem
// precisar cadastrar local numa tela nova chama esta função e cuida só de onde põe a opção;
// um `fetch` próprio devolveria o projeto ao estado que este cabeçalho descreve, e a parte que
// as cópias erravam era exatamente esta: o tratamento do erro logo abaixo.
function pdzCriarClube(nome, cidade, estado) {
    // Cidade e UF são OPCIONAIS: às vezes o local é só "onde a gente jogou", e exigir endereço
    // completo no meio de outra tarefa é atrito na hora errada.
    var corpo = 'nome=' + encodeURIComponent(nome);
    if (cidade && cidade.trim()) corpo += '&cidade=' + encodeURIComponent(cidade.trim());
    if (estado && estado.trim()) corpo += '&estado=' + encodeURIComponent(estado.trim());

    return fetch('/Clubes/Criar', {
        method: 'POST',
        headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
        body: corpo,
    }).then(function (res) {
        // ⚠️ `res.json()` direto era um erro MUDO: quando o servidor recusa (nome vazio,
        // longo demais, sessão caída) a resposta não é JSON, a promessa quebra sem catch, e
        // pra quem clicou o botão simplesmente NÃO FAZ NADA. Ninguém reporta isso — a pessoa
        // conclui que o site travou e desiste do cadastro.
        if (!res.ok) throw new Error('recusado pelo servidor (' + res.status + ')');
        return res.json();
    });
}

function adicionarClubeSelect() {
  var input = document.getElementById('novoClubeNome');
  var nome = input.value.trim();
  if (!nome) return;

  var cidade = document.getElementById('novoClubeCidade');
  var estado = document.getElementById('novoClubeEstado');

  pdzCriarClube(nome, cidade && cidade.value, estado && estado.value)
    .then(function (clube) {
      var select = document.getElementById('selectClube');
      var option = document.createElement('option');
      option.value = clube.id;
      option.text = clube.nome;
      option.selected = true;

      // ⚠️ O SELETOR É AGRUPADO POR CIDADE (`<optgroup>`, ver CatalogoLocais.AgrupadosPorCidade).
      // Pendurar a opção direto no <select> a joga pra DEPOIS de todos os grupos: ela aparece
      // solta no fim, fora da lista em que acabou de entrar, e quem cadastrou acha que deu
      // errado. O rótulo do grupo vem do servidor — é ele que sabe como a cidade foi
      // normalizada. Sem optgroup nenhum na tela, o append direto continua valendo.
      var rotulo = clube.cidade;
      if (rotulo && select.querySelector('optgroup')) {
        var grupos = select.querySelectorAll('optgroup');
        var alvo = null;
        for (var i = 0; i < grupos.length; i++) {
          if (grupos[i].label === rotulo) { alvo = grupos[i]; break; }
        }
        if (!alvo) {
          alvo = document.createElement('optgroup');
          alvo.label = rotulo;
          select.appendChild(alvo);
        }
        alvo.appendChild(option);
      } else {
        select.appendChild(option);
      }

      input.value = '';
      if (cidade) cidade.value = '';
      if (estado) estado.value = '';
    })
    .catch(function () {
      alert('Não consegui cadastrar esse local. Confira o nome e tente de novo.');
    });
}

// ── O editor de sedes do torneio (aba "Pagamentos e impedimentos" › "Quadras e sedes") ──
//
// Duas diferenças pras outras três telas. A primeira: aqui NÃO há um seletor de clube, há UM
// POR QUADRA, e o valor de cada opção é "quadraId:clubeId" — é assim que
// Services/SedesDoTorneio.LerClubePorQuadra lê o POST. Uma opção com só o id do clube seria
// aceita pelo <select>, viajaria no formulário e cairia fora na leitura: a quadra voltaria pro
// clube do torneio em silêncio.
//
// A segunda: cadastrar o clube NÃO É O FIM.
//
// 🗣️ Felipe: "mas aqui eu nao consigo selecionar o clube, parece um bug".
//
// A primeira versão só punha o clube na lista das quadras e parava. Do lado de quem usa,
// cadastrar um clube e ver os seletores continuarem todos em "Er Padel" é a mesma coisa que
// não ter funcionado — e quando o nome já existia, o achar-ou-criar do servidor devolvia o
// clube antigo e a resposta era "já estava na lista": nem cadastrou, nem colocou em lugar
// nenhum, nem disse o que fazer em seguida. Por isso o bloco agora pergunta EM QUAL QUADRA.
function adicionarClubeNasSedes() {
    var input = document.getElementById('pdzNovoClubeSede');
    var aviso = document.getElementById('pdzNovoClubeSedeAviso');
    if (!input) return;

    var falar = function (texto) { if (aviso) aviso.textContent = texto; };

    var nome = (input.value || '').trim();
    if (!nome) { falar('Escreva o nome do clube antes de adicionar.'); return; }

    var cidade = document.getElementById('pdzNovoClubeSedeCidade');
    var estado = document.getElementById('pdzNovoClubeSedeEstado');
    var ondeFica = document.getElementById('pdzNovoClubeSedeQuadra');

    var alvo = ondeFica ? String(ondeFica.value) : '';
    var nomeDaQuadra = (ondeFica && ondeFica.selectedIndex >= 0)
        ? ondeFica.options[ondeFica.selectedIndex].text.trim()
        : '';

    // Torneio sem quadra nenhuma: o clube não teria onde pousar, e cadastrá-lo aqui só encheria
    // o catálogo geral. A quadra nasce em "Gerenciar Torneio" — esta tela não cria quadra.
    if (!alvo) {
        falar('Este torneio ainda não tem quadra pra receber o clube. '
            + 'Cadastre as quadras em "Gerenciar Torneio" e volte aqui.');
        return;
    }

    falar('Cadastrando…');

    pdzCriarClube(nome, cidade && cidade.value, estado && estado.value)
        .then(function (clube) {
            var trocado = null;

            Array.prototype.forEach.call(document.querySelectorAll('.pdz-sede-da-quadra'), function (select) {
                if (select.options.length === 0) return;

                // A quadra sai da PRIMEIRA opção, não da selecionada: as opções de um select são
                // todas da mesma quadra, e assim não depende de haver algo escolhido.
                var quadra = String(select.options[0].value).split(':')[0];

                // O servidor é achar-ou-criar: digitar um clube que já está na lista devolve o
                // que já existia. Sem esta conferência ele apareceria duas vezes no seletor.
                var repetido = Array.prototype.some.call(select.options, function (o) {
                    return String(o.value).split(':')[1] === String(clube.id);
                });

                if (!repetido) {
                    var option = document.createElement('option');
                    option.value = quadra + ':' + clube.id;
                    option.text = clube.nome;
                    select.appendChild(option);
                }

                // ⚠️ A LISTA cresce em TODAS as quadras (é a mesma lista de clubes em toda
                // parte), mas a SEDE muda só na escolhida. Trocar as outras junto mudaria o
                // torneio inteiro de endereço por causa de um cadastro — num torneio de duas
                // quadras, isso é esvaziar a sede principal sem ninguém ter pedido.
                if (quadra === alvo) {
                    select.value = quadra + ':' + clube.id;
                    trocado = select;
                }
            });

            input.value = '';
            if (cidade) cidade.value = '';
            if (estado) estado.value = '';

            if (!trocado) {
                falar('Cadastrei ' + clube.nome + ', mas não achei a quadra escolhida na tela. '
                    + 'Recarregue a página e escolha o clube na lista da quadra.');
                return;
            }

            // ⚠️ MUDAR `select.value` POR CÓDIGO NÃO DISPARA `change`. Quem remonta a tabela "em
            // que clube cada categoria joga" é o listener de `change` que vive no Details.cshtml,
            // lendo o que está SELECIONADO nos seletores de quadra. Sem este aviso a sede nova
            // apareceria em cima e a tabela seguiria oferecendo só a antiga — e a trava de
            // categoria por clube é justamente o que se configura em seguida.
            trocado.dispatchEvent(new Event('change', { bubbles: true }));

            falar(clube.nome + ' agora é a sede da quadra ' + nomeDaQuadra
                + '. Clique em "Salvar sedes" pra valer.');
        })
        .catch(function () {
            falar('Não consegui cadastrar esse clube. Confira o nome e tente de novo.');
        });
}
