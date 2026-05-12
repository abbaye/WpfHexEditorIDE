//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/Functions/WhfmtBuiltinFunctions.cs
// Description: Out-of-the-box functions available in every whfmt expression.
//              Domain-specific functions (readUInt16BE, parseZIPArchive, etc.)
//              are registered by the host that has the underlying byte stream.
//////////////////////////////////////////////

using System.Globalization;

namespace WpfHexEditor.Core.Definitions.Models.Functions;

// SYNC: keep this list in lock-step with $ExprBuiltins in
//       Sources/.claude/skills/whfmt-guard/scripts/whfmt-guard.ps1
internal static class WhfmtBuiltinFunctions
{
    public static IReadOnlyList<IWhfmtFunction> All { get; } =
    [
        new MinFn(),
        new MaxFn(),
        new AbsFn(),
        new LengthFn(),
        new HexFn(),
        new ToUpperFn(),
        new ToLowerFn(),
    ];

    private sealed class MinFn : IWhfmtFunction
    {
        public string Name => "min";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count < 2) throw new ArgumentException("min requires at least 2 arguments");
            double m = Convert.ToDouble(args[0], CultureInfo.InvariantCulture);
            for (int i = 1; i < args.Count; i++)
                m = Math.Min(m, Convert.ToDouble(args[i], CultureInfo.InvariantCulture));
            return m;
        }
    }

    private sealed class MaxFn : IWhfmtFunction
    {
        public string Name => "max";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count < 2) throw new ArgumentException("max requires at least 2 arguments");
            double m = Convert.ToDouble(args[0], CultureInfo.InvariantCulture);
            for (int i = 1; i < args.Count; i++)
                m = Math.Max(m, Convert.ToDouble(args[i], CultureInfo.InvariantCulture));
            return m;
        }
    }

    private sealed class AbsFn : IWhfmtFunction
    {
        public string Name => "abs";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count != 1) throw new ArgumentException("abs requires 1 argument");
            return Math.Abs(Convert.ToDouble(args[0], CultureInfo.InvariantCulture));
        }
    }

    private sealed class LengthFn : IWhfmtFunction
    {
        public string Name => "length";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count != 1) throw new ArgumentException("length requires 1 argument");
            return args[0] switch
            {
                string s        => (long)s.Length,
                byte[] b        => (long)b.Length,
                System.Collections.ICollection c => (long)c.Count,
                null            => 0L,
                _               => throw new ArgumentException("length: unsupported argument type"),
            };
        }
    }

    private sealed class HexFn : IWhfmtFunction
    {
        public string Name => "hex";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count != 1) throw new ArgumentException("hex requires 1 argument");
            return args[0] switch
            {
                long l   => "0x" + l.ToString("X", CultureInfo.InvariantCulture),
                int i    => "0x" + i.ToString("X", CultureInfo.InvariantCulture),
                double d => "0x" + ((long)d).ToString("X", CultureInfo.InvariantCulture),
                _        => throw new ArgumentException("hex: argument must be numeric"),
            };
        }
    }

    private sealed class ToUpperFn : IWhfmtFunction
    {
        public string Name => "toUpper";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count != 1) throw new ArgumentException("toUpper requires 1 argument");
            return args[0]?.ToString()?.ToUpperInvariant();
        }
    }

    private sealed class ToLowerFn : IWhfmtFunction
    {
        public string Name => "toLower";
        public object? Invoke(IReadOnlyList<object?> args)
        {
            if (args.Count != 1) throw new ArgumentException("toLower requires 1 argument");
            return args[0]?.ToString()?.ToLowerInvariant();
        }
    }
}
