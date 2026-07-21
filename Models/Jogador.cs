using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Jogador")]
public partial class Jogador
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Cpf { get; set; } = null!;

    public string? Codigo { get; set; }

    public virtual ICollection<Dupla> DuplaJogador1s { get; set; } = new List<Dupla>();

    public virtual ICollection<Dupla> DuplaJogador2s { get; set; } = new List<Dupla>();
    public string? Celular { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Email { get; set; }
    public string? SenhaHash { get; set; }
    public string? FotoPerfil { get; set; }
    public int PontuacaoGlobal { get; set; }
    public bool IsProfessor { get; set; } // <- A nova Flag!

    // "Esquerda" / "Direita" / "Ambos"
    public string? LadoQuadra { get; set; }
    public bool NotificarEmail { get; set; }
    public bool NotificarWhatsApp { get; set; }

    public virtual ICollection<JogadorCategoria> JogadorCategorias { get; set; } = new List<JogadorCategoria>();
    public virtual ICollection<JogadorClube> JogadorClubes { get; set; } = new List<JogadorClube>();
    public virtual ICollection<JogadorDiaHorario> JogadorDiasHorarios { get; set; } = new List<JogadorDiaHorario>();

    // Quais categorias aparecem na Minha Agenda unificada
    public bool AgendaMostrarJogosSemanais { get; set; } = true;
    public bool AgendaMostrarTorneios { get; set; } = true;
    public bool AgendaMostrarAulas { get; set; } = true;
    public bool AgendaMostrarAlunos { get; set; } = true;

    // Lista de aulas onde ele é o PROFESSOR
    [InverseProperty("Professor")]
    public virtual ICollection<Aula> AulasDadas { get; set; } = new List<Aula>();

    // Lista de aulas onde ele é o ALUNO
    [InverseProperty("Aluno")]
    public virtual ICollection<Aula> AulasRecebidas { get; set; } = new List<Aula>();
    public int? TimeId { get; set; } // O "?" permite que ele fique sem time (null)
    public virtual Time? Time { get; set; }
}
