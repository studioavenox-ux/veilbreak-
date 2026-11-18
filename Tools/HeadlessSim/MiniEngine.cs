// VEILBREAK headless simulation engine (developer tooling, NOT shipped in the game).
//
// Purpose: execute the real gameplay scripts (Assets/VEILBREAK/Scripts) outside Unity so combat,
// shift-safety, navigation and state-machine behaviour can be run and asserted automatically.
//
// Honesty rules for this file:
//   * it is a functional mock, not Unity. Physics is analytic and approximate.
//   * results from here are "simulation-verified", never "verified in Unity" or "verified on device".
//   * it deliberately implements only the API surface the gameplay scripts use. Art/HUD/audio
//     presentation code is excluded from the simulation build because it needs a real renderer.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponent : Attribute { public RequireComponent(Type a) { } public RequireComponent(Type a, Type b) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }

    public enum KeyCode { None = 0, Space = 32, Escape = 27, Q = 113, W = 119, A = 97, S = 115, D = 100 }
    public enum QueryTriggerInteraction { UseGlobal = 0, Ignore = 1, Collide = 2 }
    public enum CursorLockMode { None, Locked, Confined }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public static class Cursor
    {
        public static CursorLockMode lockState;
        public static bool visible;
    }

    public static class Screen
    {
        public static int width = 1920, height = 1080;
        public static ScreenOrientation orientation { get; set; } = ScreenOrientation.LandscapeLeft;
        public static Rect safeArea => new Rect(0, 0, width, height);
    }

    public static class Application
    {
        public static bool isMobilePlatform { get; set; }
        public static bool isPlaying { get; set; } = true;
        public static bool isBatchMode => true;
        public static int targetFrameRate { get; set; }
    }

    public static class Time
    {
        public static float time, deltaTime = 1f / 60f, unscaledTime, unscaledDeltaTime = 1f / 60f, timeScale = 1;
        public static void Step(float dt)
        {
            deltaTime = dt * timeScale;
            unscaledDeltaTime = dt;
            time += deltaTime;
            unscaledTime += dt;
        }
        public static void ResetAll()
        {
            time = unscaledTime = 0; timeScale = 1; deltaTime = unscaledDeltaTime = 1f / 60f;
        }
    }

    public static class Debug
    {
        public static readonly List<string> Lines = new List<string>();
        public static void Log(object message) => Lines.Add("LOG " + message);
        public static void LogWarning(object message) => Lines.Add("WARN " + message);
        public static void LogError(object message) => Lines.Add("ERROR " + message);
        public static void LogException(Exception error, Object context = null) => Lines.Add("EXC " + error.GetType().Name + ": " + error.Message);
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector2(x / m, y / m) : zero; } }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator *(float s, Vector2 a) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.x / s, a.y / s);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new Vector2(a.x / b.x, a.y / b.y);
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object o) => o is Vector2 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static Vector2 Min(Vector2 a, Vector2 b) => new Vector2(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2 ClampMagnitude(Vector2 v, float max) => v.magnitude > max ? v.normalized * max : v;
        public override string ToString() => "(" + x.ToString("0.###") + ", " + y.ToString("0.###") + ")";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 normal)
        {
            float d = Dot(v, normal.normalized);
            return v - normal.normalized * d;
        }
        public static Vector3 ClampMagnitude(Vector3 v, float max) => v.magnitude > max ? v.normalized * max : v;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float maxDelta)
        {
            Vector3 d = b - a; float m = d.magnitude;
            return m <= maxDelta || m < 1e-6f ? b : a + d / m * maxDelta;
        }
        public static float Angle(Vector3 a, Vector3 b)
        {
            float denominator = (float)Math.Sqrt(a.sqrMagnitude * (double)b.sqrMagnitude);
            if (denominator < 1e-15f) return 0f;
            float value = Dot(a.normalized, b.normalized);
            return (float)(Math.Acos(Math.Max(-1.0, Math.Min(1.0, value))) * 57.29578);
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        public override string ToString() => "(" + x.ToString("0.###") + ", " + y.ToString("0.###") + ", " + z.ToString("0.###") + ")";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion Euler(float px, float py, float pz)
        {
            float cx = Mathf.Cos(px * Mathf.Deg2Rad * .5f), sx = Mathf.Sin(px * Mathf.Deg2Rad * .5f);
            float cy = Mathf.Cos(py * Mathf.Deg2Rad * .5f), sy = Mathf.Sin(py * Mathf.Deg2Rad * .5f);
            float cz = Mathf.Cos(pz * Mathf.Deg2Rad * .5f), sz = Mathf.Sin(pz * Mathf.Deg2Rad * .5f);
            return new Quaternion(
                sx * cy * cz + cx * sy * sz,
                cx * sy * cz - sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float num = q.x * 2f, num2 = q.y * 2f, num3 = q.z * 2f;
            float num4 = q.x * num, num5 = q.y * num2, num6 = q.z * num3;
            float num7 = q.x * num2, num8 = q.x * num3, num9 = q.y * num3;
            float num10 = q.w * num, num11 = q.w * num2, num12 = q.w * num3;
            return new Vector3(
                (1f - (num5 + num6)) * v.x + (num7 - num12) * v.y + (num8 + num11) * v.z,
                (num7 + num12) * v.x + (1f - (num4 + num6)) * v.y + (num9 - num10) * v.z,
                (num8 - num11) * v.x + (num9 + num10) * v.y + (1f - (num4 + num5)) * v.z);
        }
        public Quaternion normalized
        {
            get
            {
                float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
                return m < 1e-6f ? identity : new Quaternion(x / m, y / m, z / m, w / m);
            }
        }
        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);
        public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
        {
            Vector3 f = forward.normalized;
            if (f.sqrMagnitude < 1e-6f) return identity;
            Vector3 r = Vector3.Cross(upwards, f).normalized;
            if (r.sqrMagnitude < 1e-6f) r = Vector3.Cross(Vector3.forward, f).normalized;
            Vector3 u = Vector3.Cross(f, r);
            // Columns are the basis vectors: M = [ right | up | forward ].
            float m00 = r.x, m01 = u.x, m02 = f.x;
            float m10 = r.y, m11 = u.y, m12 = f.y;
            float m20 = r.z, m21 = u.z, m22 = f.z;
            float trace = m00 + m11 + m22;
            Quaternion q;
            if (trace > 0f)
            {
                float s = (float)Math.Sqrt(trace + 1f) * 2f;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, .25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quaternion(.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quaternion((m01 + m10) / s, .25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, .25f * s, (m10 - m01) / s);
            }
            return q.normalized;
        }
        public Vector3 eulerAngles
        {
            get
            {
                float sinr = 2f * (w * x + y * z);
                float cosr = 1f - 2f * (x * x + y * y);
                float pitch = Mathf.Atan2(sinr, cosr);
                float sinp = 2f * (w * y - z * x);
                float yaw = (float)Math.Asin(Math.Max(-1f, Math.Min(1f, sinp)));
                float siny = 2f * (w * z + x * y);
                float cosy = 1f - 2f * (y * y + z * z);
                float roll = Mathf.Atan2(siny, cosy);
                return new Vector3(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
            }
        }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            if (dot < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); dot = -dot; }
            if (dot > .9995f)
                return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
            float theta = (float)Math.Acos(dot);
            float sinTheta = (float)Math.Sin(theta);
            float s0 = (float)Math.Sin((1 - t) * theta) / sinTheta, s1 = (float)Math.Sin(t * theta) / sinTheta;
            return new Quaternion(a.x * s0 + b.x * s1, a.y * s0 + b.y * s1, a.z * s0 + b.z * s1, a.w * s0 + b.w * s1).normalized;
        }
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
        {
            float angle = Quaternion.Angle(from, to);
            if (angle < 1e-4f) return to;
            return Slerp(from, to, Mathf.Min(1f, maxDegreesDelta / angle));
        }
        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Math.Abs(a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w);
            return Mathf.Clamp(dot, 0f, 1f) * 2f * Mathf.Rad2Deg * (float)Math.Acos(Math.Min(1.0, dot));
        }
        public Quaternion Inverse()
        {
            float m = x * x + y * y + z * z + w * w;
            return m < 1e-9f ? identity : new Quaternion(-x / m, -y / m, -z / m, w / m);
        }
        public static bool operator ==(Quaternion a, Quaternion b) => Math.Abs(Quaternion.Dot(a, b)) > .99999f;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public override bool Equals(object o) => o is Quaternion q && this == q;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode();
        public override string ToString() => "Quaternion(" + x.ToString("0.###") + ", " + y.ToString("0.###") + ", " + z.ToString("0.###") + ", " + w.ToString("0.###") + ")";
    }

    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;
        public const float Infinity = float.PositiveInfinity;
        public const float Epsilon = 1.401298E-45f;
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Log(float v) => (float)Math.Log(v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float Sign(float v) => v < 0f ? -1f : 1f;
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), 1e-9f);
        public static float MoveTowards(float a, float b, float maxDelta) => Math.Abs(b - a) <= maxDelta ? b : a + Sign(b - a) * maxDelta;
    }

    public class Object
    {
        public string name = string.Empty;
        internal bool destroyed;
        public int GetInstanceID() => base.GetHashCode();
        public static void Destroy(Object target)
        {
            if (target == null) return;
            target.destroyed = true;
            if (target is GameObject go) go.DestroyInternal();
            else if (target is Component component) component.gameObject?.RemoveComponent(component);
        }
        public static void Destroy(Object target, float delay) => Destroy(target);
        public static void DestroyImmediate(Object target) => Destroy(target);
        public static implicit operator bool(Object target) => target != null && !target.destroyed;
        public static bool operator ==(Object a, Object b)
        {
            bool aNull = a is null || a.destroyed, bNull = b is null || b.destroyed;
            if (aNull && bNull) return true;
            if (aNull || bNull) return false;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => other is Object o && this == o;
        public override int GetHashCode() => base.GetHashCode();
        public override string ToString() => name;
    }

    public class Transform : Component
    {
        public Transform() { }
        internal GameObject owner { get => gameObjectField; set => gameObjectField = value; }
        // (owner lives on Component.gameObjectField; Transform inherits it.)
        internal Transform parentField;
        internal readonly List<Transform> children = new List<Transform>();
        internal Vector3 localPositionField, localScaleField = Vector3.one;
        internal Quaternion localRotationField = Quaternion.identity;

        public Transform parent
        {
            get => parentField;
            set => SetParent(value, true);
        }
        public int childCount => children.Count;
        public Transform GetChild(int index) => children[index];
        public void SetParent(Transform value) => SetParent(value, true);
        public void SetParent(Transform value, bool worldPositionStays)
        {
            Vector3 worldPosition = position;
            Quaternion worldRotation = rotation;
            parentField?.children.Remove(this);
            parentField = value;
            parentField?.children.Add(this);
            if (worldPositionStays) { position = worldPosition; rotation = worldRotation; }
        }
        public Vector3 localPosition { get => localPositionField; set => localPositionField = value; }
        public Quaternion localRotation { get => localRotationField; set => localRotationField = value; }
        public Vector3 localScale { get => localScaleField; set => localScaleField = value; }
        public Vector3 position
        {
            get => parentField == null ? localPositionField : parentField.TransformPoint(localPositionField);
            set => localPositionField = parentField == null ? value : parentField.InverseTransformPoint(value);
        }
        public Quaternion rotation
        {
            get => parentField == null ? localRotationField : parentField.rotation * localRotationField;
            set => localRotationField = parentField == null ? value : parentField.rotation.Inverse() * value;
        }
        public Vector3 lossyScale
        {
            get
            {
                if (parentField == null) return localScaleField;
                Vector3 p = parentField.lossyScale;
                return new Vector3(p.x * localScaleField.x, p.y * localScaleField.y, p.z * localScaleField.z);
            }
        }
        public Matrix4x4 localToWorldMatrix => Matrix4x4.TRS(position, rotation, lossyScale);
        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public string tag { get => owner != null ? owner.tag : "Untagged"; set { if (owner != null) owner.tag = value; } }
        public Vector3 TransformPoint(Vector3 point) => position + rotation * ScaledLocal(point);
        public Vector3 TransformDirection(Vector3 direction) => rotation * direction;
        public Vector3 InverseTransformDirection(Vector3 direction) => rotation.Inverse() * direction;
        public Vector3 InverseTransformPoint(Vector3 point) => Unscaled(rotation.Inverse() * (point - position));
        /// <summary>Local (already rotated) vector -> world vector, applying world scale.</summary>
        internal Vector3 ScaledLocal(Vector3 v) { Vector3 s = lossyScale; return new Vector3(v.x * s.x, v.y * s.y, v.z * s.z); }
        internal Vector3 Unscaled(Vector3 v) { Vector3 s = lossyScale; return new Vector3(s.x != 0 ? v.x / s.x : 0, s.y != 0 ? v.y / s.y : 0, s.z != 0 ? v.z / s.z : 0); }
        public void LookAt(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - position;
            if (direction.sqrMagnitude > 1e-8f) rotation = Quaternion.LookRotation(direction);
        }
        public void LookAt(Transform target) { if (target) LookAt(target.position); }
        public void SetPositionAndRotation(Vector3 newPosition, Quaternion newRotation) { position = newPosition; rotation = newRotation; }
        public void Rotate(Vector3 euler) => localRotationField = localRotationField * Quaternion.Euler(euler.x, euler.y, euler.z);
        public void Translate(Vector3 translation) => position += translation;
        public void SetSiblingIndex(int index) { }
    }

    public struct Matrix4x4
    {
        public Vector3 translation, lossyScale;
        public Quaternion rotation;
        public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale) =>
            new Matrix4x4 { translation = position, rotation = rotation, lossyScale = scale };
        public Matrix4x4 inverse => new Matrix4x4
        {
            rotation = rotation.Inverse(),
            lossyScale = new Vector3(lossyScale.x != 0 ? 1f / lossyScale.x : 0, lossyScale.y != 0 ? 1f / lossyScale.y : 0, lossyScale.z != 0 ? 1f / lossyScale.z : 0),
            translation = rotation.Inverse() * new Vector3(-translation.x * (lossyScale.x != 0 ? 1f / lossyScale.x : 0), -translation.y * (lossyScale.y != 0 ? 1f / lossyScale.y : 0), -translation.z * (lossyScale.z != 0 ? 1f / lossyScale.z : 0))
        };
        public Vector3 MultiplyPoint3x4(Vector3 point) => translation + rotation * new Vector3(point.x * lossyScale.x, point.y * lossyScale.y, point.z * lossyScale.z);
    }

    public class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        internal IReadOnlyList<Component> Components => components;
        internal bool activeSelfField = true;
        internal int layerField;
        internal string tagField = "Untagged";
        internal Transform transformField;

        /// <summary>Every live GameObject; the physics layer queries this list.</summary>
        internal static readonly List<GameObject> All = new List<GameObject>();

        public GameObject() : this("GameObject") { }
        public GameObject(string name)
        {
            this.name = name;
            transformField = new Transform { name = name, owner = this };
            components.Add(transformField);
            All.Add(this);
        }
        public GameObject(string name, params Type[] types) : this(name)
        {
            foreach (var type in types)
            {
                // Unity replaces the Transform with a RectTransform when a UI element is created.
                if (typeof(Transform).IsAssignableFrom(type) && type != typeof(Transform))
                {
                    components.Remove(transformField);
                    transformField = (Transform)Activator.CreateInstance(type);
                    transformField.name = name;
                    transformField.owner = this;
                    components.Add(transformField);
                    continue;
                }
                AddComponent(type);
            }
        }
        public Transform transform => transformField;
        public bool activeSelf => activeSelfField;
        public bool activeInHierarchy
        {
            get
            {
                if (destroyed || !activeSelfField) return false;
                Transform current = transformField.parentField;
                while (current != null)
                {
                    if (current.gameObject != null && !current.gameObject.activeSelfField) return false;
                    current = current.parentField;
                }
                return true;
            }
        }
        public int layer { get => layerField; set => layerField = value; }
        public string tag { get => tagField; set => tagField = value; }
        public void SetActive(bool value) { activeSelfField = value; }
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObjectField = this };
            components.Add(component);
            if (component is MonoBehaviour behaviour) behaviour.CacheMessages();
            if (activeInHierarchy) component.InvokeAwake();
            return component;
        }
        public Component AddComponent(Type type)
        {
            var component = (Component)Activator.CreateInstance(type);
            component.gameObjectField = this;
            components.Add(component);
            if (component is MonoBehaviour behaviour) behaviour.CacheMessages();
            if (activeInHierarchy) component.InvokeAwake();
            return component;
        }
        public T GetComponent<T>() where T : class
        {
            foreach (var component in components) if (component is T typed) return typed;
            return null;
        }
        public Component GetComponent(Type type)
        {
            foreach (var component in components) if (type.IsInstanceOfType(component)) return component;
            return null;
        }
        internal void RemoveComponent(Component component) => components.Remove(component);
        internal void DestroyInternal()
        {
            for (int i = components.Count - 1; i >= 0; i--) components[i].destroyed = true;
            components.Clear();
            All.Remove(this);
            transformField.SetParent(null, false);
        }
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var go = new GameObject(type.ToString());
            switch (type)
            {
                case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                case PrimitiveType.Capsule:
                case PrimitiveType.Cylinder: go.AddComponent<CapsuleCollider>(); break;
                default: go.AddComponent<MeshCollider>(); break;
            }
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            return go;
        }
        public static GameObject Find(string name) => null;
    }

    public class Component : Object
    {
        internal GameObject gameObjectField;
        public GameObject gameObject
        {
            get => gameObjectField;
            set { gameObjectField = value; if (value != null) name = value.name; }
        }
        public Transform transform => gameObjectField?.transform;
        public T GetComponent<T>() where T : class => gameObjectField?.GetComponent<T>();
        public T GetComponentInParent<T>() where T : class
        {
            for (Transform current = transform; current != null; current = current.parentField)
            {
                var found = current.gameObject?.GetComponent<T>();
                if (found != null) return found;
            }
            return null;
        }
        public T GetComponentInChildren<T>() where T : class
        {
            var all = GetComponentsInChildren<T>(false);
            return all.Length > 0 ? all[0] : null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class
        {
            var results = new List<T>();
            Collect(transform, includeInactive, results);
            return results.ToArray();
        }
        static void Collect<T>(Transform node, bool includeInactive, List<T> results) where T : class
        {
            if (node?.gameObject == null) return;
            if (!includeInactive && !node.gameObject.activeInHierarchy) return;
            foreach (var component in node.gameObject.components)
                if (component is T typed) results.Add(typed);
            foreach (var child in node.children) Collect(child, includeInactive, results);
        }
        public bool CompareTag(string value) => gameObjectField != null && gameObjectField.tagField == value;
        internal virtual void InvokeAwake() { }
    }

    public class Behaviour : Component
    {
        internal bool enabledField = true;
        public bool enabled { get => enabledField; set => enabledField = value; }
        /// <summary>Matches Unity: an inactive GameObject never reports as active-and-enabled.</summary>
        public bool isActiveAndEnabled => enabledField && gameObjectField != null && gameObjectField.activeInHierarchy;
        internal override void InvokeAwake()
        {
            if (this is MonoBehaviour behaviour) behaviour.awakeAction?.Invoke();
        }
    }

    public class MonoBehaviour : Behaviour
    {
        // Cached private-method invokers so the simulation can drive Unity's implicit Update loop.
        internal Action awakeAction, updateAction, lateUpdateAction;
        internal void CacheMessages()
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            awakeAction = Bind("Awake", Flags);
            updateAction = Bind("Update", Flags);
            lateUpdateAction = Bind("LateUpdate", Flags);
        }
        Action Bind(string message, BindingFlags flags)
        {
            var method = GetType().GetMethod(message, flags);
            if (method == null) return null;
            // A Delegate to a value-type-target instance method is not valid; wrap instead.
            return (Action)Delegate.CreateDelegate(typeof(Action), this, method);
        }
        /// <summary>True when the type declares this Unity message in its own hierarchy.</summary>
        internal bool Declares(string message)
        {
            for (var type = GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                if (type.GetMethod(message, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null)
                    return true;
            return false;
        }
    }

    public class Collider : Component
    {
        internal bool enabledField = true;
        public bool enabled { get => enabledField; set => enabledField = value; }
        public bool isTrigger { get; set; }
        internal virtual bool Active => enabledField && gameObjectField != null && gameObjectField.activeInHierarchy;
    }
    public class BoxCollider : Collider
    {
        public Vector3 center { get; set; }
        public Vector3 size { get; set; } = Vector3.one;
    }
    public class SphereCollider : Collider
    {
        public Vector3 center { get; set; }
        public float radius { get; set; } = .5f;
    }
    public class CapsuleCollider : Collider
    {
        public Vector3 center { get; set; }
        public float radius { get; set; } = .5f;
        public float height { get; set; } = 2f;
        public int direction { get; set; } = 1;
    }
    public class MeshCollider : Collider { }

    public class CharacterController : Collider
    {
        public float height { get; set; } = 1.8f;
        public float radius { get; set; } = .5f;
        public Vector3 center { get; set; } = Vector3.up * .9f;
        public float stepOffset { get; set; } = .3f;
        public float skinWidth { get; set; } = .08f;
        public bool isGrounded { get; internal set; }
        public CollisionFlags collisionFlags { get; internal set; }
        public bool detectCollisions { get; set; } = true;

        public void Move(Vector3 motion)
        {
            if (!enabledField) return;
            Vector3 start = transform.position;
            Vector3 target = start + motion;
            Transform ignored = transform;
            // Grounded == the capsule would collide if lowered by skin width (a PhysX contact test).
            bool grounded = Physics.Blocked(target + center - Vector3.up * (skinWidth + .01f), radius * .98f, height, ignored);
            isGrounded = grounded && motion.y <= 0f;
            // A climb raises the capsule, so the vertical resolve starts from the lifted height and can
            // settle back down onto the ledge; without a climb the requested y is honoured as-is.
            float stepUp = Physics.stepUpHeight;
            float baseY = start.y + stepUp;
            float requestedY = stepUp > 0f ? Math.Max(target.y, baseY) : target.y;
            Vector3 horizontal = Physics.ResolveHorizontal(start, new Vector3(target.x, start.y, target.z), ignored);
            Vector3 resolved = new Vector3(horizontal.x, baseY, horizontal.z);
            resolved.y = Physics.ResolveVertical(new Vector3(start.x, baseY, start.z),
                new Vector3(resolved.x, requestedY, resolved.z), ignored);
            transform.position = resolved;
            collisionFlags = resolved == target ? CollisionFlags.None : CollisionFlags.Sides;
        }
    }

    public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public struct RaycastHit
    {
        public Collider collider { get; set; }
        public Vector3 point { get; set; }
        public Vector3 normal { get; set; }
        public float distance { get; set; }
        public Transform transform => collider != null ? collider.transform : null;
    }

    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask mask) => mask.value;
        public static implicit operator LayerMask(int value) => new LayerMask { value = value };
    }

    public static class Physics
    {
        // Collider shapes are gathered per query from the live scene so enable/disable is honoured.
        internal static readonly List<Collider> Scratch = new List<Collider>();
        internal static Action<List<Collider>> SceneProvider;

        /// <summary>Refreshes Scratch from the live scene before a query (Unity syncs implicitly).</summary>
        static void Sync()
        {
            if (SceneProvider == null) return;
            SceneProvider(Scratch);
        }
        public static bool autoSyncTransforms { get; set; }
        public static void SyncTransforms() { }

        /// <summary>Refreshes the collider snapshot the queries run against.</summary>
        public static void Recollect() => SceneProvider?.Invoke(Scratch);

        internal static bool Matches(Collider collider, int mask, QueryTriggerInteraction trigger)
        {
            if (collider == null || !collider.Active) return false;
            if ((mask & (1 << collider.gameObject.layer)) == 0) return false;
            if (collider.isTrigger && trigger == QueryTriggerInteraction.Ignore) return false;
            return true;
        }

        // --- shape helpers -------------------------------------------------
        /// <summary>
        /// World-space box description. The position stays explicit so penetration tests can query a
        /// collider at an arbitrary transform (Unity's ComputePenetration semantics).
        /// </summary>
        internal static void BoxWorld(BoxCollider box, out Vector3 center, out Vector3 extents, out Quaternion rotation)
        {
            Transform t = box.transform;
            center = t.TransformPoint(box.center);
            Vector3 s = t.lossyScale;
            extents = new Vector3(Math.Abs(box.size.x * s.x) * .5f, Math.Abs(box.size.y * s.y) * .5f, Math.Abs(box.size.z * s.z) * .5f);
            rotation = t.rotation;
        }

        internal static void CapsuleWorld(Collider collider, out Vector3 a, out Vector3 b, out float radius, out Quaternion rotation)
        {
            Transform t = collider.transform;
            Vector3 s = t.lossyScale;
            rotation = t.rotation;
            if (collider is CapsuleCollider capsule)
            {
                int axis = capsule.direction;
                float scale = axis == 0 ? Math.Abs(s.x) : axis == 1 ? Math.Abs(s.y) : Math.Abs(s.z);
                radius = capsule.radius * Math.Max(Math.Abs(s.x), Math.Abs(s.z));
                if (axis == 1) radius = capsule.radius * Math.Max(Math.Abs(s.x), Math.Abs(s.z));
                float half = Math.Max(0f, capsule.height * scale * .5f - radius);
                Vector3 axisVector = axis == 0 ? Vector3.right : axis == 2 ? Vector3.forward : Vector3.up;
                Vector3 worldAxis = rotation * axisVector;
                Vector3 center = t.TransformPoint(capsule.center);
                a = center - worldAxis * half;
                b = center + worldAxis * half;
            }
            else if (collider is SphereCollider sphere)
            {
                radius = sphere.radius * Math.Max(Math.Abs(s.x), Math.Max(Math.Abs(s.y), Math.Abs(s.z)));
                Vector3 center = t.TransformPoint(sphere.center);
                a = b = center;
            }
            else if (collider is CharacterController controller)
            {
                radius = controller.radius * Math.Max(Math.Abs(s.x), Math.Abs(s.z));
                float half = Math.Max(0f, controller.height * Math.Abs(s.y) * .5f - radius);
                Vector3 center = t.TransformPoint(controller.center);
                a = center - Vector3.up * half;
                b = center + Vector3.up * half;
                rotation = Quaternion.identity;
            }
            else { radius = .5f; a = b = t.position; }
        }

        internal static bool ClosestPoint(Collider collider, Vector3 point, out Vector3 closest, out float distance)
        {
            if (collider is BoxCollider box)
            {
                BoxWorld(box, out Vector3 center, out Vector3 extents, out Quaternion rotation);
                Vector3 local = rotation.Inverse() * (point - center);
                Vector3 clamped = new Vector3(
                    Mathf.Clamp(local.x, -extents.x, extents.x),
                    Mathf.Clamp(local.y, -extents.y, extents.y),
                    Mathf.Clamp(local.z, -extents.z, extents.z));
                closest = center + rotation * clamped;
                distance = (point - closest).magnitude;
                return true;
            }
            if (collider is SphereCollider || collider is CapsuleCollider || collider is CharacterController || collider is MeshCollider)
            {
                CapsuleWorld(collider, out Vector3 a, out Vector3 b, out float radius, out _);
                closest = ClosestOnSegment(a, b, point);
                Vector3 delta = point - closest;
                float length = delta.magnitude;
                if (length > 1e-5f) closest += delta / length * Math.Min(radius, length);
                distance = Math.Max(0f, length - radius);
                return true;
            }
            closest = point; distance = 0f; return false;
        }

        internal static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSquared);
            return a + ab * t;
        }

        // Ray (origin, unit direction) against a box in its local frame.
        static bool RayBox(Vector3 origin, Vector3 direction, BoxCollider box, float maxDistance, out float hitDistance)
        {
            hitDistance = 0f;
            BoxWorld(box, out Vector3 center, out Vector3 extents, out Quaternion rotation);
            // Scale into the box's local space: a ray parameterised in world units stays valid
            // because the local origin/direction are both divided by the same scale.
            Vector3 scale = box.transform.lossyScale;
            Vector3 safeScale = new Vector3(
                Math.Abs(scale.x) > 1e-6f ? Math.Abs(scale.x) : 1f,
                Math.Abs(scale.y) > 1e-6f ? Math.Abs(scale.y) : 1f,
                Math.Abs(scale.z) > 1e-6f ? Math.Abs(scale.z) : 1f);
            // Divide by the world scale first, then rotate into the box's local frame.
            Vector3 localOrigin = rotation.Inverse() * new Vector3(
                (origin.x - center.x) / safeScale.x,
                (origin.y - center.y) / safeScale.y,
                (origin.z - center.z) / safeScale.z);
            Vector3 localDirection = rotation.Inverse() * new Vector3(
                direction.x / safeScale.x, direction.y / safeScale.y, direction.z / safeScale.z);
            float tMin = 0f, tMax = maxDistance;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = axis == 0 ? localOrigin.x : axis == 1 ? localOrigin.y : localOrigin.z;
                float d = axis == 0 ? localDirection.x : axis == 1 ? localDirection.y : localDirection.z;
                float e = axis == 0 ? extents.x : axis == 1 ? extents.y : extents.z;
                if (Math.Abs(d) < 1e-7f) { if (o < -e || o > e) return false; continue; }
                float t1 = (-e - o) / d, t2 = (e - o) / d;
                if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; }
                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
            hitDistance = tMin;
            return true;
        }

        static bool RayCapsule(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, float radius, float maxDistance, out float hitDistance)
        {
            // Sampled sphere sweep: adequate for the coarse 1-2 m geometry used here.
            hitDistance = float.MaxValue;
            const int samples = 96;
            for (int i = 0; i <= samples; i++)
            {
                float t = maxDistance * i / samples;
                Vector3 point = origin + direction * t;
                Vector3 closest = ClosestOnSegment(a, b, point);
                if ((point - closest).magnitude <= radius) { hitDistance = t; return true; }
            }
            return false;
        }

        // --- public queries ------------------------------------------------
        public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Sync();
            var hits = new List<RaycastHit>();
            Vector3 unit = direction.normalized;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Collider collider = Scratch[i];
                if (!Matches(collider, layerMask, queryTriggerInteraction)) continue;
                float distance;
                if (collider is BoxCollider box)
                {
                    if (!RayBox(origin, unit, box, maxDistance, out distance)) continue;
                }
                else
                {
                    CapsuleWorld(collider, out Vector3 a, out Vector3 b, out float radius, out _);
                    if (!RayCapsule(origin, unit, a, b, radius, maxDistance, out distance)) continue;
                }
                hits.Add(new RaycastHit { collider = collider, distance = distance, point = origin + unit * distance });
            }
            return hits.ToArray();
        }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            var hits = RaycastAll(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
            hitInfo = hits.Length > 0 ? hits[0] : default;
            return hits.Length > 0;
        }
        public static bool Linecast(Vector3 start, Vector3 end, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            return distance > 1e-5f && RaycastAll(start, delta / distance, distance, layerMask, queryTriggerInteraction).Length > 0;
        }
        public static bool Linecast(Vector3 start, Vector3 end, int layerMask) => Linecast(start, end, layerMask, QueryTriggerInteraction.UseGlobal);

        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Sync();
            hitInfo = default;
            Vector3 unit = direction.normalized;
            float best = float.MaxValue;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Collider collider = Scratch[i];
                if (!Matches(collider, layerMask, queryTriggerInteraction)) continue;
                const int samples = 40;
                for (int s = 0; s <= samples; s++)
                {
                    float t = maxDistance * s / samples;
                    Vector3 center = origin + unit * t;
                    ClosestPoint(collider, center, out Vector3 closest, out float distance);
                    if (distance <= radius)
                    {
                        if (t < best)
                        {
                            best = t;
                            hitInfo = new RaycastHit { collider = collider, distance = t, point = center, normal = (center - closest).normalized };
                        }
                        break;
                    }
                }
            }
            return best < float.MaxValue;
        }
        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask) =>
            SphereCast(origin, radius, direction, out hitInfo, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);

        public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Sync();
            Vector3 unit = direction.normalized;
            const int samples = 40;
            for (int s = 0; s <= samples; s++)
            {
                float t = maxDistance * s / samples;
                Vector3 offset = unit * t;
                Vector3 a = point1 + offset, b = point2 + offset;
                foreach (var collider in Scratch)
                {
                    if (!Matches(collider, layerMask, queryTriggerInteraction)) continue;
                    if (CapsuleOverlaps(collider, a, b, radius, out _)) return true;
                }
            }
            return false;
        }
        public static bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float maxDistance, int layerMask) =>
            CapsuleCast(point1, point2, radius, direction, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);

        public static bool CheckCapsule(Vector3 start, Vector3 end, float radius, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            Sync();
            foreach (var collider in Scratch)
            {
                if (!Matches(collider, layerMask, queryTriggerInteraction)) continue;
                if (CapsuleOverlaps(collider, start, end, radius, out _)) return true;
            }
            return false;
        }
        public static bool CheckCapsule(Vector3 start, Vector3 end, float radius, int layerMask) => CheckCapsule(start, end, radius, layerMask, QueryTriggerInteraction.UseGlobal);

        public static bool ComputePenetration(Collider colliderA, Vector3 positionA, Quaternion rotationA,
            Collider colliderB, Vector3 positionB, Quaternion rotationB, out Vector3 direction, out float distance)
        {
            direction = Vector3.zero;
            distance = 0f;
            if (colliderA == null || colliderB == null) return false;
            // Capsule A is described by the CALLER's transform, not by where its GameObject sits
            // (the shift validator keeps its probe parked far below the arena).
            Vector3 a, b; float radius;
            Vector3 localCenter; float height; int axis = 1;
            if (colliderA is CharacterController controller)
            {
                localCenter = controller.center; radius = controller.radius; height = controller.height;
            }
            else if (colliderA is CapsuleCollider capsule)
            {
                localCenter = capsule.center; radius = capsule.radius; height = capsule.height; axis = capsule.direction;
            }
            else if (colliderA is SphereCollider sphere)
            {
                localCenter = sphere.center; radius = sphere.radius; height = sphere.radius * 2f;
            }
            else { direction = Vector3.zero; distance = 0f; return false; }
            Vector3 capsuleCenter = positionA + rotationA * localCenter;
            float halfLength = Math.Max(0f, height * .5f - radius);
            Vector3 axisVector = axis == 0 ? Vector3.right : axis == 2 ? Vector3.forward : Vector3.up;
            Vector3 worldAxis = rotationA * axisVector;
            a = capsuleCenter - worldAxis * halfLength;
            b = capsuleCenter + worldAxis * halfLength;

            // Collider B is evaluated at ITS live transform (Unity's ComputePenetration contract).
            float depth;
            if (colliderB is BoxCollider box)
            {
                BoxWorld(box, out Vector3 boxCenter, out Vector3 boxExtents, out Quaternion boxRotation);
                depth = SeparatedDepth(box, boxCenter, boxRotation, boxExtents, a, b, radius);
            }
            else if (!CapsuleOverlaps(colliderB, a, b, radius, out depth)) depth = 0f;
            if (depth <= 0f) { direction = Vector3.zero; distance = 0f; return false; }
            direction = Vector3.up;
            distance = depth;
            return true;
        }

        // Approximate penetration depth of a capsule (a,b,radius) against a collider.
        internal static bool CapsuleOverlaps(Collider collider, Vector3 a, Vector3 b, float radius, out float depth)
        {
            depth = 0f;
            const int samples = 12;
            float worst = 0f;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 center = a + (b - a) * (i / (float)samples);
                ClosestPoint(collider, center, out _, out float distance);
                if (distance < radius) worst = Math.Max(worst, radius - distance);
            }
            depth = worst;
            return worst > 1e-4f;
        }

        /// <summary>Converts a capsule sticking out of a box into the push needed to clear it.</summary>
        internal static float BoxDepenetration(BoxCollider box, Vector3 a, Vector3 b, float radius)
        {
            BoxWorld(box, out Vector3 center, out Vector3 extents, out Quaternion rotation);
            float best = 0f;
            for (int i = 0; i <= 12; i++)
            {
                Vector3 sphereCenter = a + (b - a) * (i / 12f);
                Vector3 local = rotation.Inverse() * (sphereCenter - center);
                // Closest point on the box (inside counts as zero) expressed in local space.
                Vector3 closest = new Vector3(
                    Mathf.Clamp(local.x, -extents.x, extents.x),
                    Mathf.Clamp(local.y, -extents.y, extents.y),
                    Mathf.Clamp(local.z, -extents.z, extents.z));
                if (closest == local)
                {
                    // Sphere centre inside the box: push out through the nearest face.
                    float dx = extents.x - Math.Abs(local.x);
                    float dy = extents.y - Math.Abs(local.y);
                    float dz = extents.z - Math.Abs(local.z);
                    float face = Math.Min(dx, Math.Min(dy, dz)) + radius;
                    best = Math.Max(best, face);
                }
                else
                {
                    float distance = (local - closest).magnitude;
                    if (distance < radius) best = Math.Max(best, radius - distance);
                }
            }
            return best;
        }

        /// <summary>True when a capsule centred at <paramref name="center"/> would rest on layer 8.</summary>
        internal static bool ProbeGround(Vector3 center, float height, float radius, Transform ignored) =>
            Blocked(center - Vector3.up * .02f, radius * .98f, height, ignored);

        /// <summary>
        /// Lifts a falling CharacterController capsule back to the last free position.
        /// <paramref name="from"/> is known free; <paramref name="to"/> is known blocked.
        /// </summary>
        internal static float ResolveGround(Vector3 from, Vector3 to, float radius, float height, Transform ignored)
        {
            Sync();
            var actor = ignored != null ? ignored.GetComponent<CharacterController>() : null;
            Vector3 centerOffset = actor != null ? actor.center : Vector3.up * (height * .5f);
            if (!Blocked(to + centerOffset, radius, height, ignored)) return to.y;
            float free = from.y, blocked = to.y;
            for (int i = 0; i < 18; i++)
            {
                float mid = (free + blocked) * .5f;
                if (Blocked(new Vector3(to.x, mid, to.z) + centerOffset, radius, height, ignored)) blocked = mid;
                else free = mid;
            }
            return free;
        }

        /// <summary>
        /// Horizontal resolution for a CharacterController capsule: try the full step, then slide
        /// along X or Z. Returns the resolved horizontal position (Y is left untouched).
        /// </summary>
        /// <summary>Height of the ledge the last horizontal resolve climbed (0 when none).</summary>
        internal static float stepUpHeight;

        internal static Vector3 ResolveHorizontal(Vector3 from, Vector3 to, Transform ignored)
        {
            Sync();
            stepUpHeight = 0f;
            var actor = ignored != null ? ignored.GetComponent<CharacterController>() : null;
            if (actor == null) return new Vector3(to.x, from.y, to.z);
            Vector3 centerOffset = actor.center;
            float radius = actor.radius, height = actor.height;
            // Unity's CharacterController resolves against a capsule shrunk by its skin width, which is
            // why an actor resting on (or 5 mm into) the floor still slides horizontally. Lift the
            // bottom of the probe capsule by the skin and leave the top where it is.
            float skin = Math.Max(actor.skinWidth, .02f);
            Vector3 liftedCenter = centerOffset + Vector3.up * (skin * .5f);
            float liftedHeight = height - skin;
            bool Free(Vector3 point) => !Blocked(point + liftedCenter, radius, liftedHeight, ignored);
            var full = new Vector3(to.x, from.y, to.z);
            if (Free(full)) return full;
            // Step offset: Unity lets a controller climb ledges up to stepOffset tall, so retry the
            // same horizontal move with the capsule lifted. Vertical resolution settles the height.
            if (actor.stepOffset > 0f)
            {
                Vector3 lifted = full + Vector3.up * actor.stepOffset;
                if (!Blocked(lifted + liftedCenter, radius, liftedHeight, ignored))
                {
                    stepUpHeight = actor.stepOffset;
                    return full;
                }
            }
            stepUpHeight = 0f;
            // Slide: keep whichever single axis remains free, preferring the larger movement.
            float dx = Math.Abs(to.x - from.x), dz = Math.Abs(to.z - from.z);
            Vector3 first = dx >= dz ? new Vector3(to.x, from.y, from.z) : new Vector3(from.x, from.y, to.z);
            Vector3 second = dx >= dz ? new Vector3(from.x, from.y, to.z) : new Vector3(to.x, from.y, from.z);
            if (Free(first)) return first;
            if (Free(second)) return second;
            return new Vector3(from.x, from.y, from.z);
        }

        /// <summary>Vertical resolution: the highest position between the two that stays free.</summary>
        internal static float ResolveVertical(Vector3 from, Vector3 to, Transform ignored)
        {
            Sync();
            var actor = ignored != null ? ignored.GetComponent<CharacterController>() : null;
            if (actor == null) return to.y;
            Vector3 centerOffset = actor.center;
            if (!Blocked(to + centerOffset, actor.radius, actor.height, ignored)) return to.y;
            float free = from.y, blocked = to.y;
            for (int i = 0; i < 18; i++)
            {
                float mid = (free + blocked) * .5f;
                if (Blocked(new Vector3(to.x, mid, to.z) + centerOffset, actor.radius, actor.height, ignored)) blocked = mid;
                else free = mid;
            }
            return free;
        }

        internal static bool Blocked(Vector3 center, float radius, float height, Transform ignored)
        {
            Sync();
            // Unity's capsule spans Center +/- Height/2 with hemispherical caps; the end spheres sit
            // at Center +/- (Height/2 - Radius).
            float half = Math.Max(0f, height * .5f - radius);
            Vector3 a = center - Vector3.up * half, b = center + Vector3.up * half;
            foreach (var collider in Scratch)
            {
                if (collider == null || !collider.Active) continue;
                if (ignored != null && collider.transform == ignored) continue;
                if (collider.gameObject.layer != 8) continue;
                // CapsuleEndsInside: an end cap already inside the box counts as blocked even when
                // ClosestPoint reports zero distance (this is how a floor is detected).
                if (Depth(collider, a, b, radius) > .005f) return true;
            }
            return false;
        }

        internal static float Depth(Collider collider, Vector3 a, Vector3 b, float radius)
        {
            if (collider is BoxCollider box) return BoxDepenetration(box, a, b, radius);
            return CapsuleOverlaps(collider, a, b, radius, out float depth) ? depth : 0f;
        }

        /// <summary>Separation of an actor capsule from a collider at the given world transform.</summary>
        internal static float SeparatedDepth(BoxCollider box, Vector3 center, Quaternion rotation, Vector3 extents,
            Vector3 a, Vector3 b, float radius)
        {
            float best = 0f;
            for (int i = 0; i <= 8; i++)
            {
                Vector3 point = a + (b - a) * (i / 8f);
                Vector3 local = rotation.Inverse() * (point - center);
                Vector3 closest = new Vector3(
                    Mathf.Clamp(local.x, -extents.x, extents.x),
                    Mathf.Clamp(local.y, -extents.y, extents.y),
                    Mathf.Clamp(local.z, -extents.z, extents.z));
                if (closest == local)
                {
                    float face = Math.Min(extents.x - Math.Abs(local.x), Math.Min(extents.y - Math.Abs(local.y), extents.z - Math.Abs(local.z)));
                    best = Math.Max(best, face + radius);
                }
                else
                {
                    float distance = (local - closest).magnitude;
                    if (distance < radius) best = Math.Max(best, radius - distance);
                }
            }
            return best;
        }

        static bool EndsInside(Collider collider, Vector3 a, Vector3 b)
        {
            if (collider is BoxCollider box)
            {
                BoxWorld(box, out Vector3 center, out Vector3 extents, out Quaternion rotation);
                return InsideBox(a, center, extents, rotation) || InsideBox(b, center, extents, rotation);
            }
            return false;
        }

        static bool InsideBox(Vector3 point, Vector3 center, Vector3 extents, Quaternion rotation)
        {
            Vector3 local = rotation.Inverse() * (point - center);
            return Math.Abs(local.x) <= extents.x && Math.Abs(local.y) <= extents.y && Math.Abs(local.z) <= extents.z;
        }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) => new Shader { name = name };
    }

    public class Material : Object
    {
        public Shader shader;
        public Color color = Color.white;
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
        public Material(Shader shader) { this.shader = shader; }
        public Material(Material source) { shader = source.shader; color = source.color; }
        public void SetFloat(string name, float value) => floats[name] = value;
        public float GetFloat(string name) => floats.TryGetValue(name, out float v) ? v : 0f;
        public void SetColor(string name, Color value) => colors[name] = value;
        public Color GetColor(string name) => colors.TryGetValue(name, out Color v) ? v : Color.white;
        public void EnableKeyword(string keyword) { }
        public void DisableKeyword(string keyword) { }
        public bool HasProperty(string name) => true;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color red => new Color(1, 0, 0, 1);
        public static Color green => new Color(0, 1, 0, 1);
        public static Color blue => new Color(0, 0, 1, 1);
        public static Color gray => new Color(.5f, .5f, .5f, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color operator *(Color c, float s) => new Color(c.r * s, c.g * s, c.b * s, c.a * s);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
    }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Material material { get => sharedMaterial; set => sharedMaterial = value; }
        public bool enabled { get; set; } = true;
    }
    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } }

    public class Mesh : Object
    {
        public Vector3[] vertices = Array.Empty<Vector3>();
        public int[] triangles = Array.Empty<int>();
        public void SetVertices(List<Vector3> values) => vertices = values.ToArray();
        public void SetTriangles(List<int> values, int submesh) => triangles = values.ToArray();
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
        public void CombineMeshes(CombineInstance[] combine) { }
    }
    public struct CombineInstance { public Mesh mesh; public Matrix4x4 transform; }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public LightShadows shadows { get; set; }
        public Color color { get; set; } = Color.white;
        public float intensity { get; set; } = 1f;
    }

    public class AudioListener : Behaviour { }

    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }

    public enum ScreenOrientation { Portrait = 1, PortraitUpsideDown = 2, LandscapeLeft = 3, LandscapeRight = 4, AutoRotation = 5 }

    public class Camera : Behaviour
    {
        public CameraClearFlags clearFlags { get; set; }
        public static Camera main { get; set; }
        public Color backgroundColor { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public float fieldOfView { get; set; }
    }

    public static class RenderSettings
    {
        public static Light sun;
        public static bool fog;
        public static FogMode fogMode;
        public static float fogStartDistance, fogEndDistance;
        public static Rendering.AmbientMode ambientMode;
        public static Color ambientLight = Color.white;
        public static Color fogColor = Color.white;
    }

    public static class Input
    {
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> pressed = new HashSet<KeyCode>();
        static bool mouseDown;
        public static Vector3 mousePosition { get; set; }
        public static void Press(KeyCode key) { held.Add(key); pressed.Add(key); }
        public static void Release(KeyCode key) => held.Remove(key);
        public static void PressMouse() => mouseDown = true;
        public static bool GetKey(KeyCode key) => held.Contains(key);
        public static bool GetKeyDown(KeyCode key) => pressed.Contains(key);
        public static bool GetMouseButtonDown(int button) => button == 0 && mouseDown;
        public static float GetAxisRaw(string axis) => 0f;
        /// <summary>Clears one-frame state; call once per simulated frame after all Update calls.</summary>
        public static void EndFrame() { pressed.Clear(); mouseDown = false; }
        public static void ResetAll() { held.Clear(); pressed.Clear(); mouseDown = false; }
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox = 0, Trilight = 1, Flat = 3, Custom = 4 }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; public bool IsValid() => true; }
}

