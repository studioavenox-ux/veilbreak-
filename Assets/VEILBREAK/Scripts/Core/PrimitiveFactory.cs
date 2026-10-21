using System.Collections.Generic;
using UnityEngine;

namespace Veilbreak
{
    // All prototype art is procedural placeholder geometry, not imported concept art.
    public sealed class PrimitiveFactory
    {
        readonly List<Material> materials = new List<Material>();
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
        public void Dispose() { foreach (var mat in materials) if (mat) Object.Destroy(mat); materials.Clear(); }
    }
}
