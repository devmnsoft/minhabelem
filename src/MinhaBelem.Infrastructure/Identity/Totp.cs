using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MinhaBelem.Infrastructure.Identity;

public sealed record TotpSetup(string SecretBase32, string ManualKey, string OtpAuthUri);

/// <summary>
/// TOTP (RFC 6238, SHA-1, 30 s, 6 dígitos) implementado localmente para não depender de
/// pacote externo. Usado como segundo fator do painel administrativo.
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public const int PeriodSeconds = 30;

    public static string GenerateSecret(int bytes = 20)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Base32Encode(buffer);
    }

    public static string Verify(string secretBase32, string code, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var digits = code.Trim();
        if (digits.Length != Digits || !digits.All(char.IsAsciiDigit)) return string.Empty;

        var secret = Base32Decode(secretBase32);
        if (secret.Length == 0) return string.Empty;

        var counter = (long)(utcNow.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds / PeriodSeconds;
        for (var delta = -1; delta <= 1; delta++)
        {
            var candidate = ComputeCode(secret, counter + delta);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate),
                    Encoding.ASCII.GetBytes(digits.PadLeft(Digits, '0'))))
                return candidate;
        }
        return string.Empty;
    }

    public static string CurrentCode(string secretBase32, DateTime utcNow) =>
        ComputeCode(Base32Decode(secretBase32),
            (long)(utcNow.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds / PeriodSeconds);

    public static TotpSetup BuildSetup(string accountName, string issuer, string secretBase32)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var uri = $"otpauth://totp/{label}?secret={secretBase32}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={PeriodSeconds}";
        return new TotpSetup(secretBase32, FormatSecret(secretBase32), uri);
    }

    private static string FormatSecret(string base32)
    {
        var groups = base32.Chunk(4).Select(c => new string(c));
        return string.Join(' ', groups);
    }

    private static string ComputeCode(byte[] secret, long counter)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, counter);

        var hash = HMACSHA1.HashData(secret, buffer);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);
        return (binary % 1_000_000).ToString("D6");
    }

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 / 5) + 1);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                sb.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0) sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        return sb.ToString();
    }

    public static byte[] Base32Decode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();
        var cleaned = input.Replace(" ", string.Empty, StringComparison.Ordinal)
                           .Replace("-", string.Empty, StringComparison.Ordinal)
                           .TrimEnd('=')
                           .ToUpperInvariant();
        var output = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var ch in cleaned)
        {
            var index = Base32Alphabet.IndexOf(ch);
            if (index < 0) return Array.Empty<byte>();
            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft < 8) continue;
            output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
            bitsLeft -= 8;
        }
        return output.ToArray();
    }
}