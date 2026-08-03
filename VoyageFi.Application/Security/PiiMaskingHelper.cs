using System.Text.RegularExpressions;

namespace VoyageFi.Application.Security;

public static class PiiMaskingHelper
{
    private static readonly Regex EmailRegex = new(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);
    // Matches Turkish phone numbers e.g. 05551234567, +90555 123 45 67, 555-123-45-67
    private static readonly Regex PhoneRegex = new(@"(?:\+90|0)?5\d{2}[-\s]?\d{3}[-\s]?\d{2}[-\s]?\d{2}", RegexOptions.Compiled);
    // Matches 11 digit TC Identity Number
    private static readonly Regex TcIdentityRegex = new(@"\b[1-9][0-9]{10}\b", RegexOptions.Compiled);
    // Matches 13-16 digit credit card numbers
    private static readonly Regex CreditCardRegex = new(@"\b(?:\d[ -]*?){13,16}\b", RegexOptions.Compiled);

    public static string MaskPii(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = input;
        
        result = EmailRegex.Replace(result, "[MASKED_EMAIL]");
        result = PhoneRegex.Replace(result, "[MASKED_PHONE]");
        result = TcIdentityRegex.Replace(result, "[MASKED_TC]");
        result = CreditCardRegex.Replace(result, "[MASKED_CC]");

        return result;
    }
}
