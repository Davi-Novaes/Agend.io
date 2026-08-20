namespace Agendio.Infrastructure.Notifications;

/// <summary>
/// So pra log (docs/BACKLOG.md BL-25) — nao e criptografia nem substituto pra
/// coluna criptografada de dado sensivel de verdade (CPF, saude).
/// </summary>
public static class PiiMasking
{
    public static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[atIndex..]}";
    }

    public static string MaskPhone(string phone)
    {
        return phone.Length <= 4 ? "***" : $"***{phone[^4..]}";
    }
}
