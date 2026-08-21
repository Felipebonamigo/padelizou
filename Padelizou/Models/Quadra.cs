using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// ⚠️ O PAR (TorneioId, Nome) É IDENTIDADE desde 21/08/2026, com constraint no banco.
//
// Não era — e ninguém tinha percebido, porque o resto do sistema JÁ tratava o nome como
// identidade: `Partida.NomeQuadra` é uma string, e é por ela que a grade sabe se a quadra está
// ocupada (Services/GradeDeJogos), que o link de transmissão acha a quadra (PartidasController)
// e que o organizador troca um jogo de lugar (Services/TrocaDeQuadra). Com nome repetido, os
// três agiam sobre a quadra errada, em silêncio, porque a string batia.
//
// A régua em C# mora em Services/NomeDeQuadraUnico, nas portas que escrevem nome.
[Table("Quadra")]
public partial class Quadra
{
    public int Id { get; set; }
    public int TorneioId { get; set; }
    public string Nome { get; set; } = null!;

    // Relacionamento
    public virtual Torneio Torneio { get; set; } = null!;
}