using System.Net;
using System.Text.RegularExpressions;

namespace Agendio.Infrastructure.Notifications;

/// <summary>
/// Moldura unica para todo e-mail enviado pela plataforma. Usa tabelas e CSS
/// inline porque Gmail, Outlook e clientes moveis nao interpretam CSS moderno
/// de forma consistente.
/// </summary>
public static partial class StandardEmailLayout
{
    public static string Render(string subject, string contentHtml)
    {
        var safeSubject = WebUtility.HtmlEncode(subject);

        return $$"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="light">
              <meta name="supported-color-schemes" content="light">
              <title>{{safeSubject}}</title>
              <style>
                .email-content p { margin:0 0 16px; }
                .email-content p:last-child { margin-bottom:0; }
                .email-content strong { color:#262433; font-weight:700; }
                .email-content ul { margin:0 0 18px; padding-left:22px; }
                .email-content li { margin:0 0 8px; }
                .email-content a { display:inline-block; margin:5px 0 8px; padding:12px 20px; border-radius:10px; background:#6547f5; color:#ffffff !important; font-weight:700; text-decoration:none; }
                @media only screen and (max-width: 620px) {
                  .email-shell { width: 100% !important; }
                  .email-card { padding: 28px 22px !important; }
                  .email-title { font-size: 25px !important; line-height: 32px !important; }
                }
              </style>
            </head>
            <body style="margin:0; padding:0; background:#f5f5fa; color:#20202a; font-family:Inter,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif; -webkit-font-smoothing:antialiased;">
              <div style="display:none; max-height:0; overflow:hidden; opacity:0; color:transparent;">{{safeSubject}}&#847; &#847; &#847; &#847;</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%; background:#f5f5fa;">
                <tr>
                  <td align="center" style="padding:36px 16px;">
                    <table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0" class="email-shell" style="width:600px; max-width:600px;">
                      <tr>
                        <td style="padding:0 4px 20px;">
                          <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                            <tr>
                              <td width="42" height="42" align="center" valign="middle" style="width:42px; height:42px; border-radius:12px; background:#6547f5; color:#ffffff; font-size:22px; font-weight:800; line-height:42px;">A</td>
                              <td style="padding-left:12px;">
                                <div style="font-size:20px; line-height:24px; font-weight:800; letter-spacing:-0.4px; color:#171721;">agendio<span style="color:#6547f5;">BR</span></div>
                                <div style="font-size:11px; line-height:15px; color:#77778a;">Seu negócio organizado</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td class="email-card" style="background:#ffffff; border:1px solid #e5e3ef; border-top:4px solid #6547f5; border-radius:18px; padding:38px 42px; box-shadow:0 12px 32px rgba(35,29,74,0.08);">
                          <h1 class="email-title" style="margin:0 0 24px; color:#171721; font-size:29px; line-height:36px; font-weight:750; letter-spacing:-0.6px;">{{safeSubject}}</h1>
                          <div class="email-content" style="color:#4e4e60; font-size:16px; line-height:26px;">
                            {{contentHtml}}
                          </div>
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:24px 20px 0; color:#858596; font-size:12px; line-height:18px;">
                          <p style="margin:0 0 6px;">Este e-mail foi enviado automaticamente pelo AgendioBR.</p>
                          <p style="margin:0;">Não responda a esta mensagem.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string ToPlainText(string subject, string contentHtml)
    {
        var withLinks = AnchorRegex().Replace(contentHtml, match => $"{match.Groups["text"].Value} ({match.Groups["url"].Value})");
        var withLineBreaks = BlockBreakRegex().Replace(withLinks, Environment.NewLine);
        var withoutTags = HtmlTagRegex().Replace(withLineBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var normalized = RepeatedBlankLineRegex().Replace(decoded, $"{Environment.NewLine}{Environment.NewLine}").Trim();

        return $"{subject}{Environment.NewLine}{Environment.NewLine}{normalized}{Environment.NewLine}{Environment.NewLine}— AgendioBR";
    }

    [GeneratedRegex(@"<(?:br\s*/?|/p|/div|/li|/h[1-6])>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex("<a\\s+[^>]*href\\s*=\\s*[\"'](?<url>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"(?:\r?\n\s*){3,}")]
    private static partial Regex RepeatedBlankLineRegex();
}