// ---------------------------------------------------------------------------
// Minimal uGUI + EventSystems surface so the real HUD and touch-input scripts
// can be exercised (label text, button callbacks, joystick drag, raycast gating).
// Rendering is a no-op: layout, sprites and canvas batching are not simulated.
// ---------------------------------------------------------------------------
namespace UnityEngine
{
    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; } = new Vector2(.5f, .5f);
        public Vector2 anchorMax { get; set; } = new Vector2(.5f, .5f);
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Rect rect => new Rect(0, 0, 100, 100);
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Vector2 min => new Vector2(x, y);
        public Vector2 max => new Vector2(x + width, y + height);
        public Vector2 size => new Vector2(width, height);
        public static bool operator ==(Rect a, Rect b) => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        public static bool operator !=(Rect a, Rect b) => !(a == b);
        public override bool Equals(object o) => o is Rect r && this == r;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ width.GetHashCode() ^ height.GetHashCode();
    }

    public class Texture2D : Object
    {
        public int width = 4, height = 4;
        public static Texture2D whiteTexture { get; } = new Texture2D();
    }

    public class Sprite : Object
    {
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot) => new Sprite { name = "sprite" };
    }

    public class Font : Object { }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => (T)(object)new Font { name = path };
    }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
        public static ScriptableObject CreateInstance(Type type) => (ScriptableObject)Activator.CreateInstance(type);
    }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 arg0);

    public class UnityEvent
    {
        readonly List<UnityAction> calls = new List<UnityAction>();
        public void AddListener(UnityAction call) => calls.Add(call);
        public void RemoveListener(UnityAction call) => calls.Remove(call);
        public void RemoveAllListeners() => calls.Clear();
        public void Invoke() { foreach (var call in calls.ToArray()) call(); }
    }

    public class UnityEvent<T0>
    {
        readonly List<UnityAction<T0>> calls = new List<UnityAction<T0>>();
        public void AddListener(UnityAction<T0> call) => calls.Add(call);
        public void RemoveListener(UnityAction<T0> call) => calls.Remove(call);
        public void Invoke(T0 value) { foreach (var call in calls.ToArray()) call(value); }
    }
}

