using System.Collections.Generic;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Joins many simple coloured shapes into one mesh, so a whole person is a handful of draw calls
/// instead of dozens. Each shape is a unit ball, tube, cone or box, stretched and moved into place.
/// </summary>
public sealed class MeshBuilder
{
    private static readonly Dictionary<string, (Vector3[] Vertices, Vector3[] Normals, int[] Indices)> Shapes = new();
    private static StandardMaterial3D? _material;

    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<Color> _colours = new();
    private readonly List<int> _indices = new();

    /// <summary>The one material every built mesh shares: the colour comes from the shapes themselves.</summary>
    public static StandardMaterial3D Material => _material ??= new StandardMaterial3D
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = 0.85f,
        MetallicSpecular = 0.15f,
    };

    public bool IsEmpty => _vertices.Count == 0;

    private static (Vector3[] Vertices, Vector3[] Normals, int[] Indices) Shape(string name)
    {
        if (Shapes.TryGetValue(name, out var cached))
        {
            return cached;
        }

        PrimitiveMesh mesh = name switch
        {
            "ball" => new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 14, Rings = 7 },
            "tube" => new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 10, Rings = 0 },
            "cone" => new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 0 },
            "pill" => new CapsuleMesh { Radius = 0.5f, Height = 1.6f, RadialSegments = 12, Rings = 4 },
            _ => new BoxMesh { Size = Vector3.One },
        };
        var arrays = mesh.GetMeshArrays();
        var shape = (
            arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(),
            arrays[(int)Mesh.ArrayType.Normal].AsVector3Array(),
            arrays[(int)Mesh.ArrayType.Index].AsInt32Array());
        Shapes[name] = shape;
        return shape;
    }

    private void Add(string name, Transform3D transform, Color colour)
    {
        var (vertices, normals, indices) = Shape(name);
        var start = _vertices.Count;
        var normalBasis = transform.Basis.Inverse().Transposed();
        for (var i = 0; i < vertices.Length; i++)
        {
            _vertices.Add(transform * vertices[i]);
            _normals.Add((normalBasis * normals[i]).Normalized());
            _colours.Add(colour);
        }

        foreach (var index in indices)
        {
            _indices.Add(start + index);
        }
    }

    private static Transform3D Place(Vector3 centre, Vector3 size, Vector3 turnDegrees = default)
    {
        var basis = Basis.FromEuler(turnDegrees * (Mathf.Pi / 180f)) * Basis.FromScale(size);
        return new Transform3D(basis, centre);
    }

    /// <summary>A ball stretched to a size (width, height, depth).</summary>
    public void Ball(Vector3 centre, Vector3 size, Color colour, Vector3 turnDegrees = default) => Add("ball", Place(centre, size, turnDegrees), colour);

    public void Ball(Vector3 centre, float diameter, Color colour) => Ball(centre, Vector3.One * diameter, colour);

    /// <summary>An upright tube: width, height, depth.</summary>
    public void Tube(Vector3 centre, Vector3 size, Color colour, Vector3 turnDegrees = default) => Add("tube", Place(centre, size, turnDegrees), colour);

    /// <summary>A tube that is narrower at the top (60% of the bottom).</summary>
    public void Cone(Vector3 centre, Vector3 size, Color colour, Vector3 turnDegrees = default) => Add("cone", Place(centre, size, turnDegrees), colour);

    /// <summary>A tube with rounded ends. Its natural shape is 1 wide and 1.6 tall.</summary>
    public void Pill(Vector3 centre, Vector3 size, Color colour, Vector3 turnDegrees = default) =>
        Add("pill", Place(centre, new Vector3(size.X, size.Y / 1.6f, size.Z), turnDegrees), colour);

    public void Box(Vector3 centre, Vector3 size, Color colour, Vector3 turnDegrees = default) => Add("box", Place(centre, size, turnDegrees), colour);

    public ArrayMesh Build()
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _colours.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = _indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, Material);
        return mesh;
    }

    /// <summary>Builds the mesh into a node placed at a pivot (the shapes were added relative to it).</summary>
    public MeshInstance3D BuildNode(string name, Vector3 position = default) =>
        new() { Name = name, Mesh = Build(), Position = position };
}
