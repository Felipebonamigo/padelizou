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

    // EM QUE CLUBE ESTA QUADRA FICA (21/08/2026) — o torneio pode acontecer em mais de um.
    //
    // Nulo é o caso da imensa maioria e quer dizer "no clube do torneio" (`Torneio.ClubeId`).
    // Nasce nulo em tudo que já existia, e é por isso que a coluna é ANULÁVEL: um `int` liso
    // nasceria ZERO no banco, que não é clube nenhum — a lição que `Clube.Selecionavel` já
    // pagou uma vez.
    //
    // ⚠️ ESTA COLUNA É A ÚNICA FONTE DA VERDADE sobre as sedes do torneio. Não existe tabela
    // de "sedes": a lista de clubes onde o torneio acontece é o DISTINCT daqui, e é assim de
    // propósito — uma sede declarada à parte e sem quadra nenhuma seria uma sede que não
    // recebe jogo, ou seja, uma segunda verdade sobre "onde é o torneio" esperando pra
    // discordar da primeira. Quem lê isto é Services/SedesDoTorneio.
    //
    // ⚠️ O NOME CONTINUA ÚNICO POR TORNEIO, e não por clube — a constraint
    // UQ_Quadra_Torneio_Nome não mudou. Tinha que ser assim: `Partida.NomeQuadra` é texto solto
    // e é SÓ pelo nome que a grade sabe se a quadra está ocupada. Duas "Quadra 1" em clubes
    // diferentes seriam a MESMA quadra pro motor inteiro, e ele marcaria um jogo em cada clube
    // no mesmo horário achando que tinha dobrado a quadra. Por isso a tela de criação sugere o
    // nome do clube dentro do nome da quadra quando há mais de uma sede.
    public int? ClubeId { get; set; }
    public virtual Clube? Clube { get; set; }

    // Relacionamento
    public virtual Torneio Torneio { get; set; } = null!;
}