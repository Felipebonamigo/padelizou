using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Torneio")]
public partial class Torneio
{
    public int Id { get; set; }

    public int? OrganizadorId { get; set; }

    public string Nome { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public virtual ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();

    public virtual Organizador? Organizador { get; set; }
    public DateTime? DataInicio{ get; set; } 
    public bool PermiteImpedimentos { get; set; }
    public bool PermiteImpedimentoSextaNoite { get; set; } = true;
    public bool PermiteImpedimentoSabadoManha { get; set; } = true;
    public bool PermiteImpedimentoSabadoTarde { get; set; } = true;
    public decimal PrecoInscricao { get; set; }
    public string? LocalTorneio { get; set; }
    public string? ImagemCapa { get; set; }
    public int QuantidadeQuadras { get; set; }
    public string Status { get; set; } = "Inscrições Abertas";
    public string Formato { get; set; } = "Padrao"; // "Padrao" ou "Americano"
    public bool FormatoUnico { get; set; }
    public int SetsFaseGrupos { get; set; }
    public int GamesFaseGrupos { get; set; }
    public int SetsFaseMataMata { get; set; }
    public int GamesFaseMataMata { get; set; }
    public int SetsFaseFinal { get; set; }
    public int GamesFaseFinal { get; set; }
    public int ClubeId { get; set; }
    public Clube Clube { get; set; }
    public int TempoPrevistoPartidaMinutos { get; set; } = 50; // Padrão de 50 minutos
    public int TamanhoGrupo { get; set; } = 3;
    public int ClassificadosPorGrupo { get; set; } = 2;

    // OBSOLETO — mantido só para não dropar a coluna em produção (evita janela de erro
    // com o app antigo rodando). A regra agora é controlada por RestricaoCategoria.
    public bool BloquearCategoriaInferior { get; set; }

    // Regra anti-sandbagging configurável pelo organizador. Define o "gatilho" a partir
    // do qual um jogador comprova nível numa categoria e não pode se inscrever em
    // categorias mais fracas. Valores:
    //   "Livre"     -> sem restrição (qualquer um em qualquer categoria)
    //   "SaiuChave" -> comprova ao passar da fase de grupos (Quartas+)
    //   "Semifinal" -> comprova ao chegar à semifinal (Semi+)
    //   "Final"     -> comprova ao chegar à final (Final ou Campeão)
    public string RestricaoCategoria { get; set; } = "Livre";

    // Vagas máximas somando TODAS as categorias do torneio. Null = sem limite.
    // Quem se inscrever depois de atingido vai pra lista de espera (Dupla/InscricaoAmericana.EmListaDeEspera).
    public int? LimiteDuplasTotal { get; set; }

    // Torneio restrito: só quem tiver a ChaveAcesso consegue se inscrever (Dupla ou
    // InscricaoAmericana). Chave gerada automaticamente na criação quando Restrito = true.
    public bool Restrito { get; set; }
    public string? ChaveAcesso { get; set; }
}
