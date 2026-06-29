namespace SolarShading.Core.Solar;

/// <summary>
/// Computes the geometric (topocentric, refraction-free) position of the sun.
///
/// Implements the standard NOAA solar-position equations (Meeus / Astronomical
/// Almanac low-precision series). Accuracy is well within ~0.1° for the daytime
/// range that matters for shadow casting — far below the site-survey, project-north
/// and atmospheric uncertainties that dominate architectural solar work (see
/// research Key Finding #2). Geometric (not apparent) altitude is returned on
/// purpose: sunlight travels in straight lines, so shadow geometry uses the
/// unrefracted position.
///
/// The algorithm is isolated behind <see cref="ISolarPositionAlgorithm"/> so a full
/// NREL SPA / Grena implementation can be swapped in later without touching callers.
/// </summary>
public sealed class SolarPosition : ISolarPositionAlgorithm
{
    public static readonly SolarPosition Instance = new();

    public SunVector Compute(DateTimeOffset instant, double latitudeDeg, double longitudeDeg)
    {
        DateTime utc = instant.UtcDateTime;
        double jd = JulianDay(utc);
        double t = (jd - 2451545.0) / 36525.0; // Julian centuries since J2000.0

        double l0 = Norm360(280.46646 + t * (36000.76983 + 0.0003032 * t));     // mean longitude
        double m = 357.52911 + t * (35999.05029 - 0.0001537 * t);               // mean anomaly
        double e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);          // eccentricity

        double mRad = Deg2Rad(m);
        double c = Math.Sin(mRad) * (1.914602 - t * (0.004817 + 0.000014 * t))
                 + Math.Sin(2 * mRad) * (0.019993 - 0.000101 * t)
                 + Math.Sin(3 * mRad) * 0.000289;

        double trueLong = l0 + c;
        double omega = 125.04 - 1934.136 * t;
        double appLong = trueLong - 0.00569 - 0.00478 * Math.Sin(Deg2Rad(omega)); // apparent longitude

        double eps0 = 23.0 + (26.0 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60.0) / 60.0;
        double eps = eps0 + 0.00256 * Math.Cos(Deg2Rad(omega)); // obliquity corrected

        double epsRad = Deg2Rad(eps);
        double appLongRad = Deg2Rad(appLong);

        double declRad = Math.Asin(Math.Sin(epsRad) * Math.Sin(appLongRad));

        // Equation of time (minutes).
        double y = Math.Tan(epsRad / 2.0);
        y *= y;
        double l0Rad = Deg2Rad(l0);
        double eqTime = 4.0 * Rad2Deg(
            y * Math.Sin(2 * l0Rad)
            - 2 * e * Math.Sin(mRad)
            + 4 * e * y * Math.Sin(mRad) * Math.Cos(2 * l0Rad)
            - 0.5 * y * y * Math.Sin(4 * l0Rad)
            - 1.25 * e * e * Math.Sin(2 * mRad));

        // True solar time at the location (working entirely in UTC).
        double utcMinutes = utc.Hour * 60.0 + utc.Minute + utc.Second / 60.0 + utc.Millisecond / 60000.0;
        double trueSolarTime = utcMinutes + eqTime + 4.0 * longitudeDeg; // longitude east positive
        trueSolarTime = ((trueSolarTime % 1440.0) + 1440.0) % 1440.0;

        double hourAngle = trueSolarTime / 4.0 - 180.0; // degrees
        double haRad = Deg2Rad(hourAngle);

        double latRad = Deg2Rad(latitudeDeg);
        double cosZenith = Math.Sin(latRad) * Math.Sin(declRad)
                         + Math.Cos(latRad) * Math.Cos(declRad) * Math.Cos(haRad);
        cosZenith = Math.Clamp(cosZenith, -1.0, 1.0);
        double zenithRad = Math.Acos(cosZenith);
        double altitudeDeg = 90.0 - Rad2Deg(zenithRad);

        double azimuthDeg;
        double sinZenith = Math.Sin(zenithRad);
        if (sinZenith < 1e-9)
        {
            azimuthDeg = 0.0; // sun at zenith — azimuth undefined
        }
        else
        {
            double cosAz = (Math.Sin(latRad) * cosZenith - Math.Sin(declRad))
                         / (Math.Cos(latRad) * sinZenith);
            cosAz = Math.Clamp(cosAz, -1.0, 1.0);
            double az = Rad2Deg(Math.Acos(cosAz));
            azimuthDeg = hourAngle > 0.0 ? Norm360(az + 180.0) : Norm360(540.0 - az);
        }

        return new SunVector(altitudeDeg, azimuthDeg);
    }

    private static double JulianDay(DateTime utc)
    {
        int year = utc.Year;
        int month = utc.Month;
        double day = utc.Day + (utc.Hour + (utc.Minute + utc.Second / 60.0) / 60.0) / 24.0;
        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }
        int a = year / 100;
        int b = 2 - a + a / 4; // Gregorian calendar correction
        return Math.Floor(365.25 * (year + 4716))
             + Math.Floor(30.6001 * (month + 1))
             + day + b - 1524.5;
    }

    private static double Deg2Rad(double d) => d * Math.PI / 180.0;
    private static double Rad2Deg(double r) => r * 180.0 / Math.PI;
    private static double Norm360(double d) => ((d % 360.0) + 360.0) % 360.0;
}
