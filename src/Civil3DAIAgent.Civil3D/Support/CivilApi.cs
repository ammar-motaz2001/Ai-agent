using System;
using System.Linq;
using System.Reflection;
using Civil3DAIAgent.Logging;

namespace Civil3DAIAgent.Civil3D.Support
{
    /// <summary>
    /// Late-bound (reflection) invocation of Civil 3D API members whose exact signatures vary between
    /// releases. Using this for the version-fragile calls lets the whole solution compile against any
    /// Civil 3D reference assembly, and — critically — when a member or overload is not found at
    /// runtime it logs the <b>actual</b> members available on the type, so the correct signature can be
    /// pinned down from a single run instead of repeated compile-guess cycles.
    /// </summary>
    /// <remarks>
    /// This is intentionally scoped to the handful of `// [VERSION]` call sites. Stable API (databases,
    /// transactions, alignments' read properties, surfaces' elevation queries, etc.) is called directly
    /// with full compile-time checking.
    /// </remarks>
    public static class CivilApi
    {
        private const string Category = "API";

        /// <summary>
        /// Invokes a public static method by name, choosing the overload whose parameters accept the
        /// supplied <paramref name="args"/>. Returns the method's return value, or <c>null</c> when no
        /// matching method exists (logging the available overloads/members).
        /// </summary>
        public static object InvokeStatic(Type type, string name, object[] args, ILogger log)
        {
            var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var method = Match(type.GetMethods(flags), name, args);
            if (method == null) { LogMembers(type, name, flags, log); return null; }
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        /// <summary>
        /// Invokes a public instance method by name on <paramref name="target"/>. Returns the return
        /// value, or <c>null</c> when no matching method exists (logging the available members).
        /// </summary>
        public static object Invoke(object target, string name, object[] args, ILogger log)
        {
            if (target == null) return null;
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            var method = Match(target.GetType().GetMethods(flags), name, args);
            if (method == null) { LogMembers(target.GetType(), name, flags, log); return null; }
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        /// <summary>
        /// Invokes a public instance method (typically void) and returns whether a matching method was
        /// found and invoked. Use this for void members where a null return value is not meaningful.
        /// </summary>
        public static bool TryInvoke(object target, string name, object[] args, ILogger log)
        {
            if (target == null) return false;
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            var method = Match(target.GetType().GetMethods(flags), name, args);
            if (method == null) { LogMembers(target.GetType(), name, flags, log); return false; }
            try { method.Invoke(target, args); return true; }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }

        /// <summary>Sets a public instance property if it exists and is writable. Returns success.</summary>
        public static bool TrySet(object target, string propertyName, object value, ILogger log)
        {
            if (target == null) return false;
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite)
            {
                log?.Warn($"[API] property {target.GetType().Name}.{propertyName} not settable; skipped.", Category);
                return false;
            }
            prop.SetValue(target, value);
            return true;
        }

        /// <summary>Gets a public instance property or field value, or <c>null</c> if neither exists.</summary>
        public static object Get(object target, string memberName)
        {
            if (target == null) return null;
            var type = target.GetType();
            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(target);
            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(target);
        }

        /// <summary>Reads a member as a double (property or field), or <paramref name="fallback"/> if absent/non-numeric.</summary>
        public static double GetDouble(object target, string memberName, double fallback = 0.0)
        {
            var value = Get(target, memberName);
            if (value == null) return fallback;
            try { return Convert.ToDouble(value); } catch { return fallback; }
        }

        /// <summary>Finds the first method with the given name whose parameters accept the args.</summary>
        private static MethodInfo Match(MethodInfo[] methods, string name, object[] args)
        {
            foreach (var m in methods)
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;

                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    var a = args[i];
                    if (a == null)
                    {
                        // null is only assignable to reference types / Nullable<T>.
                        if (pt.IsValueType && Nullable.GetUnderlyingType(pt) == null) { ok = false; break; }
                    }
                    else if (!pt.IsInstanceOfType(a))
                    {
                        ok = false; break;
                    }
                }
                if (ok) return m;
            }
            return null;
        }

        /// <summary>Logs the real members for <paramref name="name"/> (or all names when absent).</summary>
        private static void LogMembers(Type type, string name, BindingFlags flags, ILogger log)
        {
            if (log == null) return;
            var matching = type.GetMethods(flags).Where(m => m.Name == name).Select(Signature).ToList();
            if (matching.Count > 0)
            {
                log.Warn($"[API] No overload of {type.Name}.{name} matched the supplied arguments. " +
                         $"Actual overloads: {string.Join("  |  ", matching)}", Category);
            }
            else
            {
                var names = type.GetMethods(flags).Select(m => m.Name).Distinct().OrderBy(x => x);
                log.Warn($"[API] {type.Name}.{name} does not exist. Available {(flags.HasFlag(BindingFlags.Static) ? "static" : "instance")} " +
                         $"methods: {string.Join(", ", names)}", Category);
            }
        }

        private static string Signature(MethodInfo m) =>
            m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ")";
    }
}
