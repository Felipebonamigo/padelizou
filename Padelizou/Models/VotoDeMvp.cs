using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// UM voto pro melhor jogador de UM torneio.
//
// A regra que dá sentido à tabela mora em Services/MvpDoTorneio; aqui só ficam a linha e as
// duas travas que o BANCO precisa garantir sozinho:
//
//   · um voto por pessoa por torneio (índice único) — sem isso, quem descobre que o botão
//     aceita dois cliques elege quem quiser sozinho, e nenhuma checagem em C# resolve corrida
//     de dois POSTs simultâneos;
//   · quem votou e em quem, guardados de verdade — a apuração é uma contagem de linhas, nunca
//     um contador incrementado numa coluna. Contador não dá pra auditar nem pra desfazer.
[Table("VotoDeMvp")]
public class VotoDeMvp
{
    public int Id { get; set; }

    public int TorneioId { get; set; }
    public virtual Torneio Torneio { get; set; } = null!;

    // Quem votou. É sempre alguém que ESTEVE NA CHAVE do torneio — quem confere é o serviço,
    // na hora de aceitar o voto.
    public int VotanteId { get; set; }
    public virtual Jogador Votante { get; set; } = null!;

    // Em quem votou. É sempre um campeão de alguma categoria deste torneio.
    public int CandidatoId { get; set; }
    public virtual Jogador Candidato { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
