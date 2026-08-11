// "Cadastre um local novo" — o campo que aparece embaixo do seletor de clube em Criar Aviso,
// Configurações do grupo e Registrar Jogo.
//
// Existe como arquivo porque as três telas tinham o MESMO código, copiado byte a byte. Quando
// a cidade passou a ser perguntada (11/08/2026), esse era o preço: a mesma mudança em três
// lugares, e o dia em que alguém mexesse só em dois ninguém saberia — o local nasceria sem
// cidade numa tela e com cidade nas outras, sem erro nenhum.
//
// A cidade importa porque o clube é o único lugar que sabe ONDE um torneio acontece
// (ver Services/UfDoTorneio), e é dela que sai a mira do aviso de torneio novo.
function adicionarClubeSelect() {
  var input = document.getElementById('novoClubeNome');
  var nome = input.value.trim();
  if (!nome) return;

  // Cidade e UF são OPCIONAIS: aqui o local costuma ser "onde a gente jogou", e exigir
  // endereço completo pra registrar um jogo é pedir demais no meio de outra tarefa.
  var cidade = document.getElementById('novoClubeCidade');
  var estado = document.getElementById('novoClubeEstado');

  var corpo = 'nome=' + encodeURIComponent(nome);
  if (cidade && cidade.value.trim()) corpo += '&cidade=' + encodeURIComponent(cidade.value.trim());
  if (estado && estado.value.trim()) corpo += '&estado=' + encodeURIComponent(estado.value.trim());

  fetch('/Clubes/Criar', {
    method: 'POST',
    headers: cabecalhoAntifalsificacao({ 'Content-Type': 'application/x-www-form-urlencoded' }),
    body: corpo,
  })
    .then(function (res) {
      // ⚠️ `res.json()` direto era um erro MUDO: quando o servidor recusa (nome vazio,
      // longo demais, sessão caída) a resposta não é JSON, a promessa quebra sem catch, e
      // pra quem clicou o botão simplesmente NÃO FAZ NADA. Ninguém reporta isso — a pessoa
      // conclui que o site travou e desiste do cadastro.
      if (!res.ok) throw new Error('recusado pelo servidor (' + res.status + ')');
      return res.json();
    })
    .then(function (clube) {
      var select = document.getElementById('selectClube');
      var option = document.createElement('option');
      option.value = clube.id;
      option.text = clube.nome;
      option.selected = true;
      select.appendChild(option);

      input.value = '';
      if (cidade) cidade.value = '';
      if (estado) estado.value = '';
    })
    .catch(function () {
      alert('Não consegui cadastrar esse local. Confira o nome e tente de novo.');
    });
}
