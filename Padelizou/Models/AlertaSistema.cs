using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Registro de alerta de sistema já disparado (ex: "MEI chegou a 70% do teto em 2026") —
// evita reenviar o mesmo aviso a cada checagem do background service.
[Table("AlertaSistema")]
public class AlertaSistema
{
    public int Id { get; set; }

    // Ex: "MEI70", "MEI90"
    public string Tipo { get; set; } = null!;

    // Ano a que o alerta se refere (o teto do MEI zera todo ano).
    public int Ano { get; set; }

    public DateTime EnviadoEm { get; set; } = DateTime.Now;
}
