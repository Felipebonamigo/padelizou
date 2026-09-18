using Padelizou.Models;

namespace Padelizou.Services;

// O TEXTO dos avisos de "alguém te inscreveu", puro, longe do banco e da rede — mesmo padrão do
// TextoDoApito: é texto que precisa estar certo, e texto certo se testa.
public static class TextoDeQuemFoiInscrito
{
    // 🗣️ A frase é do Felipe, letra por letra: *"Você foi inscrito para um torneio por Maickel"*.
    //
    // ⚠️ O NOME VAI NO TÍTULO, e não só no corpo, porque é o título que a notificação do celular
    // mostra inteiro — e "quem foi" é a informação que decide se a pessoa precisa fazer algo.
    // Um "Inscrição confirmada" sozinho deixa quem não clicou achando que foi ela mesma.
    public static string Titulo(string? quemInscreveu) =>
        string.IsNullOrWhiteSpace(quemInscreveu)
            // Nome pode faltar (pré-cadastro, conta anonimizada pela LGPD). Título terminando
            // em "por " é pior do que título sem nome nenhum.
            ? "Você foi inscrito para um torneio"
            : $"Você foi inscrito para um torneio por {quemInscreveu.Trim()}";

    // ⚠️ UMA LINHA SÓ, sem quebra: a caixa de entrada guarda o corpo como texto e a notificação
    // do celular corta o que passa de duas linhas (mesma razão escrita no TextoDoApito).
    public static string Corpo(string torneio, string categoria, bool emListaDeEspera)
    {
        var onde = string.IsNullOrWhiteSpace(categoria) ? torneio : $"{torneio} · {categoria}";

        // "Se não foi combinado, dá pra recusar" é o que transforma o aviso em saída. Sem esta
        // frase ele é um comunicado, e a pessoa que não queria entrar não descobre que pode sair.
        return emListaDeEspera
            ? $"{onde} estava lotado — você entrou na lista de espera. Se não foi combinado, dá pra recusar aqui."
            : $"{onde}. Se não foi combinado, dá pra recusar aqui.";
    }

    // O aviso de quem LEVOU a recusa — é o "e o avisa" do pedido.
    //
    // `ficouSozinho` muda o desfecho inteiro: com parceiro na inscrição, a vaga continua dele e
    // ele tem até o sorteio pra achar outro; sem ninguém, a inscrição acabou e mandá-lo procurar
    // parceiro seria mandá-lo mexer no que não existe mais.
    public static (string Titulo, string Corpo) ParaQuemFicou(string quemRecusou, string torneio,
        bool ficouSozinho)
    {
        var quem = string.IsNullOrWhiteSpace(quemRecusou) ? "Seu parceiro" : quemRecusou.Trim();

        return ficouSozinho
            ? ($"{quem} recusou a inscrição",
               $"{quem} não vai jogar {torneio}. Sua vaga continua sua — escolha outro parceiro "
             + "antes do sorteio das chaves.")
            : ($"{quem} recusou a inscrição",
               $"{quem} não vai jogar {torneio}, e a inscrição foi cancelada.");
    }

    // O aviso de quem INSCREVEU, quando essa pessoa não está na dupla — o organizador que montou
    // a inscrição na secretaria do clube. Sem ele, a recusa só apareceria na hora de montar a
    // chave, que é tarde pra chamar outra pessoa.
    public static (string Titulo, string Corpo) ParaQuemInscreveu(string quemRecusou, string torneio)
    {
        var quem = string.IsNullOrWhiteSpace(quemRecusou) ? "Quem você inscreveu" : quemRecusou.Trim();

        return ($"{quem} recusou a inscrição",
                $"Você inscreveu {quem} em {torneio}, e a inscrição foi recusada.");
    }
}