namespace UnityEngine.EventSystems
{
    public class StandaloneInputModule : Behaviour { }

    public class EventSystem : Behaviour
    {
        public static EventSystem current { get; set; }
        public GameObject currentSelectedGameObject { get; private set; }
        public void SetSelectedGameObject(GameObject selected) => currentSelectedGameObject = selected;
        public bool IsPointerOverGameObject() => false;
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults)
        {
            raycastResults.Clear();
            if (HudRaycasts != null) HudRaycasts(eventData, raycastResults);
        }
        public static System.Action<PointerEventData, List<RaycastResult>> HudRaycasts;
    }

    public struct RaycastResult
    {
        public GameObject gameObject;
        public float distance;
    }

    public class BaseEventData
    {
        public BaseEventData(EventSystem eventSystem) { currentInputModule = eventSystem; }
        public EventSystem currentInputModule { get; }
        public bool used { get; set; }
    }

    public class PointerEventData : BaseEventData
    {
        public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }
        public enum InputButton { Left = 0, Right = 1, Middle = 2 }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }
        public int pointerId { get; set; }
        public InputButton button { get; set; }
        public Camera pressEventCamera { get; set; }
        public GameObject pointerPress { get; set; }
        public void Reset() { position = Vector2.zero; delta = Vector2.zero; pointerId = 0; button = InputButton.Left; }
    }

    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IDragHandler { void OnDrag(PointerEventData eventData); }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData eventData); }

    public static class RectTransformUtility
    {
        public static bool ScreenPointToLocalPointInRectangle(RectTransform rect, Vector2 screenPoint, Camera cam, out Vector2 localPoint)
        {
            localPoint = screenPoint;
            return true;
        }
    }
}

