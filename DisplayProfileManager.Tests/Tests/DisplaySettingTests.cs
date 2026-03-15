using Microsoft.VisualStudio.TestTools.UnitTesting;
using DisplayProfileManager.Core;
using DisplayProfileManager.Tests.Helpers;

namespace DisplayProfileManager.Tests.Tests
{
    /// <summary>
    /// Testes para <see cref="DisplaySetting"/>.
    /// </summary>
    [TestClass]
    public class DisplaySettingTests
    {
        // ────────────────────────────────────────────────────────────────────
        // IsPartOfCloneGroup — testes de regressão
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPartOfCloneGroup_WhenCloneGroupIdIsEmpty_ReturnsFalse()
        {
            var setting = new DisplaySettingBuilder().Build(); // CloneGroupId = string.Empty por padrão

            Assert.IsFalse(setting.IsPartOfCloneGroup());
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPartOfCloneGroup_WhenCloneGroupIdIsNull_ReturnsFalse()
        {
            var setting = new DisplaySettingBuilder().WithCloneGroup(null).Build();

            Assert.IsFalse(setting.IsPartOfCloneGroup());
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPartOfCloneGroup_WhenCloneGroupIdIsSet_ReturnsTrue()
        {
            var setting = new DisplaySettingBuilder().WithCloneGroup("clone-group-1").Build();

            Assert.IsTrue(setting.IsPartOfCloneGroup());
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void IsPartOfCloneGroup_DefaultConstruction_ReturnsFalse()
        {
            // Backward compatibility: perfis antigos (sem CloneGroupId) devem funcionar
            // como modo estendido sem nenhuma modificação
            var setting = new DisplaySetting();

            Assert.IsFalse(setting.IsPartOfCloneGroup(),
                "DisplaySetting sem CloneGroupId deve ser tratado como modo estendido");
        }
    }
}