// A REGRA de quem foi posto numa inscrição por outra pessoa: quem precisa responder, e quem
// pode recusar.
//
// ⚠️ A PERGUNTA É "QUEM NÃO CLICOU", e não "quem é o jogador 2". O formulário deixa escolher os
// DOIS lados pelo nome (Felipe, 14/08/2026), então o autor pode ser o jogador 1, o jogador 2, ou
// nenhum dos dois — e é esse último, o organizador inscrevendo a dupla inteira, o único caso em
// que NINGUÉM dos dois pediu pra entrar. Amarrar a pergunta ao slot deixaria justo ele calado.
public static class InscricaoDeOutraPessoa
{
    public static IReadOnlyList<int> QuemPrecisaResponder(int jogador1Id, int? jogador2Id, int? autorId)
    {
        // Sem autor não há a quem responder nem quem nomear: inscrição de importação, ou
        // cobrança antiga gravada antes desta tabela existir. Nada é perguntado.
        if (autorId == null) return Array.Empty<int>();

        return new[] { jogador1Id, jogador2Id ?? 0 }
            .Where(id => id != 0 && id != autorId.Value)
            .Distinct()
            .ToList();
    }

    public static IEnumerable<InscritoPorOutro> Perguntas(int duplaId, int jogador1Id, int? jogador2Id,
        int? autorId, DateTime agora) =>
        QuemPrecisaResponder(jogador1Id, jogador2Id, autorId)
            .Select(id => new InscritoPorOutro
            {
                DuplaId = duplaId,
                JogadorId = id,
                InscritoPorId = autorId!.Value,
                CriadoEm = agora,
            });

    // AS PERGUNTAS AINDA SEM RESPOSTA de uma pessoa num torneio — o que a faixa da tela do
    // torneio desenha.
    //
    // ⚠️ MORA AQUI, e não solta dentro do controller, pra poder ser COMPILADA CONTRA O NPGSQL
    // num teste (ver TraducaoDasPerguntasDeInscricaoTests). O banco InMemory da suíte não
    // traduz nada: `p.Dupla.Categoria.TorneioId` é navegação de dois níveis — exatamente o tipo
    // de consulta que passa lisa por 7 mil testes verdes e estoura na página mais aberta do
    // site (o que aconteceu em 19/08/2026).
    public static IQueryable<InscritoPorOutro> PerguntasAbertasNoTorneio(
        DbPadelContext contexto, int jogadorId, int torneioId) =>
        contexto.InscritosPorOutro
            .Where(p => p.JogadorId == jogadorId
                        && p.ConfirmadoEm == null
                        && p.Dupla.Categoria.TorneioId == torneioId);

    // Motivo pra não poder recusar, ou null quando pode.
    //
    // ⚠️ AS TRAVAS DE SAÍDA SÃO AS MESMAS DA DESISTÊNCIA, e de propósito a MESMA função: a
    // inscrição é sua? o torneio ainda aceita mexida? não é time? Escritas de novo aqui, uma
    // das duas cópias acabaria deixando sair depois do sorteio — com a dupla já numa chave e
    // adversários contando com ela.
    public static string? MotivoParaNaoRecusar(Dupla? dupla, Torneio? torneio,
        InscritoPorOutro? pergunta, int quemPede)
    {
        if (DesistenciaDeInscricao.MotivoParaNaoDesistir(dupla, torneio, quemPede) is { } motivo)
            return motivo;

        // Sem pergunta minha nesta inscrição, não há o que recusar: ou fui eu quem inscreveu,
        // ou a inscrição é anterior a esta tabela. A saída existe e é outra — o "sair da dupla"
        // da tela do torneio, que PERGUNTA se sai só um ou os dois. Duas portas pro mesmo ato,
        // com perguntas diferentes, é como duas telas divergem.
        if (pergunta == null || pergunta.JogadorId != quemPede)
            return "Esta inscrição não foi feita por outra pessoa — pra sair dela, use o botão "
                 + "de sair da dupla na tela do torneio.";

        return null;
    }
}
