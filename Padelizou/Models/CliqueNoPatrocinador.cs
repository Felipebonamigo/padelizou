using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// UM CLIQUE NO LOGO DE UM PATROCINADOR — o que o patrocínio rende em visita (Felipe,
// 29/09/2026).
//
// ⚠️ O CLIQUE NÃO TOCAVA O SERVIDOR: o logo aponta direto pro site da marca. Não havia número
// escondido pra mostrar; foi preciso um salto nosso (`/ir/patrocinador/{nome}`) pra ele existir.
//
// ⚠️ SEM IDENTIDADE NENHUMA — nem JogadorId, nem IP, nem sessão. É a régua escrita no irmão
// `AcessoAoSite`, e vale igual aqui: a pergunta é de VOLUME ("quantas vezes clicaram"), e volume
// sai inteiro de um carimbo de tempo. Guardar QUEM clicou em anúncio seria compilar perfil de
// interesse comercial — dado que ninguém pediu e que este projeto não coleta.
//
// ⚠️ UMA LINHA POR CLIQUE, e não um contador que incrementa: dá "no mês" além do total, e não
// tem corrida de leitura-e-escrita entre dois cliques no mesmo instante.
[Table("CliqueNoPatrocinador")]
public class CliqueNoPatrocinador
{
    public long Id { get; set; }

    // O NOME do patrocinador, como ele aparece em PatrocinadoresSettings. Texto, e não FK: a
    // lista dos patrocinadores mora em CONFIGURAÇÃO (e no código), não numa tabela — e o
    // histórico tem que sobreviver ao patrocínio que acabou.
    public string Patrocinador { get; set; } = "";

    public DateTime Quando { get; set; } = DateTime.Now;
}
