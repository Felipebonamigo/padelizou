// O TECLADO DE EMOJI — 12/09/2026.
//
// 🗣️ Felipe, com um print do WhatsApp no celular (a barra de reação rápida por cima da mensagem
// e, embaixo, o teclado inteiro com busca, FREQUENTES e categorias): *"os emojis tem q abrir
// igual esse do whats com o teclado de emojis"*.
//
// ⚠️ TECLADO NOSSO, e não biblioteca de npm: não existe `package.json` em lugar nenhum deste
// repositório (ver SUPPLY-CHAIN.md), e os conferidores de JS são "sem dependência nenhuma de
// propósito". Um picker de npm traria package.json, lockfile e cadeia de suprimentos inteira
// pra um projeto que hoje não tem nada disso — pelo degrau 5 da escada do CLAUDE.md, não entra.
//
// ⚠️ CARREGADO SOB DEMANDA pelo `reacoes-do-jogo.js`, no primeiro toque no "+". A página do
// torneio em produção tem 1,08 MB de HTML e já puxa 28 arquivos de JS/CSS; o teclado é a única
// parte que a maioria nunca abre, e carregá-lo junto faria todo mundo pagar por ele.
//
// ⚠️ ~350 CURADOS, não o conjunto Unicode inteiro (escolha do Felipe entre as três opções). A
// busca cobre o que não está na grade — e o que não está aqui continua dando de colar no campo,
// porque a peneira do servidor aceita qualquer emoji, não uma lista fechada.
//
// ⚠️ TODA a tabela abaixo passa pela peneira do servidor (Services/EmojiDeReacao) — tem teste
// casando os dois (`TODO_emoji_do_teclado_passa_pela_peneira_do_servidor`). Um emoji que o
// servidor recusasse seria um botão que responde "isso não é um emoji" na cara de quem tocou.
// Por isso nada de `‼️`, `⁉️`, `↔️` e companhia: o code point base deles é pontuação.
//
// O campo é `e` (emoji) e `n` (nome, o que a busca lê) — curto porque a tabela é longa.
(function () {
    'use strict';

    const CATEGORIAS = [
        {
            id: 'rostos', rotulo: 'Rostos', icone: '🙂', emojis: [
                { e: '😀', n: 'sorriso feliz' }, { e: '😃', n: 'sorriso alegre' },
                { e: '😄', n: 'rindo feliz' }, { e: '😁', n: 'sorriso dentes' },
                { e: '😆', n: 'rindo muito' }, { e: '😅', n: 'rindo suor alivio' },
                { e: '🤣', n: 'rolando de rir chorando' }, { e: '😂', n: 'chorando de rir' },
                { e: '🙂', n: 'sorriso leve' }, { e: '🙃', n: 'de cabeca para baixo' },
                { e: '😉', n: 'piscando' }, { e: '😊', n: 'feliz corado' },
                { e: '😇', n: 'anjo auréola' }, { e: '🥰', n: 'apaixonado coracoes' },
                { e: '😍', n: 'olhos de coracao amor' }, { e: '🤩', n: 'estrelas deslumbrado' },
                { e: '😘', n: 'beijo' }, { e: '😗', n: 'beijinho' },
                { e: '😙', n: 'beijo sorrindo' }, { e: '😚', n: 'beijo olhos fechados' },
                { e: '😋', n: 'delicioso lambendo' }, { e: '😛', n: 'lingua' },
                { e: '😜', n: 'lingua piscando' }, { e: '🤪', n: 'doido maluco' },
                { e: '😝', n: 'lingua olhos fechados' }, { e: '🤑', n: 'dinheiro rico' },
                { e: '🤗', n: 'abraco' }, { e: '🤭', n: 'mao na boca ops' },
                { e: '🤫', n: 'silencio shh' }, { e: '🤔', n: 'pensando duvida' },
                { e: '🤐', n: 'boca fechada zíper' }, { e: '🤨', n: 'sobrancelha desconfiado' },
                { e: '😐', n: 'neutro' }, { e: '😑', n: 'sem expressao' },
                { e: '😶', n: 'sem boca' }, { e: '😏', n: 'malicioso sorrisinho' },
                { e: '😒', n: 'sem graca entediado' }, { e: '🙄', n: 'revirando os olhos' },
                { e: '😬', n: 'careta nervoso' }, { e: '🤥', n: 'mentira pinoquio' },
                { e: '😌', n: 'aliviado' }, { e: '😔', n: 'triste pensativo' },
                { e: '😪', n: 'sono' }, { e: '🤤', n: 'babando' },
                { e: '😴', n: 'dormindo' }, { e: '😷', n: 'mascara' },
                { e: '🤒', n: 'doente febre' }, { e: '🤕', n: 'machucado atadura' },
                { e: '🤢', n: 'nojo enjoado' }, { e: '🤮', n: 'vomitando' },
                { e: '🤧', n: 'espirrando' }, { e: '🥵', n: 'calor derretendo' },
                { e: '🥶', n: 'frio congelando' }, { e: '🥴', n: 'tonto zonzo' },
                { e: '😵', n: 'atordoado' }, { e: '🤯', n: 'cabeca explodindo' },
                { e: '🤠', n: 'cowboy chapeu' }, { e: '🥳', n: 'festa comemorando' },
                { e: '😎', n: 'oculos de sol estiloso' }, { e: '🤓', n: 'nerd' },
                { e: '🧐', n: 'monoculo analisando' }, { e: '😕', n: 'confuso' },
                { e: '😟', n: 'preocupado' }, { e: '🙁', n: 'insatisfeito' },
                { e: '😮', n: 'boca aberta surpreso' }, { e: '😯', n: 'atonito' },
                { e: '😲', n: 'chocado espantado' }, { e: '😳', n: 'vermelho envergonhado' },
                { e: '🥺', n: 'implorando suplicante' }, { e: '😦', n: 'franzindo' },
                { e: '😧', n: 'angustiado' }, { e: '😨', n: 'medo assustado' },
                { e: '😰', n: 'ansioso suor' }, { e: '😥', n: 'triste aliviado' },
                { e: '😢', n: 'chorando lagrima' }, { e: '😭', n: 'chorando muito' },
                { e: '😱', n: 'grito de medo' }, { e: '😖', n: 'confundido' },
                { e: '😣', n: 'perseverando' }, { e: '😞', n: 'desapontado' },
                { e: '😓', n: 'suando frio' }, { e: '😩', n: 'cansado' },
                { e: '😫', n: 'exausto' }, { e: '🥱', n: 'bocejando' },
                { e: '😤', n: 'bufando triunfante' }, { e: '😡', n: 'furioso raiva' },
                { e: '😠', n: 'zangado' }, { e: '🤬', n: 'praguejando xingando' },
                { e: '😈', n: 'diabinho travesso' }, { e: '👿', n: 'diabo raiva' },
                { e: '💀', n: 'caveira morte' }, { e: '💩', n: 'coco' },
                { e: '🤡', n: 'palhaco' }, { e: '👻', n: 'fantasma' },
                { e: '👽', n: 'alienigena' }, { e: '🤖', n: 'robo' },
                { e: '😺', n: 'gato sorrindo' }, { e: '😹', n: 'gato chorando de rir' },
                { e: '😻', n: 'gato apaixonado' }, { e: '🙈', n: 'macaco tapando olhos' },
                { e: '🙉', n: 'macaco tapando ouvidos' }, { e: '🙊', n: 'macaco tapando boca' }
            ]
        },
        {
            id: 'gestos', rotulo: 'Gestos', icone: '👍', emojis: [
                { e: '👍', n: 'curtir positivo joia' }, { e: '👎', n: 'negativo nao gostei' },
                { e: '👏', n: 'palmas aplausos' }, { e: '🙌', n: 'maos ao alto comemorando' },
                { e: '👐', n: 'maos abertas' }, { e: '🤲', n: 'maos juntas palmas para cima' },
                { e: '🙏', n: 'obrigado por favor reza' }, { e: '🤝', n: 'aperto de mao acordo' },
                { e: '👊', n: 'soco punho' }, { e: '✊', n: 'punho erguido' },
                { e: '🤛', n: 'punho esquerda' }, { e: '🤜', n: 'punho direita' },
                { e: '✌️', n: 'paz e amor vitoria' }, { e: '🤞', n: 'dedos cruzados sorte' },
                { e: '🤟', n: 'te amo mao' }, { e: '🤘', n: 'rock chifrinho' },
                { e: '👌', n: 'ok perfeito' }, { e: '🤌', n: 'dedos juntos italiano' },
                { e: '🤏', n: 'pouquinho' }, { e: '👈', n: 'apontando esquerda' },
                { e: '👉', n: 'apontando direita' }, { e: '👆', n: 'apontando para cima' },
                { e: '👇', n: 'apontando para baixo' }, { e: '☝️', n: 'dedo para cima atencao' },
                { e: '✋', n: 'mao levantada pare' }, { e: '🤚', n: 'costas da mao' },
                { e: '🖐️', n: 'mao dedos abertos' }, { e: '🖖', n: 'saudacao vulcana' },
                { e: '👋', n: 'tchau oi acenando' }, { e: '🤙', n: 'me liga' },
                { e: '💪', n: 'forca musculo biceps' }, { e: '🦾', n: 'braco mecanico' },
                { e: '🖕', n: 'dedo do meio' }, { e: '✍️', n: 'escrevendo' },
                { e: '🤳', n: 'selfie' }, { e: '💅', n: 'unhas' },
                { e: '👀', n: 'olhos olhando' }, { e: '👁️', n: 'olho' },
                { e: '🧠', n: 'cerebro' }, { e: '🦵', n: 'perna' },
                { e: '🦶', n: 'pe' }, { e: '👂', n: 'orelha' },
                { e: '👃', n: 'nariz' }, { e: '👄', n: 'boca labios' },
                { e: '🦷', n: 'dente' }, { e: '🫀', n: 'coracao orgao' }
            ]
        },
        {
            id: 'pessoas', rotulo: 'Pessoas', icone: '🧑', emojis: [
                { e: '👶', n: 'bebe' }, { e: '🧒', n: 'crianca' },
                { e: '👦', n: 'menino' }, { e: '👧', n: 'menina' },
                { e: '🧑', n: 'pessoa' }, { e: '👨', n: 'homem' },
                { e: '👩', n: 'mulher' }, { e: '🧓', n: 'idoso' },
                { e: '👴', n: 'senhor velho' }, { e: '👵', n: 'senhora velha' },
                { e: '🧔', n: 'barba' }, { e: '👮', n: 'policial' },
                { e: '🕵️', n: 'detetive espiao' }, { e: '💂', n: 'guarda' },
                { e: '👷', n: 'trabalhador obra' }, { e: '🤴', n: 'principe' },
                { e: '👸', n: 'princesa' }, { e: '🧑‍⚕️', n: 'medico saude' },
                { e: '🧑‍🏫', n: 'professor' }, { e: '🧑‍⚖️', n: 'juiz' },
                { e: '🧑‍🌾', n: 'agricultor' }, { e: '🧑‍🍳', n: 'cozinheiro chef' },
                { e: '🧑‍🔧', n: 'mecanico' }, { e: '🧑‍💻', n: 'programador computador' },
                { e: '🧑‍🚀', n: 'astronauta' }, { e: '🧑‍🚒', n: 'bombeiro' },
                { e: '🦸', n: 'super heroi' }, { e: '🦹', n: 'vilao' },
                { e: '🧙', n: 'mago bruxo' }, { e: '🧚', n: 'fada' },
                { e: '🧛', n: 'vampiro' }, { e: '🧟', n: 'zumbi' },
                { e: '💃', n: 'dancando mulher' }, { e: '🕺', n: 'dancando homem' },
                { e: '👯', n: 'orelhas de coelho dupla' }, { e: '🚶', n: 'andando' },
                { e: '🏃', n: 'correndo' }, { e: '🧍', n: 'de pe' },
                { e: '🧎', n: 'de joelhos' }, { e: '💁', n: 'informacao balcao' },
                { e: '🙋', n: 'mao levantada pergunta' }, { e: '🙆', n: 'gesto de ok' },
                { e: '🙅', n: 'gesto de nao' }, { e: '🤦', n: 'palma na testa' },
                { e: '🤷', n: 'ombros encolhidos sei la' }, { e: '💆', n: 'massagem' },
                { e: '💇', n: 'corte de cabelo' }, { e: '👪', n: 'familia' },
                { e: '🧑‍🤝‍🧑', n: 'pessoas de maos dadas' }, { e: '💏', n: 'casal se beijando' },
                { e: '💑', n: 'casal apaixonado' }
            ]
        },
        {
            id: 'animais', rotulo: 'Animais', icone: '🐶', emojis: [
                { e: '🐶', n: 'cachorro' }, { e: '🐕', n: 'cao' },
                { e: '🦮', n: 'cao guia' }, { e: '🐩', n: 'poodle' },
                { e: '🐺', n: 'lobo' }, { e: '🦊', n: 'raposa' },
                { e: '🦝', n: 'guaxinim' }, { e: '🐱', n: 'gato' },
                { e: '🐈', n: 'gato andando' }, { e: '🐈‍⬛', n: 'gato preto' },
                { e: '🦁', n: 'leao' }, { e: '🐯', n: 'tigre' },
                { e: '🐴', n: 'cavalo' }, { e: '🦄', n: 'unicornio' },
                { e: '🦓', n: 'zebra' }, { e: '🦌', n: 'veado' },
                { e: '🐮', n: 'vaca' }, { e: '🐷', n: 'porco' },
                { e: '🐗', n: 'javali' }, { e: '🐑', n: 'ovelha' },
                { e: '🐐', n: 'cabra gato mestre' }, { e: '🐪', n: 'camelo' },
                { e: '🦙', n: 'lhama' }, { e: '🦒', n: 'girafa' },
                { e: '🐘', n: 'elefante' }, { e: '🦏', n: 'rinoceronte' },
                { e: '🐭', n: 'rato' }, { e: '🐹', n: 'hamster' },
                { e: '🐰', n: 'coelho' }, { e: '🐿️', n: 'esquilo' },
                { e: '🦔', n: 'porco espinho' }, { e: '🦇', n: 'morcego' },
                { e: '🐻', n: 'urso' }, { e: '🐨', n: 'coala' },
                { e: '🐼', n: 'panda' }, { e: '🦥', n: 'bicho preguica' },
                { e: '🦦', n: 'lontra' }, { e: '🦘', n: 'canguru' },
                { e: '🐔', n: 'galinha' }, { e: '🐓', n: 'galo' },
                { e: '🐣', n: 'pintinho nascendo' }, { e: '🐤', n: 'pintinho' },
                { e: '🐦', n: 'passaro' }, { e: '🐧', n: 'pinguim' },
                { e: '🕊️', n: 'pomba paz' }, { e: '🦅', n: 'aguia' },
                { e: '🦆', n: 'pato' }, { e: '🦉', n: 'coruja' },
                { e: '🐸', n: 'sapo' }, { e: '🐢', n: 'tartaruga' },
                { e: '🐍', n: 'cobra' }, { e: '🦎', n: 'lagarto' },
                { e: '🐊', n: 'crocodilo' }, { e: '🐬', n: 'golfinho' },
                { e: '🐳', n: 'baleia' }, { e: '🐟', n: 'peixe' },
                { e: '🐠', n: 'peixe tropical' }, { e: '🦈', n: 'tubarao' },
                { e: '🐙', n: 'polvo' }, { e: '🦀', n: 'caranguejo' },
                { e: '🦐', n: 'camarao' }, { e: '🐌', n: 'caracol' },
                { e: '🦋', n: 'borboleta' }, { e: '🐛', n: 'lagarta bug' },
                { e: '🐝', n: 'abelha' }, { e: '🐞', n: 'joaninha' },
                { e: '🕷️', n: 'aranha' }, { e: '🦂', n: 'escorpiao' },
                { e: '🌵', n: 'cacto' }, { e: '🌲', n: 'pinheiro' },
                { e: '🌳', n: 'arvore' }, { e: '🌴', n: 'palmeira coqueiro' },
                { e: '🌱', n: 'muda brotando' }, { e: '🍀', n: 'trevo sorte' },
                { e: '🍁', n: 'folha de outono' }, { e: '🌺', n: 'hibisco flor' },
                { e: '🌻', n: 'girassol' }, { e: '🌹', n: 'rosa' },
                { e: '🌷', n: 'tulipa' }, { e: '💐', n: 'buque de flores' }
            ]
        },
        {
            id: 'comida', rotulo: 'Comida', icone: '🍔', emojis: [
                { e: '🍏', n: 'maca verde' }, { e: '🍎', n: 'maca' },
                { e: '🍐', n: 'pera' }, { e: '🍊', n: 'laranja' },
                { e: '🍋', n: 'limao' }, { e: '🍌', n: 'banana' },
                { e: '🍉', n: 'melancia' }, { e: '🍇', n: 'uva' },
                { e: '🍓', n: 'morango' }, { e: '🫐', n: 'mirtilo' },
                { e: '🍈', n: 'melao' }, { e: '🍒', n: 'cereja' },
                { e: '🍑', n: 'pessego' }, { e: '🥭', n: 'manga' },
                { e: '🍍', n: 'abacaxi' }, { e: '🥥', n: 'coco' },
                { e: '🥝', n: 'kiwi' }, { e: '🍅', n: 'tomate' },
                { e: '🥑', n: 'abacate' }, { e: '🥦', n: 'brocolis' },
                { e: '🥕', n: 'cenoura' }, { e: '🌽', n: 'milho' },
                { e: '🥔', n: 'batata' }, { e: '🍞', n: 'pao' },
                { e: '🥐', n: 'croissant' }, { e: '🧀', n: 'queijo' },
                { e: '🥚', n: 'ovo' }, { e: '🍳', n: 'ovo frito' },
                { e: '🥓', n: 'bacon' }, { e: '🍔', n: 'hamburguer' },
                { e: '🍟', n: 'batata frita' }, { e: '🍕', n: 'pizza' },
                { e: '🌭', n: 'hot dog' }, { e: '🥪', n: 'sanduiche' },
                { e: '🌮', n: 'taco' }, { e: '🌯', n: 'burrito' },
                { e: '🥗', n: 'salada' }, { e: '🍝', n: 'macarrao' },
                { e: '🍜', n: 'lamen sopa' }, { e: '🍲', n: 'panela comida' },
                { e: '🍣', n: 'sushi' }, { e: '🍤', n: 'camarao frito' },
                { e: '🍚', n: 'arroz' }, { e: '🥩', n: 'carne bife' },
                { e: '🍗', n: 'coxa de frango' }, { e: '🍖', n: 'carne osso' },
                { e: '🍿', n: 'pipoca' }, { e: '🧂', n: 'sal' },
                { e: '🍦', n: 'sorvete casquinha' }, { e: '🍩', n: 'donut rosquinha' },
                { e: '🍪', n: 'biscoito cookie' }, { e: '🎂', n: 'bolo de aniversario' },
                { e: '🍰', n: 'fatia de bolo' }, { e: '🧁', n: 'cupcake' },
                { e: '🍫', n: 'chocolate' }, { e: '🍬', n: 'bala doce' },
                { e: '🍭', n: 'pirulito' }, { e: '🍯', n: 'mel' },
                { e: '☕', n: 'cafe' }, { e: '🍵', n: 'cha' },
                { e: '🧉', n: 'chimarrao mate' }, { e: '🥤', n: 'refrigerante copo' },
                { e: '🧊', n: 'gelo' }, { e: '🍺', n: 'cerveja chope' },
                { e: '🍻', n: 'cervejas brinde' }, { e: '🥂', n: 'brinde champanhe' },
                { e: '🍷', n: 'vinho' }, { e: '🥃', n: 'whisky' },
                { e: '🍸', n: 'drink martini' }, { e: '🍹', n: 'drink tropical' },
                { e: '🧃', n: 'suco caixinha' }, { e: '💧', n: 'gota agua' }
            ]
        },
        {
            id: 'esporte', rotulo: 'Esporte', icone: '🎾', emojis: [
                { e: '🎾', n: 'tenis padel bola raquete' }, { e: '🏓', n: 'ping pong tenis de mesa' },
                { e: '🏸', n: 'badminton peteca' }, { e: '⚽', n: 'futebol bola' },
                { e: '🏀', n: 'basquete' }, { e: '🏐', n: 'volei' },
                { e: '🏈', n: 'futebol americano' }, { e: '⚾', n: 'beisebol' },
                { e: '🥎', n: 'softbol' }, { e: '🏑', n: 'hoquei' },
                { e: '🥍', n: 'lacrosse' }, { e: '🏏', n: 'criquete' },
                { e: '⛳', n: 'golfe buraco' }, { e: '🥊', n: 'boxe luva' },
                { e: '🥋', n: 'judo karate kimono' }, { e: '🤺', n: 'esgrima' },
                { e: '🏹', n: 'arco e flecha' }, { e: '🎣', n: 'pescaria' },
                { e: '🤿', n: 'mergulho' }, { e: '🏊', n: 'natacao nadando' },
                { e: '🏄', n: 'surfe' }, { e: '🚣', n: 'remo barco' },
                { e: '🚴', n: 'ciclismo bicicleta' }, { e: '🚵', n: 'mountain bike' },
                { e: '🏇', n: 'corrida de cavalo' }, { e: '⛷️', n: 'esqui' },
                { e: '🏂', n: 'snowboard' }, { e: '⛸️', n: 'patinacao no gelo' },
                { e: '🤸', n: 'ginastica estrela' }, { e: '🤼', n: 'luta livre' },
                { e: '🤾', n: 'handebol' }, { e: '🧘', n: 'ioga meditacao' },
                { e: '🏋️', n: 'levantamento de peso academia' }, { e: '🏆', n: 'trofeu campeao' },
                { e: '🥇', n: 'medalha de ouro primeiro' }, { e: '🥈', n: 'medalha de prata segundo' },
                { e: '🥉', n: 'medalha de bronze terceiro' }, { e: '🏅', n: 'medalha' },
                { e: '🎖️', n: 'medalha militar honra' }, { e: '🎯', n: 'alvo cravou na mosca' },
                { e: '🎽', n: 'camiseta de corrida' }, { e: '🥅', n: 'gol rede' },
                { e: '🎳', n: 'boliche' }, { e: '🎮', n: 'videogame controle' },
                { e: '🎲', n: 'dado sorte' }, { e: '🃏', n: 'coringa carta' },
                { e: '♟️', n: 'xadrez peao' }, { e: '🎪', n: 'circo' },
                { e: '🎤', n: 'microfone cantar' }, { e: '🥁', n: 'bateria tambor' },
                { e: '🎸', n: 'guitarra violao' }, { e: '🎺', n: 'trompete' },
                { e: '🎻', n: 'violino' }, { e: '🎬', n: 'claquete cinema' }
            ]
        },
        {
            id: 'objetos', rotulo: 'Objetos', icone: '💡', emojis: [
                { e: '⌚', n: 'relogio de pulso' }, { e: '📱', n: 'celular telefone' },
                { e: '💻', n: 'notebook computador' }, { e: '🖥️', n: 'monitor desktop' },
                { e: '⌨️', n: 'teclado' }, { e: '🖨️', n: 'impressora' },
                { e: '📷', n: 'camera foto' }, { e: '📹', n: 'filmadora video' },
                { e: '📺', n: 'televisao tv' }, { e: '🎥', n: 'camera de cinema' },
                { e: '📞', n: 'telefone' }, { e: '☎️', n: 'telefone fixo' },
                { e: '📢', n: 'megafone anuncio' }, { e: '🔔', n: 'sino aviso' },
                { e: '🔕', n: 'sino cortado silencioso' }, { e: '🎧', n: 'fone de ouvido' },
                { e: '📻', n: 'radio' }, { e: '🔋', n: 'bateria' },
                { e: '🔌', n: 'tomada plugue' }, { e: '💡', n: 'lampada ideia' },
                { e: '🔦', n: 'lanterna' }, { e: '🕯️', n: 'vela' },
                { e: '🔑', n: 'chave' }, { e: '🔒', n: 'cadeado fechado' },
                { e: '🔓', n: 'cadeado aberto' }, { e: '🔨', n: 'martelo' },
                { e: '🔧', n: 'chave inglesa ferramenta' }, { e: '🔩', n: 'parafuso' },
                { e: '⚙️', n: 'engrenagem configuracao' }, { e: '🧰', n: 'caixa de ferramentas' },
                { e: '🧲', n: 'ima' }, { e: '💉', n: 'injecao vacina' },
                { e: '💊', n: 'remedio pilula' }, { e: '🩹', n: 'band aid curativo' },
                { e: '🚪', n: 'porta' }, { e: '🛏️', n: 'cama' },
                { e: '🚿', n: 'chuveiro' }, { e: '🧹', n: 'vassoura' },
                { e: '🧺', n: 'cesto de roupa' }, { e: '🛒', n: 'carrinho de compras' },
                { e: '🎁', n: 'presente' }, { e: '🎈', n: 'balao festa' },
                { e: '🎉', n: 'festa confete comemorar' }, { e: '🎊', n: 'confete bola' },
                { e: '🎀', n: 'laco fita' }, { e: '💰', n: 'saco de dinheiro' },
                { e: '💵', n: 'dinheiro nota' }, { e: '💳', n: 'cartao de credito' },
                { e: '🧾', n: 'recibo nota fiscal' }, { e: '📅', n: 'calendario data' },
                { e: '📆', n: 'calendario arrancar' }, { e: '📌', n: 'alfinete fixar' },
                { e: '📎', n: 'clipe de papel' }, { e: '✂️', n: 'tesoura' },
                { e: '📝', n: 'anotacao escrever' }, { e: '📖', n: 'livro aberto' },
                { e: '📚', n: 'livros estudar' }, { e: '✉️', n: 'envelope email' },
                { e: '📨', n: 'mensagem recebida' }, { e: '📤', n: 'enviado' },
                { e: '🗑️', n: 'lixeira apagar' }, { e: '🏠', n: 'casa' },
                { e: '🏟️', n: 'estadio arena quadra' }, { e: '🏥', n: 'hospital' },
                { e: '🏦', n: 'banco' }, { e: '🏫', n: 'escola' },
                { e: '⛱️', n: 'guarda sol praia' }, { e: '🚗', n: 'carro' },
                { e: '🚕', n: 'taxi' }, { e: '🚌', n: 'onibus' },
                { e: '🏍️', n: 'moto' }, { e: '✈️', n: 'aviao' },
                { e: '🚁', n: 'helicoptero' }, { e: '🚀', n: 'foguete decolar' },
                { e: '⛵', n: 'veleiro barco' }, { e: '🗺️', n: 'mapa' }
            ]
        },
        {
            id: 'simbolos', rotulo: 'Símbolos', icone: '❤️', emojis: [
                { e: '❤️', n: 'coracao vermelho amor' }, { e: '🧡', n: 'coracao laranja' },
                { e: '💛', n: 'coracao amarelo' }, { e: '💚', n: 'coracao verde' },
                { e: '💙', n: 'coracao azul' }, { e: '💜', n: 'coracao roxo' },
                { e: '🖤', n: 'coracao preto' }, { e: '🤍', n: 'coracao branco' },
                { e: '🤎', n: 'coracao marrom' }, { e: '💔', n: 'coracao partido' },
                { e: '❣️', n: 'coracao exclamacao' }, { e: '💕', n: 'dois coracoes' },
                { e: '💞', n: 'coracoes girando' }, { e: '💓', n: 'coracao batendo' },
                { e: '💗', n: 'coracao crescendo' }, { e: '💖', n: 'coracao brilhando' },
                { e: '💘', n: 'coracao flechado' }, { e: '💝', n: 'coracao presente' },
                { e: '🔥', n: 'fogo pegando fogo' }, { e: '💥', n: 'explosao' },
                { e: '💫', n: 'tontura estrela' }, { e: '⭐', n: 'estrela' },
                { e: '🌟', n: 'estrela brilhante' }, { e: '✨', n: 'brilhos faiscas' },
                { e: '⚡', n: 'raio energia' }, { e: '☀️', n: 'sol' },
                { e: '🌙', n: 'lua' }, { e: '☁️', n: 'nuvem' },
                { e: '🌧️', n: 'chuva' }, { e: '⛈️', n: 'tempestade' },
                { e: '🌈', n: 'arco iris' }, { e: '❄️', n: 'floco de neve frio' },
                { e: '🌊', n: 'onda mar' }, { e: '💨', n: 'vento rapido' },
                { e: '💦', n: 'gotas suor' }, { e: '🕐', n: 'relogio hora' },
                { e: '⏰', n: 'despertador alarme' }, { e: '⏳', n: 'ampulheta tempo' },
                { e: '✅', n: 'certo confirmado' }, { e: '☑️', n: 'marcado caixa' },
                { e: '❌', n: 'errado x' }, { e: '❎', n: 'x verde' },
                { e: '➕', n: 'mais somar' }, { e: '➖', n: 'menos subtrair' },
                { e: '➗', n: 'divisao' }, { e: '✖️', n: 'multiplicacao' },
                { e: '💯', n: 'cem nota maxima' }, { e: '🔝', n: 'top para cima' },
                { e: '🆗', n: 'ok' }, { e: '🆒', n: 'legal cool' },
                { e: '🆕', n: 'novo' }, { e: '🔴', n: 'circulo vermelho' },
                { e: '🟠', n: 'circulo laranja' }, { e: '🟡', n: 'circulo amarelo' },
                { e: '🟢', n: 'circulo verde' }, { e: '🔵', n: 'circulo azul' },
                { e: '🟣', n: 'circulo roxo' }, { e: '⚫', n: 'circulo preto' },
                { e: '⚪', n: 'circulo branco' }, { e: '🚨', n: 'sirene alerta' },
                { e: '⚠️', n: 'aviso atencao perigo' }, { e: '🚫', n: 'proibido' },
                { e: '♻️', n: 'reciclagem' }, { e: '🔄', n: 'atualizar girar' },
                { e: '🔗', n: 'link corrente' }, { e: '❓', n: 'interrogacao duvida' },
                { e: '❗', n: 'exclamacao' }, { e: '💤', n: 'sono zzz' },
                { e: '🎵', n: 'nota musical' }, { e: '🎶', n: 'notas musicais' },
                { e: '🇧🇷', n: 'brasil bandeira' }, { e: '🏁', n: 'bandeira quadriculada fim' },
                { e: '🏳️', n: 'bandeira branca' }, { e: '🚩', n: 'bandeira vermelha' }
            ]
        }
    ];

    // ── AS FREQUENTES ────────────────────────────────────────────────────────────────────────
    // ⚠️ POR APARELHO, no `localStorage`: é preferência de tela, não dado do torneio — mandar
    // isso pro banco criaria escrita a cada toque num emoji pra nada. E TODO acesso é protegido:
    // navegador com dado de site bloqueado (aba privada, política do aparelho) ESTOURA no
    // `localStorage`, e o teclado inteiro morreria por causa de uma lista de conveniência.
    const CHAVE_FREQUENTES = 'pdzEmojiFrequentes';
    const MAX_FREQUENTES = 16;

    function frequentes() {
        try {
            const cru = window.localStorage.getItem(CHAVE_FREQUENTES);
            return cru ? JSON.parse(cru).filter(e => typeof e === 'string') : [];
        } catch (e) { return []; }
    }

    function anotarUso(emoji) {
        try {
            const lista = frequentes().filter(e => e !== emoji);
            lista.unshift(emoji);
            window.localStorage.setItem(CHAVE_FREQUENTES, JSON.stringify(lista.slice(0, MAX_FREQUENTES)));
        } catch (e) { /* sem memória do aparelho, o teclado continua funcionando */ }
    }

    // ── A GRADE ──────────────────────────────────────────────────────────────────────────────
    function escapar(t) {
        return String(t).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function botao(item) {
        return '<button type="button" class="pdz-teclado-emoji" data-emoji="' + escapar(item.e) + '"'
            + ' title="' + escapar(item.n) + '" aria-label="' + escapar(item.n) + '">'
            + escapar(item.e) + '</button>';
    }

    function secao(rotulo, itens) {
        if (itens.length === 0) return '';
        return '<div class="pdz-teclado-secao">'
            + '<div class="pdz-teclado-rotulo">' + escapar(rotulo) + '</div>'
            + '<div class="pdz-teclado-grade">' + itens.map(botao).join('') + '</div>'
            + '</div>';
    }

    // Tudo que o teclado conhece, achatado — é sobre isto que a busca corre.
    const TODOS = CATEGORIAS.reduce((acc, c) => acc.concat(c.emojis), []);

    // ⚠️ Busca SEM ACENTO: quem digita "coracao" tem que achar "coração", e quem digita
    // "coração" também. `normalize('NFD')` separa o acento do caractere e o `replace` o tira.
    function semAcento(t) {
        return String(t).toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
    }

    function procurar(termo) {
        const t = semAcento(termo).trim();
        if (!t) return [];
        return TODOS.filter(i => semAcento(i.n).indexOf(t) >= 0);
    }

    function corpoDasCategorias() {
        const ultimos = frequentes();
        const mapa = new Map(TODOS.map(i => [i.e, i]));
        // As frequentes podem conter emoji que NÃO está na grade (colado no campo): ele aparece
        // com o próprio caractere como nome, em vez de desaparecer da lista de quem o usou.
        const secaoFrequentes = secao('FREQUENTES', ultimos.map(e => mapa.get(e) || { e: e, n: e }));

        return secaoFrequentes + CATEGORIAS.map(c =>
            '<div class="pdz-teclado-secao" id="pdzTecladoCat-' + c.id + '">'
            + '<div class="pdz-teclado-rotulo">' + escapar(c.rotulo.toUpperCase()) + '</div>'
            + '<div class="pdz-teclado-grade">' + c.emojis.map(botao).join('') + '</div>'
            + '</div>').join('');
    }

    function abas() {
        return '<div class="pdz-teclado-abas">' + CATEGORIAS.map(c =>
            '<button type="button" class="pdz-teclado-aba" data-cat="' + c.id + '"'
            + ' title="' + escapar(c.rotulo) + '" aria-label="' + escapar(c.rotulo) + '">'
            + escapar(c.icone) + '</button>').join('') + '</div>';
    }

    // ── A API QUE O `reacoes-do-jogo.js` USA ─────────────────────────────────────────────────
    // Um objeto só, no `window`, porque o arquivo é carregado sob demanda: quem o pede precisa
    // de um nome combinado pra achar o que chegou.
    window.pdzTecladoDeEmoji = {
        // Desenha o teclado dentro de `alvo` e chama `aoEscolher(emoji)` no toque.
        montar: function (alvo, aoEscolher) {
            alvo.innerHTML =
                '<div class="pdz-teclado">'
                + '<input type="search" class="pdz-teclado-busca form-control form-control-sm"'
                + ' placeholder="Pesquisar" aria-label="Pesquisar emoji" autocomplete="off">'
                + '<div class="pdz-teclado-corpo">' + corpoDasCategorias() + '</div>'
                + abas()
                + '</div>';

            const corpo = alvo.querySelector('.pdz-teclado-corpo');
            const busca = alvo.querySelector('.pdz-teclado-busca');

            busca.addEventListener('input', function () {
                if (!busca.value.trim()) { corpo.innerHTML = corpoDasCategorias(); return; }
                const achados = procurar(busca.value);
                corpo.innerHTML = achados.length
                    ? secao('RESULTADOS', achados)
                    : '<div class="pdz-teclado-vazio">Nenhum emoji com esse nome. Dá pra colar qualquer um no campo do painel.</div>';
            });

            // ⚠️ UM OUVINTE NO CORPO, não um por botão: são ~350 botões, e a grade é reescrita a
            // cada letra digitada — religar 350 ouvintes por tecla é como uma busca fica travada.
            corpo.addEventListener('click', function (ev) {
                const b = ev.target.closest('.pdz-teclado-emoji');
                if (!b) return;
                anotarUso(b.dataset.emoji);
                aoEscolher(b.dataset.emoji);
            });

            alvo.querySelector('.pdz-teclado-abas').addEventListener('click', function (ev) {
                const b = ev.target.closest('.pdz-teclado-aba');
                if (!b) return;
                // Tocar numa aba com a busca escrita tem que voltar pras categorias primeiro,
                // senão o rolar procura uma seção que a busca apagou da tela.
                if (busca.value.trim()) { busca.value = ''; corpo.innerHTML = corpoDasCategorias(); }
                const secaoAlvo = corpo.querySelector('#pdzTecladoCat-' + b.dataset.cat);
                if (secaoAlvo) corpo.scrollTop = secaoAlvo.offsetTop - corpo.offsetTop;
            });
        },

        // Os atalhos da barra rápida: as frequentes de quem já usou, e um padrão de padel pra
        // quem nunca abriu o teclado.
        atalhos: function () {
            const ultimos = frequentes();
            if (ultimos.length >= 6) return ultimos.slice(0, 6);
            const padrao = ['🔥', '👏', '😂', '😮', '💪', '🎾'];
            return ultimos.concat(padrao.filter(e => ultimos.indexOf(e) < 0)).slice(0, 6);
        },
    };
})();
