// Minimal, ASSSERTING NUnit shim for the headless harness.
//
// Tools/OfflineCompile/UnityTestStubs.cs deliberately has empty bodies because it only has to
// satisfy the compiler. This one is different: it actually evaluates the assertions so
// EditModeRunner can execute the project's real EditMode test sources outside Unity.
//
// It is not NUnit and it is not the Unity Test Runner. Only the members the project's tests use
// are implemented, and any member that is missing is a compile error rather than a silent pass.
using System;
using System.Collections;
using System.Collections.Generic;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class OneTimeSetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class OneTimeTearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class CategoryAttribute : Attribute { public CategoryAttribute(string name) { } }

    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        static void Fail(string what, string message) =>
            throw new AssertionException(what + (string.IsNullOrEmpty(message) ? "" : " :: " + message));

        static bool Numeric(object value) =>
            value is int || value is long || value is float || value is double || value is short || value is byte;

        static double AsDouble(object value) => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

        public static void AreEqual(object expected, object actual) => AreEqual(expected, actual, null);
        public static void AreEqual(object expected, object actual, string message)
        {
            if (Numeric(expected) && Numeric(actual))
            {
                if (AsDouble(expected) != AsDouble(actual))
                    Fail("expected " + expected + " but was " + actual, message);
                return;
            }
            if (!Equals(expected, actual)) Fail("expected " + Show(expected) + " but was " + Show(actual), message);
        }
        public static void AreEqual(int expected, int actual) => AreEqual(expected, actual, null);
        public static void AreEqual(int expected, int actual, string message) => AreEqual((object)expected, actual, message);
        public static void AreEqual(float expected, float actual, float delta) => AreEqual(expected, actual, delta, null);
        public static void AreEqual(float expected, float actual, float delta, string message)
        {
            if (Math.Abs(expected - actual) > delta)
                Fail("expected " + expected.ToString("0.#####") + " +/- " + delta + " but was " + actual.ToString("0.#####"), message);
        }
        public static void AreEqual(double expected, double actual, double delta) => AreEqual(expected, actual, delta, null);
        public static void AreEqual(double expected, double actual, double delta, string message)
        {
            if (Math.Abs(expected - actual) > delta)
                Fail("expected " + expected + " +/- " + delta + " but was " + actual, message);
        }

        public static void AreNotEqual(object expected, object actual) => AreNotEqual(expected, actual, null);
        public static void AreNotEqual(object expected, object actual, string message)
        {
            bool same = Numeric(expected) && Numeric(actual)
                ? AsDouble(expected) == AsDouble(actual)
                : Equals(expected, actual);
            if (same) Fail("expected a value different from " + Show(expected), message);
        }

        public static void AreSame(object expected, object actual) => AreSame(expected, actual, null);
        public static void AreSame(object expected, object actual, string message)
        {
            if (!ReferenceEquals(expected, actual)) Fail("expected the same instance", message);
        }
        public static void AreNotSame(object expected, object actual) => AreNotSame(expected, actual, null);
        public static void AreNotSame(object expected, object actual, string message)
        {
            if (ReferenceEquals(expected, actual)) Fail("expected a different instance", message);
        }

        public static void IsTrue(bool condition) => IsTrue(condition, null);
        public static void IsTrue(bool condition, string message) { if (!condition) Fail("expected true", message); }
        public static void IsFalse(bool condition) => IsFalse(condition, null);
        public static void IsFalse(bool condition, string message) { if (condition) Fail("expected false", message); }

        public static void IsNull(object value) => IsNull(value, null);
        public static void IsNull(object value, string message) { if (value != null) Fail("expected null but was " + Show(value), message); }
        public static void IsNotNull(object value) => IsNotNull(value, null);
        public static void IsNotNull(object value, string message) { if (value == null) Fail("expected a value but was null", message); }

        // NUnit's argument order is (actual, bound): Assert.Greater(a, b) means "a is greater than b".
        public static void Greater(int actual, int bound) => Greater(actual, bound, null);
        public static void Greater(int actual, int bound, string message) => Greater((double)actual, bound, message);
        public static void Greater(long actual, long bound) => Greater(actual, bound, null);
        public static void Greater(long actual, long bound, string message) => Greater((double)actual, bound, message);
        public static void Greater(float actual, float bound) => Greater(actual, bound, null);
        public static void Greater(float actual, float bound, string message) => Greater((double)actual, bound, message);
        public static void Greater(double actual, double bound) => Greater(actual, bound, null);
        public static void Greater(double actual, double bound, string message)
        {
            if (!(actual > bound)) Fail("expected " + actual + " to be greater than " + bound, message);
        }

        public static void Less(int actual, int bound) => Less(actual, bound, null);
        public static void Less(int actual, int bound, string message) => Less((double)actual, bound, message);
        public static void Less(float actual, float bound) => Less(actual, bound, null);
        public static void Less(float actual, float bound, string message) => Less((double)actual, bound, message);
        public static void Less(double actual, double bound) => Less(actual, bound, null);
        public static void Less(double actual, double bound, string message)
        {
            if (!(actual < bound)) Fail("expected " + actual + " to be less than " + bound, message);
        }

        public static void IsNotEmpty(string value) => IsNotEmpty(value, null);
        public static void IsNotEmpty(string value, string message)
        {
            if (string.IsNullOrEmpty(value)) Fail("expected a non-empty string", message);
        }
        public static void IsNotEmpty(IEnumerable values) => IsNotEmpty(values, null);
        public static void IsNotEmpty(IEnumerable values, string message)
        {
            if (values == null) Fail("expected a non-empty collection but was null", message);
            var walker = values.GetEnumerator();
            bool any = walker.MoveNext();
            if (walker is IDisposable disposable) disposable.Dispose();
            if (!any) Fail("expected a non-empty collection", message);
        }
        public static void IsEmpty(IEnumerable values) => IsEmpty(values, null);
        public static void IsEmpty(IEnumerable values, string message)
        {
            if (values == null) return;
            var walker = values.GetEnumerator();
            bool any = walker.MoveNext();
            if (walker is IDisposable disposable) disposable.Dispose();
            if (any) Fail("expected an empty collection", message);
        }

        public static void Fail(string message) => throw new AssertionException(message);

        static string Show(object value) => value == null ? "<null>" : value.ToString();
    }

    public static class StringAssert
    {
        public static void Contains(string expected, string actual) => Contains(expected, actual, null);
        public static void Contains(string expected, string actual, string message)
        {
            if (actual == null || expected == null || !actual.Contains(expected))
                throw new AssertionException("expected \"" + expected + "\" in \"" + actual + "\"" +
                    (string.IsNullOrEmpty(message) ? "" : " :: " + message));
        }
    }

    public static class CollectionAssert
    {
        public static void AreEquivalent(IEnumerable expected, IEnumerable actual) => AreEquivalent(expected, actual, null);
        public static void AreEquivalent(IEnumerable expected, IEnumerable actual, string message)
        {
            if (expected == null || actual == null)
                throw new AssertionException("collection was null" + (string.IsNullOrEmpty(message) ? "" : " :: " + message));
            var left = new List<object>();
            var right = new List<object>();
            foreach (var item in expected) left.Add(item);
            foreach (var item in actual) right.Add(item);
            if (left.Count != right.Count)
                throw new AssertionException("expected " + left.Count + " items but was " + right.Count +
                    (string.IsNullOrEmpty(message) ? "" : " :: " + message));
            foreach (var item in left)
            {
                int index = right.FindIndex(other => Equals(other, item));
                if (index < 0)
                    throw new AssertionException("missing item " + item + (string.IsNullOrEmpty(message) ? "" : " :: " + message));
                right.RemoveAt(index);
            }
        }
    }
}
