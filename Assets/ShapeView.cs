using UnityEngine;

/// <summary>
/// Caches the built-in primitive meshes and a pipeline-correct template material,
/// so no shader lookup (Shader.Find) is needed. This keeps it safe for Quest builds.
/// </summary>
public static class ShapeLibrary
{
    static Mesh[] meshes;
    static Material template;

    static void Init()
    {
        if (meshes != null && meshes[0] != null && template != null) return;

        meshes = new Mesh[3];
        PrimitiveType[] types = { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder };

        for (int i = 0; i < types.Length; i++)
        {
            GameObject go = GameObject.CreatePrimitive(types[i]);
            meshes[i] = go.GetComponent<MeshFilter>().sharedMesh;
            if (i == 0) template = go.GetComponent<MeshRenderer>().sharedMaterial;
            Object.Destroy(go);
        }
    }

    public static Mesh GetMesh(ShapeType shape)
    {
        Init();
        return meshes[(int)shape];
    }

    public static Material CreateMaterialInstance()
    {
        Init();
        return new Material(template);
    }
}

/// <summary>
/// Displays an ObjectSpec as a 3D object and smoothly animates scale and rotation.
/// </summary>
public class ShapeView : MonoBehaviour
{
    const float SmoothSpeed = 12f;

    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Material material;

    Vector3 desiredScale = Vector3.one * 0.25f;
    Quaternion desiredRotation = Quaternion.identity;

    public static ShapeView Create(string objectName, Vector3 position)
    {
        var go = new GameObject(objectName);
        go.transform.position = position;
        return go.AddComponent<ShapeView>();
    }

    void Awake()
    {
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        material = ShapeLibrary.CreateMaterialInstance();
        meshRenderer.sharedMaterial = material;
    }

    public void Apply(ObjectSpec spec, bool instant = false)
    {
        meshFilter.sharedMesh = ShapeLibrary.GetMesh(spec.shape);
        material.color = spec.UnityColor;
        desiredScale = spec.VisualScale;
        desiredRotation = spec.Rotation;

        if (instant)
        {
            transform.localScale = desiredScale;
            transform.rotation = desiredRotation;
        }
    }

    void Update()
    {
        float t = 1f - Mathf.Exp(-SmoothSpeed * Time.deltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, desiredScale, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
