using System.Collections.Generic;
using UnityEngine;

namespace Veilbreak
{
    // All prototype art is procedural placeholder geometry, not imported concept art.
    public sealed class PrimitiveFactory
    {
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<bool, Mesh> meshes = new Dictionary<bool, Mesh>();
        public Material Material(string name, Color color, bool emissive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new System.InvalidOperationException("URP Lit shader missing. Run VEILBREAK > Generate Veil Arena first.");
            var mat = new Material(shader) { name = name, color = color };
            mat.SetFloat("_Smoothness", .25f);
            if (emissive) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 1.4f); }
            materials.Add(mat); return mat;
        }
        public Transform Group(string name, Transform parent, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
        public Transform Shape(string name, Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (solid) go.layer = 8;
            else { var collider = go.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider); }
            return go.transform;
        }
        // Shared eight-sided tapered meshes: armor/stone blocks and pointed crystal shards.
        // Flat normals keep silhouettes faceted without requiring external model assets.
        public Transform Facet(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool pointed = false)
        {
            if (!meshes.TryGetValue(pointed, out Mesh mesh))
            {
                mesh = CreateFacet(pointed); meshes.Add(pointed, mesh);
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }
        static Mesh CreateFacet(bool pointed)
        {
            var ring = new[] { new Vector2(-.32f,-.5f), new Vector2(.32f,-.5f), new Vector2(.5f,-.32f),
                new Vector2(.5f,.32f), new Vector2(.32f,.5f), new Vector2(-.32f,.5f), new Vector2(-.5f,.32f), new Vector2(-.5f,-.32f) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int j = (i + 1) % 8;
                Vector3 a = new Vector3(ring[i].x, -.5f, ring[i].y), b = new Vector3(ring[j].x, -.5f, ring[j].y);
                Vector3 c = pointed ? new Vector3(0, .5f, 0) : new Vector3(ring[j].x * .8f, .5f, ring[j].y * .8f);
                Vector3 d = pointed ? c : new Vector3(ring[i].x * .8f, .5f, ring[i].y * .8f);
                int n = vertices.Count;
                if (pointed) vertices.AddRange(new[] { a, c, b, Vector3.down * .5f, a, b });
                else vertices.AddRange(new[] { a, d, c, a, c, b, Vector3.down * .5f, a, b });
                for (int k = n; k < vertices.Count; k++) triangles.Add(k);
                if (!pointed)
                {
                    n = vertices.Count; vertices.AddRange(new[] { Vector3.up * .5f, c, d });
                    triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2);
                }
            }
            var mesh = new Mesh { name = pointed ? "Shared crystal shard" : "Shared beveled armor stone" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        // Only use for static cosmetic parts sharing one material, never actor joints or reactive groups.
        public void CombineDecor(Transform group, Material material)
        {
            var filters = group.GetComponentsInChildren<MeshFilter>();
            var instances = new CombineInstance[filters.Length];
            for (int i = 0; i < filters.Length; i++) instances[i] = new CombineInstance
            { mesh = filters[i].sharedMesh, transform = group.worldToLocalMatrix * filters[i].transform.localToWorldMatrix };
            var mesh = new Mesh { name = group.name + " combined" }; mesh.CombineMeshes(instances);
            // Additional cache entry ensures each combined mesh is released with this factory.
            combinedMeshes.Add(mesh);
            foreach (var filter in filters) { filter.gameObject.SetActive(false); Object.Destroy(filter.gameObject); }
            group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            group.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        readonly List<Mesh> combinedMeshes = new List<Mesh>();
        public void Dispose()
        {
            foreach (var mat in materials) if (mat) Object.Destroy(mat);
            foreach (var mesh in meshes.Values) if (mesh) Object.Destroy(mesh);
            foreach (var mesh in combinedMeshes) if (mesh) Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); combinedMeshes.Clear();
        }
    }
}
