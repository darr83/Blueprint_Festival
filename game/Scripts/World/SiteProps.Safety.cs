using Godot;

namespace BlueprintFestival.World;

public static partial class SiteProps
{
    /// <summary>
    /// A run of front-of-stage barrier along +X from the origin: steel panels a little over waist
    /// high, each on a tread plate the crowd stands on so it cannot be pushed over.
    /// </summary>
    public static Node3D CrowdBarrier(float length)
    {
        var b = new MeshBuilder();
        var steel = new Color("#B9C2CA");
        var dark = new Color("#8E99A3");
        const float panel = 1.2f;
        var panels = Mathf.Max(1, Mathf.RoundToInt(length / panel));
        var each = length / panels;
        for (var i = 0; i < panels; i++)
        {
            var mid = (i + 0.5f) * each;
            b.Box(new Vector3(mid, 0.62f, 0), new Vector3(each - 0.04f, 0.86f, 0.05f), steel);
            b.Box(new Vector3(mid, 1.08f, 0), new Vector3(each - 0.02f, 0.08f, 0.09f), dark);
            b.Box(new Vector3(mid, 0.03f, 0.42f), new Vector3(each - 0.04f, 0.05f, 0.84f), dark);
            b.Box(new Vector3(mid - (each / 2) + 0.05f, 0.45f, -0.3f), new Vector3(0.05f, 0.9f, 0.05f), dark, new Vector3(-32, 0, 0));
        }

        return b.BuildNode("CrowdBarrier");
    }
}
