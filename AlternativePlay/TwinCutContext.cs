using System;

namespace AlternativePlay
{
    internal static class TwinCutContext
    {
        [ThreadStatic]
        internal static Saber Source;
    }
}
