using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DisplayProfileManager.Core;
using DisplayProfileManager.Tests.Helpers;

namespace DisplayProfileManager.Tests.Tests
{
    /// <summary>
    /// Testes para <see cref="ProfileManager.ValidateCloneGroups"/> (método private,
    /// acessado via reflection enquanto não for extraído para uma classe própria).
    ///
    /// Todos são testes de REGRESSÃO: o método já existe e funciona corretamente.
    /// Garantem que futuras alterações não quebrem as regras de validação de clone groups.
    /// </summary>
    [TestClass]
    public class CloneGroupValidationTests
    {
        private MethodInfo _validateMethod;

        [TestInitialize]
        public void Setup()
        {
            _validateMethod = typeof(ProfileManager).GetMethod(
                "ValidateCloneGroups",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(_validateMethod,
                "ValidateCloneGroups não encontrado em ProfileManager. " +
                "Se o método foi renomeado ou extraído, atualize esta classe de teste.");
        }

        private bool Validate(List<DisplaySetting> settings)
        {
            return (bool)_validateMethod.Invoke(ProfileManager.Instance, new object[] { settings });
        }

        // ────────────────────────────────────────────────────────────────────
        // Casos válidos — devem retornar true
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenEmpty_ReturnsTrue()
        {
            Assert.IsTrue(Validate(new List<DisplaySetting>()));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenOnlyExtendedDisplays_ReturnsTrue()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithSourceId(1).Build(),
            };

            Assert.IsTrue(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupHasTwoIdenticalMembers_ReturnsTrue()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
            };

            Assert.IsTrue(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupHasThreeIdenticalMembers_ReturnsTrue()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
            };

            Assert.IsTrue(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupHasOneMember_ReturnsTrue()
        {
            // Um clone group com apenas 1 membro gera warning mas não é erro
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
            };

            Assert.IsTrue(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentDpi_ReturnsTrue()
        {
            // DPI diferente é apenas warning — não deve bloquear a aplicação do perfil
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithDpi(100).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithDpi(125).Build(),
            };

            Assert.IsTrue(Validate(settings),
                "DPI diferente em clone group deve gerar warning, não falha de validação");
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenMixedCloneAndExtended_ReturnsTrue()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithSourceId(1).Build(), // extended
            };

            Assert.IsTrue(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenMultipleValidCloneGroups_ReturnsTrue()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-2").WithSourceId(1).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-2").WithSourceId(1).Build(),
            };

            Assert.IsTrue(Validate(settings));
        }

        // ────────────────────────────────────────────────────────────────────
        // Casos inválidos — devem retornar false
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentWidth_ReturnsFalse()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithResolution(1920, 1080).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithResolution(2560, 1080).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentHeight_ReturnsFalse()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithResolution(1920, 1080).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithResolution(1920, 720).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentFrequency_ReturnsFalse()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithFrequency(60).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithFrequency(144).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentPositionX_ReturnsFalse()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithPosition(0, 0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithPosition(1920, 0).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentPositionY_ReturnsFalse()
        {
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithPosition(0, 0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithPosition(0, 1080).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMembersHaveDifferentSourceId_ReturnsFalse()
        {
            // Membros do mesmo clone group devem ter o mesmo SourceId
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(1).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenOneGroupValidAndOneInvalid_ReturnsFalse()
        {
            var settings = new List<DisplaySetting>
            {
                // clone-1 válido
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithResolution(1920, 1080).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-1").WithSourceId(0).WithResolution(1920, 1080).Build(),
                // clone-2 inválido — resoluções diferentes
                new DisplaySettingBuilder().WithCloneGroup("clone-2").WithSourceId(1).WithResolution(1920, 1080).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-2").WithSourceId(1).WithResolution(1280, 720).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ValidateCloneGroups_WhenCloneGroupMemberRetainsExtendedDesktopPosition_ReturnsFalse()
        {
            // Regression: ExecuteClone() was not syncing DisplayPositionX/Y to clone group members.
            // A display from an extended layout (e.g. at -1920,0) would join a clone group with the
            // primary at (0,0) but keep its old position, causing SetDisplayConfig to reject the config.
            // Reproduces the exact scenario from New.dpm: DISPLAY1 at (0,0) and DISPLAY2 at (-1920,0)
            // both assigned to the same clone group with sourceId=0.
            var settings = new List<DisplaySetting>
            {
                new DisplaySettingBuilder().WithCloneGroup("clone-f543fe96").WithSourceId(0).WithPosition(0, 0).Build(),
                new DisplaySettingBuilder().WithCloneGroup("clone-f543fe96").WithSourceId(0).WithPosition(-1920, 0).Build(),
            };

            Assert.IsFalse(Validate(settings));
        }
    }
}
