using System;
using System.Reflection;

namespace SixAgesAccessibility.Lore
{
    /// <summary>
    /// Loose-coupling bridge from the accessibility mod to the optional
    /// SixAgesDE translation plugin. The two mods ship and deploy separately —
    /// the accessibility mod must run with or without the translator — so the
    /// link is resolved by reflection on first use rather than a hard assembly
    /// reference. When SixAgesDE is absent the bridge stays inert and every
    /// call returns its input unchanged, leaving lore in English.
    ///
    /// Resolution is deferred to first use: BepInEx may load the two plugins
    /// in either order, but by the time the player opens a lore dialog both
    /// are guaranteed loaded.
    ///
    /// Mono note: every null test against a Type/MemberInfo/Assembly goes
    /// through an (object) cast — Unity 2018's Mono has no == operator on
    /// those reflection types and a bare == throws MissingMethodException.
    /// </summary>
    public static class TranslationBridge
    {
        private delegate string TranslateDelegate(string source);

        private static bool _resolved;
        private static TranslateDelegate _translate;

        /// <summary>True when SixAgesDE.Translator.Translate was found and bound.</summary>
        public static bool Available
        {
            get
            {
                EnsureResolved();
                return (object)_translate != null;
            }
        }

        /// <summary>
        /// Translate one string through SixAgesDE. Returns <paramref name="source"/>
        /// unchanged when the translator plugin is absent or the string has no
        /// corpus entry — callers can treat the result as "best-effort German".
        /// </summary>
        public static string Translate(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            EnsureResolved();
            if ((object)_translate == null) return source;
            try
            {
                string result = _translate(source);
                return string.IsNullOrEmpty(result) ? source : result;
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TranslationBridge.Translate", ex);
                return source;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Type translator = FindType("SixAgesDE.Translator");
                if ((object)translator == null)
                {
                    Plugin.Log?.LogInfo("[TranslationBridge] SixAgesDE plugin not present — lore stays in English.");
                    return;
                }

                // Bind by exact signature so an unrelated future overload can't
                // be picked up by accident.
                MethodInfo method = translator.GetMethod(
                    "Translate",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                if ((object)method == null)
                {
                    DebugLogger.Warn("TranslationBridge", "SixAgesDE.Translator.Translate(string) not found.");
                    return;
                }

                _translate = (TranslateDelegate)Delegate.CreateDelegate(typeof(TranslateDelegate), method);
                Plugin.Log?.LogInfo("[TranslationBridge] bound to SixAgesDE.Translator.Translate — lore will be translated.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("TranslationBridge.EnsureResolved", ex);
            }
        }

        /// <summary>
        /// Locate a type by full name across every loaded assembly. We probe
        /// each assembly with GetType rather than filtering by assembly name —
        /// that keeps the bridge working regardless of what SixAgesDE's
        /// assembly is called.
        /// </summary>
        private static Type FindType(string fullTypeName)
        {
            Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < all.Length; i++)
            {
                Assembly a = all[i];
                if ((object)a == null) continue;
                try
                {
                    Type t = a.GetType(fullTypeName);
                    if ((object)t != null) return t;
                }
                catch (Exception ex)
                {
                    DebugLogger.Error("TranslationBridge.FindType", ex);
                }
            }
            return null;
        }
    }
}
