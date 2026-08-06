using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Configuração que o ADMIN muda de dentro do app, e que precisa sobreviver ao restart.
//
// O grosso da configuração do Padelizou mora no systemd (`Environment=`) e é a coisa certa
// pra segredo e pra endereço de serviço: muda com deploy, versionado, fora do banco. O que
// não cabe lá é a chave que se mexe NO MEIO DE UM DIA DE USO, sem acesso ao servidor —
// desligar o portão de acesso antecipado no dia do lançamento é o caso que motivou isto.
//
// Uma tabela chave/valor de propósito, e não uma coluna por opção: cada opção nova viraria
// uma migração, e migração no dia do lançamento é justamente o que a gente quer evitar.
[Table("ConfiguracaoDoSistema")]
public class ConfiguracaoDoSistema
{
    public int Id { get; set; }

    // Índice único: a chave é a identidade da linha, e duas linhas pra mesma chave fariam a
    // leitura depender de qual vem primeiro.
    public string Chave { get; set; } = null!;

    public string Valor { get; set; } = null!;

    public DateTime AtualizadoEm { get; set; } = DateTime.Now;

    // Quem mexeu. Não é auditoria completa — é o suficiente pra saber a quem perguntar
    // "por que o portão está desligado?".
    public int? AtualizadoPorJogadorId { get; set; }
}