namespace UnityEngine.UI
{
    public class Graphic : Behaviour
    {
        public RectTransform rectTransform => (RectTransform)transform;
        public Color color { get; set; } = Color.white;
        public bool raycastTarget { get; set; } = true;
        public Sprite sprite { get; set; }
    }

    public class Image : Graphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; } = 1f;
    }

    public class Text : Graphic
    {
        public string text { get; set; } = string.Empty;
        public Font font { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; } = true;
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

    public struct Navigation
    {
        public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 }
        public Mode mode { get; set; }
    }

    public class Selectable : Behaviour
    {
        public Navigation navigation { get; set; }
        public Graphic targetGraphic { get; set; }
        public bool interactable { get; set; } = true;
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : Events.UnityEvent { }
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
        public void Press() => onClick.Invoke();
    }

    public class Slider : Selectable
    {
        public class SliderEvent : Events.UnityEvent<float> { }
        public SliderEvent onValueChanged { get; } = new SliderEvent();
        public RectTransform handleRect { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; } = 1f;
        float valueField;
        public float value
        {
            get => valueField;
            set { valueField = value; onValueChanged.Invoke(value); }
        }
    }

    public class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; }
        public float scaleFactor { get; set; } = 1f;
    }

    public class GraphicRaycaster : Behaviour { }

    public class Outline : Behaviour
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
    }
}

