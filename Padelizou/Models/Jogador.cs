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
    // CAMPO MORTO — não é o ranking. Nunca foi alimentado pelo sistema (os valores que
    // existem em produção vieram de SQL manual antigo). Os pontos reais são calculados a
    // partir das fases alcançadas: use IEstatisticasService.ObterPontosPorJogadorAsync ou
    // ObterResumoJogadorAsync. A coluna fica só pra não exigir migração destrutiva.
    [Obsolete("Use IEstatisticasService para pontos reais. Este campo não reflete o ranking.")]
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
    public bool AgendaMostrarMarcacoes { get; set; } = true;

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

    // Default false de propósito (diferente das outras 5 flags acima, que são opt-out) — sem
    // cidade marcada em JogadorCidades não faz sentido notificar "da região".
    public bool NotificarHorarioVagoRegiao { get; set; }
    public virtual ICollection<JogadorCidade> JogadorCidades { get; set; } = new List<JogadorCidade>();

    // Lista de aulas onde ele é o PROFESSOR
    [InverseProperty("Professor")]
    public virtual ICollection<Aula> AulasDadas { get; set; } = new List<Aula>();

    // Lista de aulas onde ele é o ALUNO
    [InverseProperty("Aluno")]
    public virtual ICollection<Aula> AulasRecebidas { get; set; } = new List<Aula>();
    public int? TimeId { get; set; } // O "?" permite que ele fique sem time (null)
    public virtual Time? Time { get; set; }

    // ---- Recebimento pelo Padelizou (opt-in de quem organiza torneio ou dá aula) ----
    // Fica no Jogador porque tanto o dono do torneio (TorneioOrganizador com NivelAcesso
    // "Criador") quanto o professor de um JogoAula são Jogadores.

    // Desligado = cobra a inscrição por fora e o app não gera cobrança nenhuma.
    public bool ReceberPagamentoOnline { get; set; }

    // Wallet da conta Asaas dele — pra onde o split manda a fatia da inscrição. Pode ser
    // conta de pessoa física; não precisa de CNPJ.
    public string? AsaasWalletId { get; set; }

    // Escolha dele sobre quem paga a comissão do Padelizou:
    // "Somada"     -> soma ao valor do jogador, e ele recebe a inscrição cheia.
    // "Descontada" -> sai da fatia dele, e o jogador paga só o valor anunciado.
    // Nulo = usa o padrão de AsaasSettings.
    public string? ModoComissao { get; set; }

    // Nulo = cadastrado antes de 25/07/2026 (quando a coluna nasceu) — usado nas métricas
    // de uso do admin (cadastros por semana).
    public DateTime? CriadoEm { get; set; } = DateTime.Now;
}
