//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Core.Models
{
    /// <summary>
    /// Virtual position in the edited view (includes inserted bytes, excludes deleted bytes when hidden)
    /// </summary>
    public readonly struct VirtualPosition : IEquatable<VirtualPosition>, IComparable<VirtualPosition>
    {
        public long Value { get; }

        public VirtualPosition(long value) => Value = value;

        public static VirtualPosition Zero => new(0);
        public static VirtualPosition Invalid => new(-1);
        public bool IsValid => Value >= 0;

        public static implicit operator long(VirtualPosition pos) => pos.Value;
        public static explicit operator VirtualPosition(long value) => new(value);

        public static VirtualPosition operator +(VirtualPosition a, long b) => new(a.Value + b);
        public static VirtualPosition operator -(VirtualPosition a, long b) => new(a.Value - b);
        public static long operator -(VirtualPosition a, VirtualPosition b) => a.Value - b.Value;

        public static bool operator ==(VirtualPosition a, VirtualPosition b) => a.Value == b.Value;
        public static bool operator !=(VirtualPosition a, VirtualPosition b) => a.Value != b.Value;
        public static bool operator <(VirtualPosition a, VirtualPosition b) => a.Value < b.Value;
        public static bool operator >(VirtualPosition a, VirtualPosition b) => a.Value > b.Value;
        public static bool operator <=(VirtualPosition a, VirtualPosition b) => a.Value <= b.Value;
        public static bool operator >=(VirtualPosition a, VirtualPosition b) => a.Value >= b.Value;

        public bool Equals(VirtualPosition other) => Value == other.Value;
        public override bool Equals(object obj) => obj is VirtualPosition other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(VirtualPosition other) => Value.CompareTo(other.Value);
        public override string ToString() => $"V:{Value}";
    }

    /// <summary>
    /// Physical position in the actual file/stream (actual byte offset)
    /// </summary>
    public readonly struct PhysicalPosition : IEquatable<PhysicalPosition>, IComparable<PhysicalPosition>
    {
        public long Value { get; }

        public PhysicalPosition(long value) => Value = value;

        public static PhysicalPosition Zero => new(0);
        public static PhysicalPosition Invalid => new(-1);
        public bool IsValid => Value >= 0;

        public static implicit operator long(PhysicalPosition pos) => pos.Value;
        public static explicit operator PhysicalPosition(long value) => new(value);

        public static PhysicalPosition operator +(PhysicalPosition a, long b) => new(a.Value + b);
        public static PhysicalPosition operator -(PhysicalPosition a, long b) => new(a.Value - b);
        public static long operator -(PhysicalPosition a, PhysicalPosition b) => a.Value - b.Value;

        public static bool operator ==(PhysicalPosition a, PhysicalPosition b) => a.Value == b.Value;
        public static bool operator !=(PhysicalPosition a, PhysicalPosition b) => a.Value != b.Value;
        public static bool operator <(PhysicalPosition a, PhysicalPosition b) => a.Value < b.Value;
        public static bool operator >(PhysicalPosition a, PhysicalPosition b) => a.Value > b.Value;
        public static bool operator <=(PhysicalPosition a, PhysicalPosition b) => a.Value <= b.Value;
        public static bool operator >=(PhysicalPosition a, PhysicalPosition b) => a.Value >= b.Value;

        public bool Equals(PhysicalPosition other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PhysicalPosition other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(PhysicalPosition other) => Value.CompareTo(other.Value);
        public override string ToString() => $"P:{Value}";
    }
}
