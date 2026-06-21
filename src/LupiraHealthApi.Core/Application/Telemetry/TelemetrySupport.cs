using Npgsql;

namespace LupiraHealthApi.Application.Telemetry;

/// <summary>Small null-aware readers over the raw telemetry result sets (real columns come back as <c>float</c>,
/// smallints as <c>short</c> — normalize them to the DTO shapes).</summary>
internal static class Db
{
    public static double? NDouble(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToDouble(r.GetValue(i));
    public static double Double0(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? 0.0 : Convert.ToDouble(r.GetValue(i));
    public static int? NInt(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    public static short? NShort(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt16(i);
}
