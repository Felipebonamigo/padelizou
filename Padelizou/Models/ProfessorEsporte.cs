using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Esportes que o professor ensina (Padel/Tênis/Beach Tênis — Services/EsporteDaAula), pra
// mostrar no perfil público dele. Mesma forma de ProfessorCidade (chave composta, N:N), mas
// sem tabela catálogo no banco: o esporte é a mesma string de Aula.Esporte, validada contra
// EsporteDaAula.Todos em código — não uma FK pra tabela nova.
[Table("ProfessorEsporte")]
public partial class ProfessorEsporte
{
    public int ProfessorId { get; set; }
    public string Esporte { get; set; } = null!;

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;
}
