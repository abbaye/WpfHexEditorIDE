//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Core.Definitions
// File: AssemblyInfo.cs
// Description: Exposes internal expression AST + Compile/Parse helpers to the
//              test assembly so unit tests can exercise them without enlarging
//              the public NuGet surface.
//////////////////////////////////////////////

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WpfHexEditor.Tests")]
