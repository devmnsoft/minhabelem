using System.Data;
using System.Globalization;
using Dapper;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.MySql;

/// <summary>Mapeia enums persistidos como VARCHAR (legibilidade no banco e migrações mais seguras).</summary>
public sealed class StringEnumTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override T Parse(object value) => ParseCore(value);

    public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value.ToString();

    internal static T ParseCore(object value) => value switch
    {
        null => default,
        string s => Enum.Parse<T>(s, ignoreCase: true),
        byte b => (T)Enum.ToObject(typeof(T), b),
        short s => (T)Enum.ToObject(typeof(T), s),
        int i => (T)Enum.ToObject(typeof(T), i),
        long l => (T)Enum.ToObject(typeof(T), l),
        _ => Enum.Parse<T>(value.ToString()!, ignoreCase: true)
    };
}

/// <summary>Flags são persistidos como INT para preservar a combinação de bits.</summary>
public sealed class FlagsTypeHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override T Parse(object value) => value switch
    {
        null => default,
        int i => (T)Enum.ToObject(typeof(T), i),
        long l => (T)Enum.ToObject(typeof(T), l),
        string s => Enum.Parse<T>(s, ignoreCase: true),
        _ => (T)Enum.ToObject(typeof(T), Convert.ToInt32(value, CultureInfo.InvariantCulture))
    };

    public override void SetValue(IDbDataParameter parameter, T value)
        => parameter.Value = Convert.ToInt32(value, CultureInfo.InvariantCulture);
}

public static class DapperTypes
{
    private static bool _registered;
    private static readonly object Gate = new();

    /// <summary>Idempotente. Chamado no arranque da aplicação e no início dos testes.</summary>
    public static void Register()
    {
        if (_registered) return;
        lock (Gate)
        {
            if (_registered) return;
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<ContentCategory>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<ContentKind>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<ContentStatus>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<VerificationStatus>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<CandidateStatus>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<RunStatus>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<FeedProtocol>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<SubmissionStatus>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<Audience>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<AccessibilityFeature>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<AiProvider>());
            SqlMapper.AddTypeHandler(new StringEnumTypeHandler<AiTask>());
            SqlMapper.AddTypeHandler(new FlagsTypeHandler<ModerationFlag>());
            _registered = true;
        }
    }
}