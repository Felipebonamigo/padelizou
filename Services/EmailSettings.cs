namespace Padelizou.Services;

public class EmailSettings
{
    public string SmtpHost { get; set; } = null!;
    public int SmtpPort { get; set; }
    public string RemetenteEmail { get; set; } = null!;
    public string RemetenteSenhaApp { get; set; } = null!;
    public string RemetenteNome { get; set; } = null!;
}