namespace Veilbreak
{
    using UnityEngine;

    /// <summary>
    /// Simulation stand-in for PrototypeSceneArt: builds the parts gameplay depends on (the player
    /// visual and the guardian's core/armour transforms) without any mesh work.
    /// </summary>
    public sealed class PrototypeSceneArt
    {
        readonly PrimitiveFactory factory;
        public PrototypeSceneArt(PrimitiveFactory art) { factory = art; }
        public Material Energy => factory.Material("energy", Color.white, true);
        public Material Warning => factory.Material("warning", Color.red, true);
        public System.Collections.Generic.List<VeilReactiveObject> Courtyard(Transform parent) =>
            new System.Collections.Generic.List<VeilReactiveObject>();
        public Transform Warrior(Transform root, out Transform blade)
        {
            blade = factory.Group("Blade swing", root, Vector3.zero);
            return factory.Group("Warrior", root, Vector3.zero);
        }
        public Transform Guardian(Transform root, out Transform core, out Transform armor)
        {
            core = factory.Group("core", root, Vector3.zero);
            armor = factory.Group("armour", root, Vector3.zero);
            return factory.Group("Guardian visual", root, Vector3.zero);
        }
    }

    /// <summary>Simulation stand-in for EncounterAudio: generated tones need the audio system.</summary>
    public sealed class EncounterAudio : MonoBehaviour
    {
        public void Initialize(ArenaGameManager manager) { }
    }

    /// <summary>
    /// Simulation stand-in for PrimitiveFactory (real one needs a renderer and mesh uploads).
    /// Keeps the public shape so the real scene-art script could compile against it later.
    /// </summary>
    public sealed class PrimitiveFactory
    {
        public Material Material(string name, Color color, bool emissive = false) => new Material(new Shader()) { name = name, color = color };
        public Transform Group(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }
        public Transform Shape(string name, Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, bool solid = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (solid) { go.layer = 8; go.AddComponent<BoxCollider>(); }
            return go.transform;
        }
        public Transform Facet(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool pointed = false) =>
            Shape(name, parent, PrimitiveType.Cube, pos, scale, mat);
        public void CombineDecor(Transform group, Material material) { }
        public void Dispose() { }
    }
}
