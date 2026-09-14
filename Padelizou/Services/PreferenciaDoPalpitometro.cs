using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O QUE ESTA PESSOA ESCOLHEU VER DO PALPITÔMETRO (Felipe, 14/09/2026).
//
// Existe como serviço, e não como duas leituras soltas em cada controller, porque são TRÊS
// telas perguntando a mesma coisa — a lista de jogos (Details e Jogos, pelo
// CarregarViewBagJogosAsync), a aba de palpiteiros do torneio e a do ranking geral
// (HubDoRanking). A terceira a ser escrita é a que esqueceria o padrão do visitante.
public static class PreferenciaDoPalpitometro
{
    public record Escolha(bool VerPalpitometro, bool VerQuemPalpitou);

    // ⚠️ SEM JOGADOR, TUDO À MOSTRA — e este padrão é o ponto do arquivo. Visitante deslogado
    // não tem perfil pra escolher, e a página do torneio é PÚBLICA: nascer escondido tiraria o
    // palpitômetro justamente de quem a barra existe pra atrair. Vale também pro id que não
    // acha linha (conta apagada, claim velha).
    public static readonly Escolha Padrao = new(VerPalpitometro: true, VerQuemPalpitou: true);

    public static async Task<Escolha> DeAsync(DbPadelContext contexto, int? jogadorId)
    {
        if (jogadorId is not int id) return Padrao;

        // Projeção enxuta de propósito: são duas colunas `bool` numa tabela larga, e esta
        // consulta entra na página mais visitada do site.
        return await contexto.Jogadores
            .Where(j => j.Id == id)
            .Select(j => new Escolha(j.VerPalpitometro, j.VerQuemPalpitou))
            .FirstOrDefaultAsync() ?? Padrao;
    }
}
