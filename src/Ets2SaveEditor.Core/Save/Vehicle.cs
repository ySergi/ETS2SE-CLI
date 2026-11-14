using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Save;

public sealed class Vehicle(SiiUnit unit, SiiDocument doc)
{
    public SiiUnit Unit { get; } = unit;
    public string Id => Unit.Id;

    public string DataPath => ResolveDataPath(Unit, doc);

    public string ModelName => FormatModelName(DataPath);

    public string LicensePlate
    {
        get => SiiValue.Unquote(Unit.Get("license_plate"));
        set => Unit.Set("license_plate", SiiValue.Quote(value));
    }

    public long Odometer
    {
        get => Unit.GetLong("odometer") ?? 0;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Odometer cannot be negative.");
            Unit.SetLong("odometer", value);
            Unit.TrySet("odometer_float_part", "0");
            Unit.TrySet("integrity_odometer", value.ToString());
            Unit.TrySet("integrity_odometer_float_part", "0");
        }
    }

    public float FuelRelative
    {
        get => Unit.GetFloat("fuel_relative") ?? 1f;
        set
        {
            if (value is < 0f or > 1f)
                throw new ArgumentOutOfRangeException(nameof(value), "Fuel ratio must be between 0.0 and 1.0.");
            Unit.SetFloat("fuel_relative", value);
        }
    }

    public float EngineWear => Unit.GetFloat("engine_wear") ?? 0f;
    public float TransmissionWear => Unit.GetFloat("transmission_wear") ?? 0f;
    public float CabinWear => Unit.GetFloat("cabin_wear") ?? 0f;
    public float ChassisWear => Unit.GetFloat("chassis_wear") ?? 0f;

    public float MaxWheelWear
    {
        get
        {
            var w = Unit.GetArray("wheels_wear");
            return w.Count == 0 ? 0f : w.Max(s => SiiValue.TryParseFloat(s, out var v) ? v : 0f);
        }
    }

    public float MaxWear => Math.Max(
        Math.Max(EngineWear, TransmissionWear),
        Math.Max(CabinWear, Math.Max(ChassisWear, MaxWheelWear)));

    public float MaxUnfixableWear
    {
        get
        {
            float m = Math.Max(
                Math.Max(Unit.GetFloat("engine_wear_unfixable") ?? 0f, Unit.GetFloat("transmission_wear_unfixable") ?? 0f),
                Math.Max(Unit.GetFloat("cabin_wear_unfixable") ?? 0f, Unit.GetFloat("chassis_wear_unfixable") ?? 0f));
            var wu = Unit.GetArray("wheels_wear_unfixable");
            if (wu.Count > 0) m = Math.Max(m, wu.Max(s => SiiValue.TryParseFloat(s, out var v) ? v : 0f));
            return m;
        }
    }

    /// <summary>Zeroes wear across engine, transmission, cabin, chassis, and all wheels (optionally including permanent wear).</summary>
    public void Repair(bool includePermanent = true)
    {
        Unit.TrySet("engine_wear", "0");
        Unit.TrySet("transmission_wear", "0");
        Unit.TrySet("cabin_wear", "0");
        Unit.TrySet("chassis_wear", "0");
        ZeroArray("wheels_wear");

        if (includePermanent)
        {
            Unit.TrySet("engine_wear_unfixable", "0");
            Unit.TrySet("transmission_wear_unfixable", "0");
            Unit.TrySet("cabin_wear_unfixable", "0");
            Unit.TrySet("chassis_wear_unfixable", "0");
            ZeroArray("wheels_wear_unfixable");
        }
    }

    public void Refuel() => FuelRelative = 1f;

    private void ZeroArray(string name)
    {
        if (!Unit.IsArray(name)) return;
        var count = Unit.GetArray(name).Count;
        Unit.SetArray(name, Enumerable.Repeat("0", count).ToList());
    }

    internal static string ResolveDataPath(SiiUnit unit, SiiDocument doc)
    {
        var accIds = unit.GetArray("accessories");
        if (accIds.Count == 0) return "";
        var acc = doc.FindUnit(accIds[0]);
        return acc is null ? "" : SiiValue.Unquote(acc.Get("data_path"));
    }

    internal static string FormatModelName(string dataPath)
    {
        if (string.IsNullOrEmpty(dataPath)) return "(unknown)";
        // "/def/vehicle/truck/scania.s_2016/data.sii" -> "scania.s_2016"
        var parts = dataPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && parts[^1].Equals("data.sii", StringComparison.OrdinalIgnoreCase))
            return parts[^2];
        return dataPath;
    }
}

public sealed class Trailer(SiiUnit unit, SiiDocument doc)
{
    public SiiUnit Unit { get; } = unit;
    public string Id => Unit.Id;
    public string DataPath => Vehicle.ResolveDataPath(Unit, doc);
    public string ModelName => Vehicle.FormatModelName(DataPath);

    public string LicensePlate
    {
        get => SiiValue.Unquote(Unit.Get("license_plate"));
        set => Unit.Set("license_plate", SiiValue.Quote(value));
    }

    public long Odometer
    {
        get => Unit.GetLong("odometer") ?? 0;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Odometer cannot be negative.");
            Unit.SetLong("odometer", value);
            Unit.TrySet("odometer_float_part", "0");
            Unit.TrySet("integrity_odometer", value.ToString());
            Unit.TrySet("integrity_odometer_float_part", "0");
        }
    }

    public float BodyWear => Unit.GetFloat("trailer_body_wear") ?? 0f;
    public float ChassisWear => Unit.GetFloat("chassis_wear") ?? 0f;
    public float CargoDamage
    {
        get => Unit.GetFloat("cargo_damage") ?? 0f;
        set
        {
            if (value is < 0f or > 1f)
                throw new ArgumentOutOfRangeException(nameof(value), "Cargo damage must be between 0.0 and 1.0.");
            Unit.TrySet("cargo_damage", SiiValue.FormatFloat(value));
        }
    }

    public float MaxWear
    {
        get
        {
            var w = Unit.GetArray("wheels_wear");
            float ww = w.Count == 0 ? 0f : w.Max(s => SiiValue.TryParseFloat(s, out var v) ? v : 0f);
            return Math.Max(BodyWear, Math.Max(ChassisWear, ww));
        }
    }

    public void Repair(bool includePermanent = true)
    {
        Unit.TrySet("trailer_body_wear", "0");
        Unit.TrySet("chassis_wear", "0");
        ZeroArray("wheels_wear");
        if (includePermanent)
        {
            Unit.TrySet("trailer_body_wear_unfixable", "0");
            Unit.TrySet("chassis_wear_unfixable", "0");
            ZeroArray("wheels_wear_unfixable");
        }
    }

    private void ZeroArray(string name)
    {
        if (!Unit.IsArray(name)) return;
        var count = Unit.GetArray(name).Count;
        Unit.SetArray(name, Enumerable.Repeat("0", count).ToList());
    }
}
