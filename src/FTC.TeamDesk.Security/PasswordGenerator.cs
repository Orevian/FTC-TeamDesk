using System.Security.Cryptography;
using System.Text;

namespace FTC.TeamDesk.Security;

public sealed record PasswordOptions(
    int Length = 20, bool Lower = true, bool Upper = true, bool Digits = true, bool Symbols = true, bool AvoidAmbiguous = true);

public static class PasswordGenerator
{
    private const string LowerSet = "abcdefghijkmnopqrstuvwxyz";
    private const string LowerAmbiguous = "l";
    private const string UpperSet = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string UpperAmbiguous = "IO";
    private const string DigitSet = "23456789";
    private const string DigitAmbiguous = "01";
    private const string SymbolSet = "!@#$%^&*()-_=+[]{};:,.?";

    public static string Generate(PasswordOptions options)
    {
        var sets = new List<string>();
        if (options.Lower) sets.Add(options.AvoidAmbiguous ? LowerSet : LowerSet + LowerAmbiguous);
        if (options.Upper) sets.Add(options.AvoidAmbiguous ? UpperSet : UpperSet + UpperAmbiguous);
        if (options.Digits) sets.Add(options.AvoidAmbiguous ? DigitSet : DigitSet + DigitAmbiguous);
        if (options.Symbols) sets.Add(SymbolSet);
        if (sets.Count == 0) throw new ArgumentException("At least one character class is required.", nameof(options));
        if (options.Length < sets.Count || options.Length > 256)
            throw new ArgumentOutOfRangeException(nameof(options), "Invalid length.");

        var all = string.Concat(sets);
        var chars = new List<char>(options.Length);
        foreach (var s in sets) chars.Add(s[RandomNumberGenerator.GetInt32(s.Length)]); // guarantee each class
        while (chars.Count < options.Length) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        // Fisher-Yates shuffle with a CSPRNG
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }

    /// <summary>Rough entropy estimate in bits (character-class pool size × length). 0-4 score for UI meters.</summary>
    public static (double Bits, int Score) Estimate(string password)
    {
        if (string.IsNullOrEmpty(password)) return (0, 0);
        var pool = 0;
        if (password.Any(char.IsLower)) pool += 26;
        if (password.Any(char.IsUpper)) pool += 26;
        if (password.Any(char.IsDigit)) pool += 10;
        if (password.Any(c => !char.IsLetterOrDigit(c))) pool += 32;
        var bits = password.Length * Math.Log2(Math.Max(pool, 2));
        // Penalise obvious repetition.
        var distinct = password.Distinct().Count();
        if (distinct < password.Length / 2) bits *= 0.6;
        var score = bits switch { < 35 => 0, < 50 => 1, < 65 => 2, < 80 => 3, _ => 4 };
        return (Math.Round(bits, 1), score);
    }

    public static string Describe(PasswordOptions o)
    {
        var sb = new StringBuilder();
        sb.Append(o.Length);
        return sb.ToString();
    }
}
