namespace Padelizou.Services;

// Liga a semeadura de dados fictícios no startup.
//
// **Desligado por padrão, de propósito.** Produção é lugar de gente de verdade; se isso
// nascesse ligado, toda limpeza da base seria desfeita no próximo restart. Quem liga é o
// ambiente de testes (dev), no systemd.
public class DadosDemoSettings
{
    public bool Habilitado { get; set; }
}
