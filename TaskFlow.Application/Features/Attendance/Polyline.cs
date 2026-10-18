using System.Text;
using TaskFlow.Application.Contracts.Tracking;

namespace TaskFlow.Application.Features.Attendance;

/// <summary>
/// Google's encoded-polyline format at precision 6 — what Valhalla returns, so a raw track and a
/// matched one are stored, sent and drawn the same way.
/// </summary>
public static class Polyline
{
    private const double Factor = 1e6;

    public static string Encode(IEnumerable<GeoPoint> points)
    {
        var builder = new StringBuilder();
        long previousLatitude = 0, previousLongitude = 0;
        foreach (var point in points)
        {
            var latitude = (long)Math.Round(point.Latitude * Factor);
            var longitude = (long)Math.Round(point.Longitude * Factor);
            Write(builder, latitude - previousLatitude);
            Write(builder, longitude - previousLongitude);
            previousLatitude = latitude;
            previousLongitude = longitude;
        }
        return builder.ToString();
    }

    public static List<GeoPoint> Decode(string encoded)
    {
        var points = new List<GeoPoint>();
        int index = 0;
        long latitude = 0, longitude = 0;
        while (index < encoded.Length)
        {
            latitude += Read(encoded, ref index);
            if (index >= encoded.Length) break;
            longitude += Read(encoded, ref index);
            points.Add(new GeoPoint(latitude / Factor, longitude / Factor));
        }
        return points;
    }

    private static void Write(StringBuilder builder, long value)
    {
        var shifted = value < 0 ? ~(value << 1) : value << 1;
        while (shifted >= 0x20)
        {
            builder.Append((char)((0x20 | (shifted & 0x1f)) + 63));
            shifted >>= 5;
        }
        builder.Append((char)(shifted + 63));
    }

    private static long Read(string encoded, ref int index)
    {
        long result = 0;
        var shift = 0;
        int chunk;
        do
        {
            chunk = encoded[index++] - 63;
            result |= (long)(chunk & 0x1f) << shift;
            shift += 5;
        } while (chunk >= 0x20 && index < encoded.Length);
        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
