using Agendio.Infrastructure.Notifications;

namespace Agendio.UnitTests.Infrastructure;

public class StandardEmailLayoutTests
{
    [Fact]
    public void Render_Should_Wrap_Content_In_The_Standard_Responsive_Layout()
    {
        var html = StandardEmailLayout.Render("Seu código de acesso", "<p>Olá!</p><p><a href=\"https://example.com\">Continuar</a></p>");

        html.ShouldContain("<!doctype html>");
        html.ShouldContain("agendio<span style=\"color:#6547f5;\">BR</span>");
        html.ShouldContain("class=\"email-card\"");
        html.ShouldContain("@media only screen and (max-width: 620px)");
        html.ShouldContain("<p>Olá!</p>");
        html.ShouldContain("https://example.com");
    }

    [Fact]
    public void Render_Should_Encode_The_Subject()
    {
        var html = StandardEmailLayout.Render("Aviso <importante>", "<p>Conteúdo</p>");

        html.ShouldContain("Aviso &lt;importante&gt;");
        html.ShouldNotContain("<h1 class=\"email-title\" style=\"margin:0 0 24px; color:#171721; font-size:29px; line-height:36px; font-weight:750; letter-spacing:-0.6px;\">Aviso <importante></h1>");
    }

    [Fact]
    public void ToPlainText_Should_Remove_Markup_And_Preserve_Readable_Breaks()
    {
        var text = StandardEmailLayout.ToPlainText(
            "Confirmação",
            "<p>Olá, <strong>Maria</strong>!</p><p><a href=\"https://example.com/confirmar\">Acesse &amp; confirme</a>.</p>");

        text.ShouldContain("Confirmação");
        text.ShouldContain("Olá, Maria!");
        text.ShouldContain("Acesse & confirme (https://example.com/confirmar).");
        text.ShouldNotContain("<p>");
        text.ShouldEndWith("— AgendioBR");
    }
}
