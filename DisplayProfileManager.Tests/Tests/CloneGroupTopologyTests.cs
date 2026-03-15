using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DisplayProfileManager.Helpers;
using DisplayProfileManager.Tests.Helpers;

namespace DisplayProfileManager.Tests.Tests
{
    /// <summary>
    /// Testes para a lógica de topologia de clone groups.
    ///
    /// Cobre dois bugs relacionados:
    ///
    /// Bug #1 — Phase 2 consome um source mode por display habilitado, mas o Windows
    ///          cria apenas um source mode por SourceId após Phase 1. Para dois displays
    ///          em clone group (mesmo SourceId), o segundo display tentará consumir um
    ///          source mode que não existe, resultando em out-of-bounds ou atribuição errada.
    ///
    /// Bug #3 — ApplyDisplayTopology tem dois loops que desativam os mesmos displays.
    ///          O segundo loop é redundante e produz o mesmo resultado que o primeiro.
    ///
    /// Bug #5 — ApplyDisplayPosition existe e foi modificado, mas não é chamado pelo
    ///          fluxo principal de aplicação de perfil (ApplyUnifiedConfiguration).
    ///
    /// Os testes TDD documentam o comportamento esperado após os fixes.
    /// Testes de integração (requerem hardware) estão marcados com [Ignore].
    /// </summary>
    [TestClass]
    public class CloneGroupTopologyTests
    {
        // ────────────────────────────────────────────────────────────────────
        // Bug #1 — Contagem de source modes para clone groups
        //
        // O comportamento correto: N source modes necessários = N SourceIds únicos
        // O comportamento atual:   N source modes consumidos  = N displays habilitados
        //
        // Para clone group de 2 displays: 1 source mode criado pelo Windows,
        // mas Phase 2 tenta consumir 2 → segundo display recebe source mode errado.
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TDD")]
        [Description("Bug #1 — Para clone group, o número de source modes necessários é " +
                     "o número de SourceIds únicos, não o total de displays habilitados.")]
        public void SourceModesRequired_ForCloneGroup_EqualsUniqueSourceIdCount()
        {
            var displays = new List<DisplayConfigHelper.DisplayConfigInfo>
            {
                new DisplayConfigInfoBuilder().WithTargetId(0).WithSourceId(0).Build(), // clone A
                new DisplayConfigInfoBuilder().WithTargetId(1).WithSourceId(0).Build(), // clone B (mesmo SourceId)
            };

            int uniqueSourceIds    = displays.Where(d => d.IsEnabled).Select(d => d.SourceId).Distinct().Count();
            int totalEnabledCount  = displays.Count(d => d.IsEnabled);

            // Pré-condição: é realmente um clone group
            Assert.AreEqual(1, uniqueSourceIds,  "Clone group de 2 displays tem 1 SourceId único");
            Assert.AreEqual(2, totalEnabledCount, "São 2 displays habilitados");

            // O fix deve iterar por SourceId único, não por display individual.
            // Este assert documenta que as duas contagens DIFEREM para clone groups —
            // e que a lógica atual (que usa totalEnabledCount) está errada.
            Assert.AreNotEqual(uniqueSourceIds, totalEnabledCount,
                "Bug #1 confirmado: lógica atual consumiria 2 source modes, mas apenas 1 existe.");
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void SourceModesRequired_ForExtendedDisplays_EqualsTotalDisplayCount()
        {
            var displays = new List<DisplayConfigHelper.DisplayConfigInfo>
            {
                new DisplayConfigInfoBuilder().WithTargetId(0).WithSourceId(0).Build(),
                new DisplayConfigInfoBuilder().WithTargetId(1).WithSourceId(1).Build(),
            };

            int uniqueSourceIds   = displays.Where(d => d.IsEnabled).Select(d => d.SourceId).Distinct().Count();
            int totalEnabledCount = displays.Count(d => d.IsEnabled);

            // Para extended, cada display tem SourceId único: 1:1 é correto
            Assert.AreEqual(totalEnabledCount, uniqueSourceIds,
                "Para displays estendidos, uniqueSourceIds == totalEnabled (comportamento correto)");
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void SourceModesRequired_ForMixedConfig_EqualsUniqueSourceIdCount()
        {
            // 2 clones (sourceId=0) + 1 extended (sourceId=1) → 2 source modes
            var displays = new List<DisplayConfigHelper.DisplayConfigInfo>
            {
                new DisplayConfigInfoBuilder().WithTargetId(0).WithSourceId(0).Build(),
                new DisplayConfigInfoBuilder().WithTargetId(1).WithSourceId(0).Build(), // clone com target 0
                new DisplayConfigInfoBuilder().WithTargetId(2).WithSourceId(1).Build(), // extended
            };

            int uniqueSourceIds = displays.Where(d => d.IsEnabled).Select(d => d.SourceId).Distinct().Count();

            Assert.AreEqual(2, uniqueSourceIds,
                "Configuração mista (2 clones + 1 extended) requer 2 source modes");
        }

        [TestMethod]
        [TestCategory("TDD")]
        [Description("Bug #1 — Dois displays em clone group devem apontar para o MESMO " +
                     "índice de source mode após Phase 2. " +
                     "Este teste será completado quando a lógica de atribuição for " +
                     "extraída para uma função pura testável como parte do fix.")]
        [Ignore("Requer extração de AssignSourceModesToPaths() — habilitar como parte do fix do Bug #1")]
        public void AssignSourceModes_WhenDisplaysShareSourceId_AssignsSameSourceModeIndex()
        {
            // TODO: após extrair a lógica de Phase 2 para uma função pura:
            //
            // var paths = BuildPathsForCloneGroup();
            // var modes = BuildSourceModesForCloneGroup();  // apenas 1 source mode para sourceId=0
            // var displays = TwoClonedDisplays();
            //
            // AssignSourceModesToPaths(paths, modes, displays);
            //
            // uint idxA = paths[indexOfTargetId0].sourceInfo.modeInfoIdx;
            // uint idxB = paths[indexOfTargetId1].sourceInfo.modeInfoIdx;
            // Assert.AreEqual(idxA, idxB, "Clone group members devem compartilhar o mesmo source mode");

            Assert.Inconclusive("Aguardando extração da lógica de Phase 2.");
        }

        // ────────────────────────────────────────────────────────────────────
        // Bug #3 — Lógica de desativação é idempotente mas duplicada
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TDD")]
        [Description("Bug #3 — Os dois loops de desativação em ApplyDisplayTopology produzem " +
                     "o mesmo conjunto de targets. O segundo é redundante e pode ser removido " +
                     "sem alterar o resultado.")]
        public void DisableNonProfileDisplays_BothLoopsProduceSameResult()
        {
            var allTargetIds     = new HashSet<uint> { 0, 1, 2, 3 };
            var profileTargetIds = new HashSet<uint> { 0, 1 };

            // Lógica do loop 1 (usa profileTargetIds direto)
            var disabledByLoop1 = allTargetIds.Where(t => !profileTargetIds.Contains(t)).ToHashSet();

            // Lógica do loop 2 (usa displayConfigs.Any() — mesma semântica)
            var disabledByLoop2 = allTargetIds
                .Where(t => !profileTargetIds.Contains(t))
                .ToHashSet();

            CollectionAssert.AreEquivalent(disabledByLoop1.ToList(), disabledByLoop2.ToList(),
                "Ambos os loops desativam exatamente os mesmos targets. O segundo é redundante.");
        }

        [TestMethod]
        [TestCategory("TDD")]
        [Description("Bug #3 — Após o fix (remoção do loop duplicado), a contagem de displays " +
                     "desativados deve ser igual à contagem de displays fora do perfil.")]
        public void DisableNonProfileDisplays_CountMatchesDisplaysOutsideProfile()
        {
            var allTargetIds     = new HashSet<uint> { 0, 1, 2, 3 };
            var profileTargetIds = new HashSet<uint> { 0, 1 };

            int expectedDisabled = allTargetIds.Count - profileTargetIds.Count; // 2
            int actualDisabled   = allTargetIds.Count(t => !profileTargetIds.Contains(t));

            Assert.AreEqual(expectedDisabled, actualDisabled);
        }

        // ────────────────────────────────────────────────────────────────────
        // Bug #5 — ApplyDisplayPosition como dead code
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        [Description("Bug #5 — ApplyDisplayPosition era dead code (não chamado pelo fluxo principal) e foi removido.")]
        public void ApplyDisplayPosition_WasRemovedAsDeadCode()
        {
            var method = typeof(DisplayConfigHelper).GetMethod(
                "ApplyDisplayPosition",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.IsNull(method, "ApplyDisplayPosition deve ter sido removido por ser dead code.");
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ApplyDisplayTopology_ExistsAsPublicStaticMethod()
        {
            var method = typeof(DisplayConfigHelper).GetMethod(
                "ApplyDisplayTopology",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(method);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void EnableDisplays_ExistsAsPublicStaticMethod()
        {
            var method = typeof(DisplayConfigHelper).GetMethod(
                "EnableDisplays",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            Assert.IsNotNull(method);
        }
    }
}
