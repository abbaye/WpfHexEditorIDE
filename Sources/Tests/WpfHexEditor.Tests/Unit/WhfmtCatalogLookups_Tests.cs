//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Project: WpfHexEditor.Tests
// File: Unit/WhfmtCatalogLookups_Tests.cs
// Description:
//     Coverage for F1 — IEmbeddedFormatCatalog.GetByName + GetByFormatId
//     O(1) dictionary lookups, with parity against the legacy
//     Query().WithName/WithFormatId.First() pattern.
//////////////////////////////////////////////

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Query;

namespace WpfHexEditor.Tests.Unit
{
    [TestClass]
    public class WhfmtCatalogLookups_Tests
    {
        private static readonly EmbeddedFormatCatalog Cat = EmbeddedFormatCatalog.Instance;

        [TestMethod]
        public void GetByName_ParityWithQuery()
        {
            var fast = Cat.GetByName("Game Boy Color ROM");
            var slow = Cat.Query().WithName("Game Boy Color ROM").First();
            Assert.IsNotNull(fast);
            Assert.AreEqual(slow.ResourceKey, fast.ResourceKey);
        }

        [TestMethod]
        public void GetByFormatId_ParityWithQuery()
        {
            var fast = Cat.GetByFormatId("ROM_GBC");
            var slow = Cat.Query().WithFormatId("ROM_GBC").First();
            Assert.IsNotNull(fast);
            Assert.AreEqual(slow.ResourceKey, fast.ResourceKey);
        }

        [TestMethod]
        public void GetByName_IsCaseInsensitive()
        {
            var lower = Cat.GetByName("game boy color rom");
            var upper = Cat.GetByName("GAME BOY COLOR ROM");
            var mixed = Cat.GetByName("Game Boy Color ROM");
            Assert.IsNotNull(lower);
            Assert.AreSame(lower, upper);
            Assert.AreSame(lower, mixed);
        }

        [TestMethod]
        public void GetByFormatId_IsCaseInsensitive()
        {
            var lower = Cat.GetByFormatId("rom_gbc");
            var upper = Cat.GetByFormatId("ROM_GBC");
            Assert.IsNotNull(lower);
            Assert.AreSame(lower, upper);
        }

        [TestMethod]
        public void GetByName_ReturnsNullForMissing()
        {
            Assert.IsNull(Cat.GetByName("NoSuchFormatExists"));
            Assert.IsNull(Cat.GetByName(""));
            Assert.IsNull(Cat.GetByName(null!));
        }

        [TestMethod]
        public void GetByFormatId_ReturnsNullForMissing()
        {
            Assert.IsNull(Cat.GetByFormatId("NoSuchId"));
            Assert.IsNull(Cat.GetByFormatId(""));
            Assert.IsNull(Cat.GetByFormatId(null!));
        }
    }
}
