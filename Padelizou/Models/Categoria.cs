using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Categoria")]
public partial class Categoria
{
    public int Id { get; set; }

    public int TorneioId { get; set; }

    public string Nome { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public virtual ICollection<Dupla> Duplas { get; set; } = new List<Dupla>();

    public virtual ICollection<Partida> Partidas { get; set; } = new List<Partida>();
    public virtual ICollection<GrupoTorneio> GruposTorneio { get; set; } = new List<GrupoTorneio>();

    public virtual Torneio Torneio { get; set; } = null!;

    // Vagas máximas nesta categoria. Null = sem limite. Quem se inscrever depois de
    // atingido vai pra lista de espera (Dupla.EmListaDeEspera).
    public int? LimiteDuplas { get; set; }

    // ---- Categoria de TIMES ----
    // Aqui quem disputa são times, não duplas: o organizador define a estrutura (quantos
    // times, quantos grupos, quantos classificam por grupo) e cadastra os times pelo nome —
    // jogador não se inscreve. Cada time vira uma Dupla com NomeTime preenchido, e daí o
    // motor inteiro (grupos, partidas, classificação, mata-mata, grade de horários) funciona
    // igual ao de duplas. Regras da estrutura em Services/CategoriaDeTimes.
    public bool DeTimes { get; set; }

    // A estrutura prometida pelo organizador. QuantidadeTimes é o alvo (a tela avisa quando
    // faltam times); QuantidadeGrupos manda no sorteio; ClassificadosPorGrupo manda no
    // mata-mata. Nulos nas categorias comuns.
    public int? QuantidadeTimes { get; set; }
    public int? QuantidadeGrupos { get; set; }
    public int? ClassificadosPorGrupo { get; set; }
}
