namespace Padelizou.Services;

// O endereço público do site, pra quando um link precisa sair de dentro do sistema (mensagem
// de WhatsApp, e-mail de robô). Dentro de uma requisição dá pra perguntar ao HttpContext; num
// serviço de fundo ou numa mensagem que sai pra fora, não há requisição pra perguntar.
//
// O padrão aponta pra produção porque é o caso que não pode errar. O ambiente de teste
// sobrescreve com `Site__Url` no systemd — senão o dev manda gente pro site de verdade.
public class SiteSettings
{
    public string Url { get; set; } = "https://padelizou.com.br";
}
