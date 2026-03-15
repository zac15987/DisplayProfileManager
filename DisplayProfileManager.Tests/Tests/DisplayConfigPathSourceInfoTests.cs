using Microsoft.VisualStudio.TestTools.UnitTesting;
using DisplayProfileManager.Helpers;

namespace DisplayProfileManager.Tests.Tests
{
    /// <summary>
    /// Testes para as propriedades de encoding em <see cref="DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO"/>.
    ///
    /// Contexto: modeInfoIdx é um campo de 32 bits com dupla finalidade:
    ///   - Bits 31-16 (superiores): SourceModeInfoIdx — índice no array de modos (0xFFFF = inválido)
    ///   - Bits 15-0  (inferiores): CloneGroupId      — identificador de grupo de clone
    ///
    /// Esta codificação é usada apenas em Phase 1 (SDC_TOPOLOGY_SUPPLIED).
    /// Em Phase 2 (SDC_USE_SUPPLIED_DISPLAY_CONFIG), modeInfoIdx é um índice simples.
    /// </summary>
    [TestClass]
    public class DisplayConfigPathSourceInfoTests
    {
        // ────────────────────────────────────────────────────────────────────
        // Bug #6 — CloneGroupId getter: (modeInfoIdx << 16) >> 16
        //          A expressão funciona mas é obscura; deveria ser modeInfoIdx & 0xFFFF.
        //          Estes testes são de REGRESSÃO: verificam que o comportamento atual
        //          está correto e não muda após a refatoração para a forma mais clara.
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        public void CloneGroupId_Get_ReturnsLower16Bits()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 0xABCD_1234 };

            Assert.AreEqual(0x1234u, src.CloneGroupId);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void CloneGroupId_Get_WhenUpperBitsAreMaxAndLowerAreZero_ReturnsZero()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 0xFFFF_0000 };

            Assert.AreEqual(0u, src.CloneGroupId);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void CloneGroupId_Get_WhenOnlyLowerBitsSet_ReturnsThem()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 0x0000_0005 };

            Assert.AreEqual(5u, src.CloneGroupId);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void CloneGroupId_Set_PreservesExistingSourceModeInfoIdx()
        {
            // SourceModeInfoIdx=0xFFFF (inválido), CloneGroupId=0 → setar CloneGroupId=3
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 0xFFFF_0000 };

            src.CloneGroupId = 3;

            Assert.AreEqual(0xFFFF_0003u, src.modeInfoIdx);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void CloneGroupId_Get_IsEquivalentToMask()
        {
            // Garante que (x << 16) >> 16 == x & 0xFFFF para valores representativos
            uint[] values = { 0x0000_0000, 0x0000_1234, 0xFFFF_0000, 0xABCD_5678, 0xFFFF_FFFF };

            foreach (uint raw in values)
            {
                var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = raw };

                Assert.AreEqual(raw & 0xFFFFu, src.CloneGroupId,
                    $"modeInfoIdx=0x{raw:X8}");
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // SourceModeInfoIdx getter/setter — testes de regressão
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        public void SourceModeInfoIdx_Get_ReturnsUpper16Bits()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 0xABCD_1234 };

            Assert.AreEqual(0xABCDu, src.SourceModeInfoIdx);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void SourceModeInfoIdx_Set_StoresPlainValue()
        {
            // O setter deve armazenar o valor diretamente como índice simples.
            // Phase 1 usa ResetModeAndSetCloneGroup() para o encoding; o setter
            // não precisa preservar CloneGroupId.
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 0xFFFF_0003 };

            src.SourceModeInfoIdx = 2;

            Assert.AreEqual(2u, src.modeInfoIdx);
        }

        // ────────────────────────────────────────────────────────────────────
        // ResetModeAndSetCloneGroup — testes de regressão
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Regression")]
        public void ResetModeAndSetCloneGroup_SetsUpperBitsToInvalid()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO();
            src.ResetModeAndSetCloneGroup(2);

            Assert.AreEqual(0xFFFFu, src.SourceModeInfoIdx);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ResetModeAndSetCloneGroup_SetsLowerBitsToCloneGroup()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO();
            src.ResetModeAndSetCloneGroup(5);

            Assert.AreEqual(5u, src.CloneGroupId);
        }

        [TestMethod]
        [TestCategory("Regression")]
        public void ResetModeAndSetCloneGroup_ProducesExpectedRawValue()
        {
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO();
            src.ResetModeAndSetCloneGroup(3);

            Assert.AreEqual(0xFFFF_0003u, src.modeInfoIdx);
        }

        // ────────────────────────────────────────────────────────────────────
        // Bug #2 — SourceModeInfoIdx setter contamina modeInfoIdx quando usado
        //          no contexto de Phase 2.
        //
        // Contexto: após QueryDisplayConfig, paths[i].sourceInfo.modeInfoIdx contém
        // um índice simples no array de modos (ex: 3), NÃO o encoding de clone group.
        // Phase 2 chama SourceModeInfoIdx = N esperando produzir modeInfoIdx = N,
        // mas o setter preserva os bits inferiores do índice de query como se fossem
        // CloneGroupId, resultando em (N << 16) | queryIndex em vez de N.
        //
        // Estes testes são TDD (falham com o código atual) e passarão após o fix.
        // O fix consiste em Phase 2 setar modeInfoIdx diretamente, sem usar o setter.
        // ────────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TDD")]
        [Description("Bug #2 — Phase 2 deve setar modeInfoIdx como índice simples, " +
                     "não como encoding de clone group. Com o código atual, o setter " +
                     "preserva bits inferiores do valor de query, corrompendo o resultado.")]
        public void SourceModeInfoIdx_WhenModeInfoIdxComesFromQuery_SetsPlainIndex()
        {
            // Arrange: simula paths[i].sourceInfo após QueryDisplayConfig retornar
            // modeInfoIdx=3 (índice simples no array de modos, sem encoding de clone group)
            var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = 3 };

            // Act: Phase 2 atribui o índice do source mode encontrado
            src.SourceModeInfoIdx = 5;

            // Assert: para SDC_USE_SUPPLIED_DISPLAY_CONFIG, modeInfoIdx deve ser o índice puro
            // FALHA atualmente: produz (5 << 16) | 3 = 0x0005_0003 em vez de 5
            Assert.AreEqual(5u, src.modeInfoIdx,
                $"Esperado modeInfoIdx=5, obtido 0x{src.modeInfoIdx:X8}. " +
                "O setter preservou os bits inferiores do índice de query (3) como CloneGroupId.");
        }

        [TestMethod]
        [TestCategory("TDD")]
        [Description("Bug #2 (complementar) — qualquer índice de query não-zero nos bits " +
                     "inferiores contamina o resultado do setter.")]
        public void SourceModeInfoIdx_WhenQueryIndexIsNonZero_DoesNotContaminateResult()
        {
            uint[] queryIndices = { 1, 2, 3, 4, 7, 15 };

            foreach (uint queryIdx in queryIndices)
            {
                var src = new DisplayConfigHelper.DISPLAYCONFIG_PATH_SOURCE_INFO { modeInfoIdx = queryIdx };

                src.SourceModeInfoIdx = 0;

                // FALHA atualmente: (0 << 16) | queryIdx = queryIdx em vez de 0
                Assert.AreEqual(0u, src.modeInfoIdx,
                    $"queryIdx={queryIdx}: esperado modeInfoIdx=0, obtido 0x{src.modeInfoIdx:X8}");
            }
        }
    }
}
