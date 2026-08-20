namespace Padelizou.Services;

// Quem pode ENTRAR neste ambiente. É o par do EntregaSettings: aquele diz pra quem o sistema
// pode escrever, este diz quem o sistema deixa entrar.
//
// Existe porque o dev roda com uma CÓPIA dos dados de produção — e cópia de produção traz
// junto a senha de todo mundo. O portão de acesso antecipado não segura isso, e não é falha
// dele: `/Auth/Login` é caminho liberado ali de propósito, porque quem tem conta não deveria
// precisar decorar uma senha compartilhada. Só que num ambiente com o banco copiado isso
// significa que qualquer uma das contas reais entra no ensaio com a senha do dia a dia — e
// vê o próprio nome inscrito num torneio que não vai acontecer.
//
// ⚠️ VAZIO = QUALQUER CONTA ENTRA, e é o padrão de propósito, pelo mesmo motivo do
// EntregaSettings: é o comportamento da produção, e um ambiente que sobe sem a chave não pode
// trancar todo mundo do lado de fora do próprio site.
//
// ⚠️ E É POR ISSO QUE A TRAVA MORA AQUI, E NÃO NUM UPDATE NO BANCO. Apagar a senha dos outros
// no banco do dev funciona — até a próxima cópia da produção, que é exatamente como o dev é
// mantido em dia. A rotina que existe pra atualizar o ambiente desfaria a trava sem que
// ninguém percebesse, e o buraco reabriria calado.
//
// Formato no systemd (aceita e-mail, login OU CPF — o que estiver à mão):
//   Entrada__SoEstas__0=felipe.bonamigo@gmail.com
//   Entrada__SoEstas__1=Foka
public class EntradaSettings
{
    public string[] SoEstas { get; set; } = [];
}
