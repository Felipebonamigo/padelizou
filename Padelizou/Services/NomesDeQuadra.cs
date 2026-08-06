namespace Padelizou.Services;

// Quais nomes de quadra a grade pode usar num torneio.
//
// A grade cria uma vaga por quadra em cada horário — `QuantidadeQuadras` jogos ao mesmo
// tempo. Quem dá NOME a essas vagas é esta lista, e as duas contas precisam fechar: quando
// há menos nome do que vaga, o último jogo do horário nasce com hora e SEM QUADRA (o
// `Encaixar` procura o primeiro nome livre e não acha nenhum). O jogador vê a hora sem o
// lugar, e o organizador nem consegue arrumar na mão — o seletor "mudar de quadra" oferece
// esta mesma lista.
//
// Aconteceu no Interno de 05/08/2026: uma semifinal entrou ao vivo sem quadra enquanto os
// outros quatro jogos tinham a delas.
//
// A regra abaixo é uma escolha entre dois erros, e o de cima é o pior:
//
//   • Faltar nome deixa jogo sem quadra.
//   • Sobrar nome põe DOIS nomes pra mesma quadra na tela — que é o que acontece quando o
//     clube renomeia as quadras depois do sorteio: os jogos guardam "Quadra A".."Quadra E"
//     e o cadastro passa a dizer "Quadra 1".."Quadra 5". Juntar tudo daria dez, e metade
//     mandaria o jogador pro lugar errado.
//
// Por isso o que os JOGOS usam manda, e o cadastro só entra pra completar o que falta até o
// número de quadras do torneio. Renomeação não completa nada (já tem nome pra toda vaga);
// quadra ACRESCENTADA depois do sorteio, sim — que é o caso em que a conta não fechava.
public static class NomesDeQuadra
{
    public static List<string> Disponiveis(
        IReadOnlyList<string> nosJogos, IReadOnlyList<string> cadastradas, int quantidadeQuadras)
    {
        // Nenhum jogo marcado ainda (sorteio): o cadastro é a única fonte que existe.
        if (nosJogos.Count == 0) return cadastradas.ToList();

        var lista = nosJogos.ToList();

        // Completa no FIM: os nomes que os jogos já usam continuam sendo escolhidos
        // primeiro, então uma quadra nova só é usada quando as antigas estão ocupadas.
        foreach (var nome in cadastradas)
        {
            if (lista.Count >= quantidadeQuadras) break;
            if (!lista.Contains(nome)) lista.Add(nome);
        }

        return lista;
    }
}
