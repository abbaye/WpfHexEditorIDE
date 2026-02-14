//////////////////////////////////////////////
// Apache 2.0  - 2026
// Migration Compatibility Aliases
// Phase 2: V2 → Main, V1 → Legacy
//////////////////////////////////////////////

using System;

namespace WpfHexaEditor
{
    /// <summary>
    /// LEGACY COMPATIBILITY ALIAS: HexEditorV1 → HexEditorLegacy (V1)
    ///
    /// ⚠️ THIS IS THE OLD V1 CONTROL - DEPRECATED!
    ///
    /// Problems with V1:
    /// - 99% SLOWER rendering (ItemsControl vs DrawingContext)
    /// - Critical bugs: #145 (insert mode broken), save data loss
    /// - 100-6000x slower operations (no SIMD, no parallel, linear search)
    ///
    /// ✅ PLEASE MIGRATE TO: HexEditor (V2 is now default!)
    ///
    /// Benefits of V2:
    /// - 99% faster rendering
    /// - All critical bugs fixed
    /// - 100-6000x faster operations (SIMD, parallel, binary search)
    /// - Same public API - just change the class name!
    ///
    /// Migration: Change "HexEditorV1" to "HexEditor" (30 seconds!)
    ///
    /// This alias will be REMOVED in v3.0 (April 2027 - 12 months).
    /// See MIGRATION_PLAN_V2.md for complete migration guide.
    /// </summary>
    [Obsolete(
        "HexEditorV1 (V1 legacy) is deprecated. " +
        "Use HexEditor instead (V2 is now the main control - 99% faster with bug fixes). " +
        "HexEditorV1 will be REMOVED in v3.0 (April 2027). " +
        "See MIGRATION_PLAN_V2.md for migration guide.",
        false  // Warning, not error (yet)
    )]
    public class HexEditorV1 : HexEditorLegacy
    {
        /// <summary>
        /// Creates new HexEditorV1 (legacy V1 control).
        /// Shows deprecation warning in debug mode.
        /// </summary>
        public HexEditorV1() : base()
        {
            #if DEBUG
            System.Diagnostics.Debug.WriteLine("");
            System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            System.Diagnostics.Debug.WriteLine("⚠️  WARNING: You are using HexEditorV1 (LEGACY V1 CONTROL)");
            System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            System.Diagnostics.Debug.WriteLine("");
            System.Diagnostics.Debug.WriteLine("Problems with V1:");
            System.Diagnostics.Debug.WriteLine("  ✗ 99% SLOWER rendering");
            System.Diagnostics.Debug.WriteLine("  ✗ Critical bugs (#145 insert mode, save data loss)");
            System.Diagnostics.Debug.WriteLine("  ✗ 100-6000x slower operations");
            System.Diagnostics.Debug.WriteLine("");
            System.Diagnostics.Debug.WriteLine("SOLUTION: Migrate to HexEditor (V2) for:");
            System.Diagnostics.Debug.WriteLine("  ✓ 99% faster rendering");
            System.Diagnostics.Debug.WriteLine("  ✓ All bugs fixed");
            System.Diagnostics.Debug.WriteLine("  ✓ 100-6000x faster operations");
            System.Diagnostics.Debug.WriteLine("");
            System.Diagnostics.Debug.WriteLine("EASY MIGRATION (30 seconds):");
            System.Diagnostics.Debug.WriteLine("  OLD: <control:HexEditorV1 ... />");
            System.Diagnostics.Debug.WriteLine("  NEW: <control:HexEditor ... />   ← Just change the name!");
            System.Diagnostics.Debug.WriteLine("");
            System.Diagnostics.Debug.WriteLine("See: MIGRATION_PLAN_V2.md for complete guide");
            System.Diagnostics.Debug.WriteLine("Removal: v3.0 (April 2027 - 12 months)");
            System.Diagnostics.Debug.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            System.Diagnostics.Debug.WriteLine("");
            #endif
        }
    }

    /// <summary>
    /// COMPATIBILITY ALIAS: HexEditorV2 → HexEditor (V2)
    ///
    /// ℹ️ HexEditorV2 is now just called "HexEditor" (cleaner name)
    ///
    /// Both names work and refer to the same control (V2 - fast & bug-free):
    /// - HexEditor (✅ recommended - cleaner)
    /// - HexEditorV2 (✅ works - but deprecated name)
    ///
    /// This is the GOOD control:
    /// - ✓ 99% faster rendering
    /// - ✓ All critical bugs fixed
    /// - ✓ 100-6000x faster operations
    ///
    /// Migration: Change "HexEditorV2" to "HexEditor" (for cleaner code)
    ///
    /// This alias will be REMOVED in v3.0 (April 2027 - 12 months).
    /// </summary>
    [Obsolete(
        "HexEditorV2 is now the main HexEditor control. " +
        "Use HexEditor instead for cleaner code (same control, just simpler name). " +
        "This alias will be REMOVED in v3.0 (April 2027).",
        false  // Warning, not error
    )]
    public class HexEditorV2 : HexEditor
    {
        /// <summary>
        /// Creates new HexEditorV2 (V2 control, now called HexEditor).
        /// No warning needed - this is the good control, just use HexEditor name.
        /// </summary>
        public HexEditorV2() : base()
        {
            // V2 is good! Just use "HexEditor" name instead for cleaner code.
        }
    }
}
