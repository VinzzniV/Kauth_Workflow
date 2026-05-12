using System.Security.Cryptography;

namespace AdAutomationWorker.Core.Ad;

// Krypto-stabiler Random-Password-Generator. CSPRNG via RandomNumberGenerator.
// Garantiert mindestens je ein Zeichen aus Upper/Lower/Digit/Symbol — wichtig fuer AD-
// Password-Policy. Symbol-Set ohne `'"\``, weil diese in der `unicodePwd`-Encoding-Pipeline
// (UTF-16-LE in quotes) Sonderbehandlung brauchen wuerden.
internal static class AdPasswordGenerator
{
    private const string UpperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowerChars = "abcdefghijklmnopqrstuvwxyz";
    private const string DigitChars = "0123456789";
    private const string SymbolChars = "!@#$%^&*-_=+";
    private static readonly string AllChars = UpperChars + LowerChars + DigitChars + SymbolChars;

    public static string Generate(int length = 16)
    {
        if (length < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be >= 8 to satisfy AD policy.");
        }

        var chars = new char[length];
        chars[0] = UpperChars[RandomNumberGenerator.GetInt32(UpperChars.Length)];
        chars[1] = LowerChars[RandomNumberGenerator.GetInt32(LowerChars.Length)];
        chars[2] = DigitChars[RandomNumberGenerator.GetInt32(DigitChars.Length)];
        chars[3] = SymbolChars[RandomNumberGenerator.GetInt32(SymbolChars.Length)];

        for (var i = 4; i < length; i++)
        {
            chars[i] = AllChars[RandomNumberGenerator.GetInt32(AllChars.Length)];
        }

        ShuffleInPlace(chars);
        return new string(chars);
    }

    private static void ShuffleInPlace(char[] chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
