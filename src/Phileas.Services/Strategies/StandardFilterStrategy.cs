using System.Security.Cryptography;
using System.Text;
using Phileas.Filters;
using Phileas.Model.Filtering;
using Phileas.Policy;

namespace Phileas.Services.Strategies;

public abstract class StandardFilterStrategy : AbstractFilterStrategy
{
    public override bool EvaluateCondition(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern)
    {
        if (string.IsNullOrEmpty(Condition)) return true;
        return true;
    }

    protected Replacement GetStandardReplacement(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe, FilterType filterType)
    {
        var salt = Salt ? GenerateSalt() : string.Empty;

        return Strategy switch
        {
            Redact => new Replacement(GetRedactedToken(token, classification, filterType), salt, true),
            StaticReplace => new Replacement(!string.IsNullOrEmpty(StaticReplacement) ? StaticReplacement : GetRedactedToken(token, classification, filterType), salt, true),
            Mask => new Replacement(MaskToken(token), salt, true),
            Last4 => new Replacement(token.Length >= 4 ? token[^4..] : token, salt, true),
            HashSha256Replace => new Replacement(HashSha256(token + salt), salt, true),
            CryptoReplace => crypto != null ? new Replacement(AesEncrypt(token, crypto), salt, true) : new Replacement(GetRedactedToken(token, classification, filterType), salt, true),
            Same => new Replacement(token, salt, false),
            Truncate => new Replacement(token.Length > 0 ? token[..1] : token, salt, true),
            _ => new Replacement(GetRedactedToken(token, classification, filterType), salt, true)
        };
    }

    private string MaskToken(string token)
    {
        if (MaskLength == "same") return new string(MaskCharacter[0], token.Length);
        if (int.TryParse(MaskLength, out int len)) return new string(MaskCharacter[0], Math.Min(len, token.Length));
        return new string(MaskCharacter[0], token.Length);
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static string AesEncrypt(string plaintext, Crypto crypto)
    {
        try
        {
            var key = Convert.FromBase64String(crypto.Key ?? string.Empty);
            var iv = Convert.FromBase64String(crypto.Iv ?? string.Empty);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
            return Convert.ToBase64String(encrypted);
        }
        catch
        {
            return plaintext;
        }
    }
}
