//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtViewModelSetField_Tests.cs
// Description:
//     F2 — verifies the string-specialized SetField on ViewModelBase:
//       (a) coerces null → empty before storing/comparing
//       (b) does NOT fire PropertyChanged when the new value equals the old
//       (c) notifies the alsoNotify dependent properties when changed
//     Exercised through EnrichedFormatViewModel's 4 documentary props
//     (NavigationStructure / NavigationNotes / InspectorBadge / ForensicNotes).
//////////////////////////////////////////////

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtViewModelSetField_Tests
    {
        private static List<string> CaptureChanges(EnrichedFormatViewModel vm)
        {
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => { if (e.PropertyName is { } n) fired.Add(n); };
            return fired;
        }

        [TestMethod]
        public void SetField_CoercesNullToEmpty()
        {
            var vm = new EnrichedFormatViewModel();
            vm.NavigationStructure = null!;
            Assert.AreEqual(string.Empty, vm.NavigationStructure);
            Assert.IsFalse(vm.HasNavigationStructure);
        }

        [TestMethod]
        public void SetField_NoPropertyChangedOnSameValue()
        {
            var vm = new EnrichedFormatViewModel();
            vm.NavigationStructure = "Header → Body";   // initial set
            var fired = CaptureChanges(vm);
            vm.NavigationStructure = "Header → Body";   // same value
            Assert.AreEqual(0, fired.Count, "PropertyChanged should not fire on same-value assign; saw: " + string.Join(",", fired));
        }

        [TestMethod]
        public void SetField_FiresPropertyChangedAndDependentOnChange()
        {
            var vm = new EnrichedFormatViewModel();
            var fired = CaptureChanges(vm);
            vm.NavigationStructure = "Header → Body";
            CollectionAssert.Contains(fired, nameof(EnrichedFormatViewModel.NavigationStructure));
            CollectionAssert.Contains(fired, nameof(EnrichedFormatViewModel.HasNavigationStructure));
        }

        [TestMethod]
        public void SetField_TreatsNullAndEmptyAsEqualAfterCoercion()
        {
            // _field starts at string.Empty; assigning null coerces to empty → no change → no event.
            var vm = new EnrichedFormatViewModel();
            var fired = CaptureChanges(vm);
            vm.ForensicNotes = null!;
            Assert.AreEqual(0, fired.Count);
        }
    }
}
