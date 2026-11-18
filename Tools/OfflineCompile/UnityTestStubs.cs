// VEILBREAK offline-compile stub: NUnit + Unity Test Framework surface used by Assets/VEILBREAK/Tests.
//
// NUnit and the Unity Test Framework are not redistributable from this sandbox, so the test
// assemblies are typechecked against this faithful signature stub. Assertions here do nothing --
// this file does not and cannot run the tests. See Documentation/OfflineCompilation.md.
using System;
using System.Collections;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public TestCaseAttribute(params object[] arguments) { }
        public string TestName { get; set; }
        public string Description { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TearDownAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DescriptionAttribute : Attribute
    {
        public DescriptionAttribute(string description) { }
    }

    public delegate void TestDelegate();

    public static class Assert
    {
        public static void AreEqual(object expected, object actual) { }
        public static void AreEqual(object expected, object actual, string message) { }
        public static void AreEqual(int expected, int actual) { }
        public static void AreEqual(int expected, int actual, string message) { }
        public static void AreEqual(long expected, long actual) { }
        public static void AreEqual(long expected, long actual, string message) { }
        public static void AreEqual(float expected, float actual, float delta) { }
        public static void AreEqual(float expected, float actual, float delta, string message) { }
        public static void AreEqual(double expected, double actual, double delta) { }
        public static void AreEqual(double expected, double actual, double delta, string message) { }
        public static void AreEqual(object expected, object actual, string message, params object[] args) { }

        public static void AreNotEqual(object expected, object actual) { }
        public static void AreNotEqual(object expected, object actual, string message) { }
        public static void AreNotEqual(int expected, int actual) { }
        public static void AreNotEqual(int expected, int actual, string message) { }
        public static void AreNotEqual(float expected, float actual, float delta) { }
        public static void AreNotEqual(float expected, float actual, float delta, string message) { }

        public static void IsNotEmpty(System.Collections.IEnumerable collection) { }
        public static void IsNotEmpty(System.Collections.IEnumerable collection, string message) { }
        public static void IsNotEmpty(string value) { }
        public static void IsNotEmpty(string value, string message) { }
        public static void IsEmpty(System.Collections.IEnumerable collection) { }
        public static void IsEmpty(System.Collections.IEnumerable collection, string message) { }

        public static void AreSame(object expected, object actual) { }
        public static void AreSame(object expected, object actual, string message) { }
        public static void AreNotSame(object expected, object actual) { }
        public static void AreNotSame(object expected, object actual, string message) { }

        public static void IsTrue(bool condition) { }
        public static void IsTrue(bool condition, string message) { }
        public static void IsTrue(bool condition, string message, params object[] args) { }
        public static void IsFalse(bool condition) { }
        public static void IsFalse(bool condition, string message) { }
        public static void IsFalse(bool condition, string message, params object[] args) { }

        public static void IsNull(object value) { }
        public static void IsNull(object value, string message) { }
        public static void IsNotNull(object value) { }
        public static void IsNotNull(object value, string message) { }

        public static void Greater(int arg1, int arg2) { }
        public static void Greater(int arg1, int arg2, string message) { }
        public static void Greater(float arg1, float arg2) { }
        public static void Greater(float arg1, float arg2, string message) { }
        public static void Greater(double arg1, double arg2) { }
        public static void Greater(double arg1, double arg2, string message) { }
        public static void GreaterOrEqual(int arg1, int arg2) { }
        public static void GreaterOrEqual(int arg1, int arg2, string message) { }
        public static void GreaterOrEqual(float arg1, float arg2, string message) { }

        public static void Less(int arg1, int arg2) { }
        public static void Less(int arg1, int arg2, string message) { }
        public static void Less(float arg1, float arg2) { }
        public static void Less(float arg1, float arg2, string message) { }
        public static void LessOrEqual(int arg1, int arg2) { }
        public static void LessOrEqual(int arg1, int arg2, string message) { }
        public static void LessOrEqual(float arg1, float arg2, string message) { }

        public static void Fail() { }
        public static void Fail(string message) { }
        public static void Fail(string message, params object[] args) { }
        public static void Pass() { }
        public static void Pass(string message) { }
        public static void Inconclusive(string message) { }
        public static void Ignore(string message) { }

        public static T Throws<T>(TestDelegate code) where T : Exception => null;
        public static T Throws<T>(TestDelegate code, string message) where T : Exception => null;
        public static Exception Throws(Type expectedExceptionType, TestDelegate code) => null;
        public static void DoesNotThrow(TestDelegate code) { }
    }

    public static class StringAssert
    {
        public static void Contains(string expected, string actual) { }
        public static void Contains(string expected, string actual, string message) { }
        public static void DoesNotContain(string expected, string actual) { }
        public static void DoesNotContain(string expected, string actual, string message) { }
        public static void StartsWith(string expected, string actual) { }
        public static void StartsWith(string expected, string actual, string message) { }
        public static void EndsWith(string expected, string actual) { }
        public static void EndsWith(string expected, string actual, string message) { }
        public static void AreEqualIgnoringCase(string expected, string actual) { }
        public static void AreEqualIgnoringCase(string expected, string actual, string message) { }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual) { }
        public static void AreEqual(IEnumerable expected, IEnumerable actual, string message) { }
        public static void AreEquivalent(IEnumerable expected, IEnumerable actual) { }
        public static void AreEquivalent(IEnumerable expected, IEnumerable actual, string message) { }
        public static void AreNotEquivalent(IEnumerable expected, IEnumerable actual) { }
        public static void AreNotEquivalent(IEnumerable expected, IEnumerable actual, string message) { }
        public static void Contains(IEnumerable collection, object item) { }
        public static void Contains(IEnumerable collection, object item, string message) { }
        public static void DoesNotContain(IEnumerable collection, object item) { }
        public static void DoesNotContain(IEnumerable collection, object item, string message) { }
        public static void IsEmpty(IEnumerable collection) { }
        public static void IsEmpty(IEnumerable collection, string message) { }
        public static void IsNotEmpty(IEnumerable collection) { }
        public static void IsNotEmpty(IEnumerable collection, string message) { }
    }
}

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UnityTestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UnitySetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UnityTearDownAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UnityPlatformAttribute : Attribute
    {
        public UnityPlatformAttribute(RuntimePlatform[] exclude) { }
    }

    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }
        public static void Expect(LogType type, string message) { }
        public static void Expect(LogType type, System.Text.RegularExpressions.Regex message) { }
        public static void NoUnexpectedReceived() { }
    }

    public class WaitForFrames
    {
        public WaitForFrames(int count) { }
    }
}
