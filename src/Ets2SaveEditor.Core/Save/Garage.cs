using Ets2SaveEditor.Core.Sii;

namespace Ets2SaveEditor.Core.Save;

/// <summary>Garage "status" values as stored in game.sii.</summary>
public enum GarageSize
{
    None = 0,
    Small = 2,
    Large = 3,
    Tiny = 6,
}

public static class GarageSizeExtensions
{
    public static int Slots(this GarageSize size) => size switch
    {
        GarageSize.Tiny => 1,
        GarageSize.Small => 3,
        GarageSize.Large => 5,
        _ => 0,
    };

    public static GarageSize FromStatus(long status) =>
        Enum.IsDefined(typeof(GarageSize), (int)status) ? (GarageSize)(int)status : GarageSize.None;

    public static bool TryParse(string s, out GarageSize size)
    {
        size = s.ToLowerInvariant() switch
        {
            "none" or "sell" or "0" => GarageSize.None,
            "tiny" or "1" => GarageSize.Tiny,
            "small" or "3" => GarageSize.Small,
            "large" or "big" or "5" => GarageSize.Large,
            _ => (GarageSize)(-1),
        };
        return (int)size >= 0;
    }
}

public sealed class Garage(SiiUnit unit)
{
    public SiiUnit Unit { get; } = unit;
    public string City => Unit.Id.StartsWith("garage.", StringComparison.Ordinal) ? Unit.Id[7..] : Unit.Id;
    public long Status => Unit.GetLong("status") ?? 0;
    public GarageSize Size => GarageSizeExtensions.FromStatus(Status);
    public bool Owned => Status != 0;
    public List<string> Vehicles => Unit.GetArray("vehicles");
    public List<string> Drivers => Unit.GetArray("drivers");
    public List<string> Trailers => Unit.GetArray("trailers");
    public int UsedVehicleSlots => Vehicles.Count(v => !SiiValue.IsNull(v));
    public int UsedDriverSlots => Drivers.Count(v => !SiiValue.IsNull(v));
    public float Productivity => Unit.GetFloat("productivity") ?? 0;

    /// <summary>Buys, upgrades, downgrades or sells the garage, resizing the slot arrays accordingly.</summary>
    public void SetSize(GarageSize size)
    {
        int slots = size.Slots();
        var vehicles = Vehicles;
        var drivers = Drivers;

        bool Occupied(List<string> list) => list.Skip(slots).Any(v => !SiiValue.IsNull(v));
        if (Occupied(vehicles) || Occupied(drivers))
            throw new InvalidOperationException(
                $"Garage '{City}' has trucks/drivers in slots that would be removed. Move them out in-game first.");
        if (size == GarageSize.None && Trailers.Count > 0)
            throw new InvalidOperationException($"Garage '{City}' still has trailers parked; move them first.");

        Unit.SetArray("vehicles", Resize(vehicles, slots));
        Unit.SetArray("drivers", Resize(drivers, slots));
        Unit.SetLong("status", (int)size);
        if (size == GarageSize.None) Unit.TrySet("productivity", "0");
    }

    private static List<string> Resize(List<string> list, int slots)
    {
        var r = list.Take(slots).ToList();
        while (r.Count < slots) r.Add("null");
        return r;
    }
}
