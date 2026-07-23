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

    public string? Login { get; set; }

    public string? Codigo { get; set; }

    public virtual ICollection<Dupla> DuplaJogador1s { get; set; } = new List<Dupla>();

    public virtual ICollection<Dupla> DuplaJogador2s { get; set; } = new List<Dupla>();
    public string? Celular { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Email { get; set; }
    public string? SenhaHash { get; set; }
    public string? FotoPerfil { get; set; }
    public string? Instagram { get; set; }
    public int PontuacaoGlobal { get; set; }
    public bool IsProfessor { get; set; } // <- A nova Flag!

    // "Esquerda" / "Direita" / "Ambos"
    public string? LadoQuadra { get; set; }

    // "Destro" / "Canhoto"
    public string? Lateralidade { get; set; }

    // Se true, visitantes só veem foto e nome no perfil público (Jogadores/Perfil)
    public bool PerfilPrivado { get; set; }
    public bool NotificarEmail { get; set; }
    public bool NotificarWhatsApp { get; set; }

    // Controla só o convite avulso pra jogo de grupo (Grupos.Convidar) — separado de
    // NotificarWhatsApp, que é o canal geral de aviso (ex: aula).
    public bool AceitaConvitesJogo { get; set; } = true;

    public virtual ICollection<JogadorCategoria> JogadorCategorias { get; set; } = new List<JogadorCategoria>();
    public virtual ICollection<JogadorClube> JogadorClubes { get; set; } = new List<JogadorClube>();
    public virtual ICollection<JogadorDiaHorario> JogadorDiasHorarios { get; set; } = new List<JogadorDiaHorario>();

    // Quais categorias aparecem na Minha Agenda unificada
    public bool AgendaMostrarJogosSemanais { get; set; } = true;
    public bool AgendaMostrarTorneios { get; set; } = true;
    public bool AgendaMostrarAulas { get; set; } = true;
    public bool AgendaMostrarAlunos { get; set; } = true;

    // Token opaco usado na URL do feed .ics de assinatura de agenda (sem exigir login)
    public Guid AgendaFeedToken { get; set; } = Guid.NewGuid();

    // Administrador raiz: só o dono do app (definido 1x direto no banco, nunca por tela).
    // Só quem tem essa flag pode gerenciar a lista de IsAdminGeral.
    public bool IsAdminRaiz { get; set; }

    // Administrador do sistema nomeado pelo raiz (por CPF/login) — hoje só gerencia donos de
    // clube, mas é a fundação pra outras telas administrativas futuras.
    public bool IsAdminGeral { get; set; }

    // Preferências de notificação por tipo de aviso (independentes do canal NotificarEmail/
    // NotificarWhatsApp, que definem COMO recebe; estas definem O QUE recebe).
    public bool NotificarTorneiosAbertos { get; set; } = true;
    public bool NotificarSeguidosTorneio { get; set; } = true;

    // Gate do Avisos ("Buscar Jogo") existente — antes só dependia de NotificarEmail/WhatsApp.
    public bool NotificarAvisoJogo { get; set; } = true;
    public bool NotificarJogoAula { get; set; } = true;
    public bool NotificarRaqueteLivre { get; set; } = true;

    // Lista de aulas onde ele é o PROFESSOR
    [InverseProperty("Professor")]
    public virtual ICollection<Aula> AulasDadas { get; set; } = new List<Aula>();

    // Lista de aulas onde ele é o ALUNO
    [InverseProperty("Aluno")]
    public virtual ICollection<Aula> AulasRecebidas { get; set; } = new List<Aula>();
    public int? TimeId { get; set; } // O "?" permite que ele fique sem time (null)
    public virtual Time? Time { get; set; }
}
