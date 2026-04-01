// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: Services/SignatureDecoder.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     BCL-only method / field / property signature decoder.
//     Uses System.Reflection.Metadata.SignatureDecoder<TType,TGenericContext>
//     with a string-based type provider to produce human-readable signatures
//     without any external NuGet dependency.
//
// Architecture Notes:
//     Pattern: Strategy (ISignatureTypeProvider implementation).
//     Thread-safe — no mutable state; can be shared across analysis tasks.
//     All exceptions are caught and return "?" to prevent analysis crashes.
// ==========================================================

using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace WpfHexEditor.Core.AssemblyAnalysis.Services;

/// <summary>
/// Decodes ECMA-335 method, field, and property blob signatures into
/// human-readable C#-style strings using only BCL APIs.
/// </summary>
public sealed class SignatureDecoder
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes the full signature of a method definition,
    /// e.g. <c>"int Add(int a, int b)"</c>.
    /// Returns the method name with <c>"(?)"</c> parameters on decode failure.
    /// </summary>
    public string DecodeMethodSignature(MethodDefinition methodDef, MetadataReader mdReader)
    {
        try
        {
            var provider   = new StringTypeProvider(mdReader);
            var methodName = mdReader.GetString(methodDef.Name);
            var decoded    = methodDef.DecodeSignature(provider, default);

            var returnType  = decoded.ReturnType;
            var parameters  = FormatParameters(decoded.ParameterTypes, methodDef, mdReader);
            var generics    = decoded.GenericParameterCount > 0
                              ? $"`{decoded.GenericParameterCount}"
                              : string.Empty;

            return $"{returnType} {methodName}{generics}({parameters})";
        }
        catch
        {
            var fallback = mdReader.GetString(methodDef.Name);
            return $"{fallback}(?)";
        }
    }

    /// <summary>
    /// Decodes the type name of a field, e.g. <c>"int"</c>, <c>"List&lt;string&gt;"</c>.
    /// Returns <c>"?"</c> on failure.
    /// </summary>
    public string DecodeFieldSignature(FieldDefinition fieldDef, MetadataReader mdReader)
    {
        try
        {
            var provider = new StringTypeProvider(mdReader);
            return fieldDef.DecodeSignature(provider, default);
        }
        catch { return "?"; }
    }

    /// <summary>
    /// Decodes the type name of a property, e.g. <c>"string"</c>.
    /// Returns <c>"?"</c> on failure.
    /// </summary>
    public string DecodePropertySignature(PropertyDefinition propDef, MetadataReader mdReader)
    {
        try
        {
            var provider  = new StringTypeProvider(mdReader);
            var decoded   = propDef.DecodeSignature(provider, default);
            return decoded.ReturnType;
        }
        catch { return "?"; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatParameters(
        ImmutableArray<string> paramTypes,
        MethodDefinition       methodDef,
        MetadataReader         mdReader)
    {
        if (paramTypes.IsEmpty) return string.Empty;

        // Attempt to read parameter names for nicer display.
        var paramDefs  = methodDef.GetParameters().ToArray();
        var parts      = new string[paramTypes.Length];

        for (var i = 0; i < paramTypes.Length; i++)
        {
            var typeName  = paramTypes[i];
            var paramName = string.Empty;

            // paramDefs[0] may be the return parameter (Sequence == 0); skip it.
            var defIdx = i + (paramDefs.Length > paramTypes.Length ? 1 : 0);
            if (defIdx < paramDefs.Length)
            {
                var pDef = mdReader.GetParameter(paramDefs[defIdx]);
                if (pDef.SequenceNumber > 0)
                    paramName = mdReader.GetString(pDef.Name);
            }

            parts[i] = string.IsNullOrEmpty(paramName)
                       ? typeName
                       : $"{typeName} {paramName}";
        }

        return string.Join(", ", parts);
    }

    // ── ISignatureTypeProvider implementation ─────────────────────────────────

    /// <summary>
    /// BCL-only type name provider that resolves metadata type references to
    /// C#-style keyword strings or fully-qualified names.
    /// </summary>
    private sealed class StringTypeProvider
        : ISignatureTypeProvider<string, object?>
    {
        private readonly MetadataReader _mdReader;

        public StringTypeProvider(MetadataReader mdReader)
            => _mdReader = mdReader;

        // Primitive type → C# keyword
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Byte    => "byte",
            PrimitiveTypeCode.SByte   => "sbyte",
            PrimitiveTypeCode.Char    => "char",
            PrimitiveTypeCode.Int16   => "short",
            PrimitiveTypeCode.UInt16  => "ushort",
            PrimitiveTypeCode.Int32   => "int",
            PrimitiveTypeCode.UInt32  => "uint",
            PrimitiveTypeCode.Int64   => "long",
            PrimitiveTypeCode.UInt64  => "ulong",
            PrimitiveTypeCode.Single  => "float",
            PrimitiveTypeCode.Double  => "double",
            PrimitiveTypeCode.String  => "string",
            PrimitiveTypeCode.Object  => "object",
            PrimitiveTypeCode.Void    => "void",
            PrimitiveTypeCode.IntPtr  => "nint",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.TypedReference => "TypedReference",
            _ => typeCode.ToString()
        };

        // TypeReference or TypeDefinition handle → simple name
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            return reader.GetString(typeDef.Name);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var typeRef = reader.GetTypeReference(handle);
            return reader.GetString(typeRef.Name);
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext,
            TypeSpecificationHandle handle, byte rawTypeKind)
        {
            // Decode the spec blob for generic instantiations.
            var spec = reader.GetTypeSpecification(handle);
            return spec.DecodeSignature(this, genericContext);
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => $"{StripArity(genericType)}<{string.Join(", ", typeArguments)}>";

        public string GetArrayType(string elementType, ArrayShape shape)
            => shape.Rank == 1
               ? $"{elementType}[]"
               : $"{elementType}[{new string(',', shape.Rank - 1)}]";

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetPointerType(string elementType)   => $"{elementType}*";
        public string GetByReferenceType(string elementType) => $"ref {elementType}";
        public string GetPinnedType(string elementType)    => $"pinned {elementType}";

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            var args  = string.Join(", ", signature.ParameterTypes);
            return $"delegate*<{args}, {signature.ReturnType}>";
        }

        public string GetGenericMethodParameter(object? genericContext, int index) => $"T{index}";
        public string GetGenericTypeParameter(object? genericContext, int index)   => $"T{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
            => unmodifiedType;

        // Strip generic arity suffix: "List`1" → "List"
        private static string StripArity(string name)
        {
            var tick = name.IndexOf('`');
            return tick >= 0 ? name[..tick] : name;
        }
    }
}
