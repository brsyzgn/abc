using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ORBIT.API.Data;

/// <summary>
/// Stores DateTime values as "timestamp without time zone" and returns them with Kind=Unspecified,
/// matching the previous SQL Server datetime2 behavior. Npgsql refuses to write Kind=Utc values to
/// "timestamp without time zone", so values are normalized to UTC wall-clock time with Unspecified kind.
/// </summary>
public sealed class UnspecifiedDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UnspecifiedDateTimeConverter()
        : base(
            value => DateTime.SpecifyKind(
                value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
                DateTimeKind.Unspecified),
            value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified))
    {
    }
}
