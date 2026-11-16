# Test Components Catalog

Complete inventory of Grasshopper test components in `SmartHopper.Components.Test` (not built in Release). Display names follow the `Test …` ribbon naming; files are grouped by source folder.

## Metadata

| Property | Value |
| --- | --- |
| **Source Code** | `src/SmartHopper.Components.Test/` |
| **Component count** | 87 (+ `ProviderTestComponentBase` abstract base) |
| **Last Updated** | 2026-11-10 |

## AiTools — MCP/AI-tool harness components (11)

| Component file | Component GUID |
| --- | --- |
| `TestCanvasProtectionComponent` | `90D18698-28D4-4978-B30B-579D99E6D6DD` |
| `TestGhComponentLockComponent` | `EFC99FCE-9C63-4FF8-AB1B-E70EF75804B2` |
| `TestGhComponentPreviewComponent` | `5C56677D-6603-4C00-8D9D-CB14A701A0F2` |
| `TestGhGetPutComponent` | `6BBDEF7C-1616-475F-8337-7B40852B2625` |
| `TestGhGroupComponent` | `7BFEF496-71D4-47BB-8A5B-C3D9DA564B0F` |
| `TestGhJsonLayoutComponent` | `55047F7B-BCFB-4915-8275-96FCFFFB3CB4` |
| `TestGhMoveComponent` | `79F7DE44-9A14-4140-B1CA-C652852068AF` |
| `TestGhRunComponent` | `B5C8D3F4-5E6A-4B7C-9D0E-1F2A3B4C5D6E` |
| `TestGhSelectComponent` | `969D3770-0657-494E-874C-5A1D37226C50` |
| `TestGhSetValueComponent` | `A3B7C1E2-4D5F-4A6B-8C9D-0E1F2A3B4C5D` |
| `TestSmarthopperReadmeComponent` | `9BCF1E9D-0FDE-49EB-8BE6-7B0881350CBB` |

## Badges — visual badge/state indicators (3)

| Component file | Component GUID |
| --- | --- |
| `TestBadgesOneComponent` | `2B5E5F5A-6F4D-4C0F-8D99-0E9A04BB33A1` |
| `TestBadgesTwoComponent` | `A1A8E7D1-5F40-4F8C-8D8C-2E7DE55F6B22` |
| `TestBadgesThreeComponent` | `5D9A39A7-9F1B-4F2E-9D6A-9B9D3F93A0B7` |

## DataProcessor — data-tree branch-mapping matrix (24)

Covers the `DataTreeProcessor` contract: equal/different paths, broadcast, graft/flatten, grouping, direct-match precedence, GH_Path/GH_Structure equality.

| Component file | Component GUID |
| --- | --- |
| `DataTreeProcessorBranchFlattenTestComponent` | `E9642177-D368-4E9D-9BD6-E84C46D0958F` |
| `DataTreeProcessorBranchToBranchTestComponent` | `4FBE8A03-5A39-4C99-B190-F95468A0D3AC` |
| `DataTreeProcessorBroadcastDeeperDiffRootTestComponent` | `D659A768-A076-4946-80B9-A8AD99D4F740` |
| `DataTreeProcessorBroadcastDeeperSameRootTestComponent` | `0E1105D8-1EA0-446B-B51D-F90D1EC29342` |
| `DataTreeProcessorBroadcastMultipleNoZeroTestComponent` | `11287A68-04D7-46F4-99DE-C5B0C45F0732` |
| `DataTreeProcessorBroadcastMultipleTopLevelTestComponent` | `CBD8E900-B6EF-4ADE-B68C-A1A6AB486647` |
| `DataTreeProcessorDifferentPathsFirstOneSecondThreeTestComponent` | `A8D1E0F3-3C2B-4E1E-9B3F-1A2C3D4E5F60` |
| `DataTreeProcessorDifferentPathsFirstThreeSecondOneTestComponent` | `7A6E5F0B-9D3C-4A0C-8B2E-1F3A4D5C6B7E` |
| `DataTreeProcessorDifferentPathsOneItemEachTestComponent` | `F26E3A5B-2EFD-4F7B-8D8A-7C9A6B6882A2` |
| `DataTreeProcessorDifferentPathsThreeItemsEachTestComponent` | `5A7B9B0C-12D0-4B90-AE17-5D1F764C6C5A` |
| `DataTreeProcessorDirectMatchPrecedenceTestComponent` | `77095C92-474F-4D5C-9EA6-6FE31FFFA710` |
| `DataTreeProcessorEqualPathsFirstOneSecondThreeTestComponent` | `0C6B2C9E-2D68-45AC-A2D8-7B2E5F97F9C3` |
| `DataTreeProcessorEqualPathsFirstThreeSecondOneTestComponent` | `B3C7D9E1-4A5B-4F2C-8B1D-2E3F4A5B6C7D` |
| `DataTreeProcessorEqualPathsTestComponent` | `B0C2B1B7-3A6C-46A5-9E52-9F9E4F6B7C11` |
| `DataTreeProcessorEqualPathsThreeItemsTestComponent` | `1F4D5C1B-8E6D-49B4-B55F-1A3F5E2E6B31` |
| `DataTreeProcessorGroupIdenticalTestComponent` | `CEF161C9-36FC-4503-8BB0-9717EEA13865` |
| `DataTreeProcessorItemGraftTestComponent` | `3B09EE1F-00A3-4B7D-86DA-4C7EB0C6C0C3` |
| `DataTreeProcessorItemToItemTestComponent` | `C4D5E6F7-8A9B-4C0D-9E1F-2A3B4C5D6E7F` |
| `DataTreeProcessorMixedDepthsTestComponent` | `F788712F-B2B3-4131-87CA-E654F6153339` |
| `DataTreeProcessorRule2OverrideTestComponent` | `FE2E0986-FFAF-4A64-9F97-0FD3F4E571D8` |
| `GH_Path_EqualityTestComponent` | `E19C7C94-E670-4F97-8061-CE81078C29B7` |
| `GH_StructureTestComponent` | `0069CD6E-C135-4D2E-85F3-78A7FC498F82` |
| `GH_Structure_WithStringsTestComponent` | `561D6314-0C04-4DC3-AB1F-8C50202E9C75` |

## Misc — state machine, async worker, dialog tests (7)

| Component file | Component GUID |
| --- | --- |
| `StyledMessageDialogTestComponent` | `F5BD0E33-7D54-4273-A4AD-94928BB1EF62` |
| `TestAIStatefulTreePrimeCalculatorComponent` | `C349BCD4-81C8-40CC-B449-DDE7C9180CAA` |
| `TestAsyncPrimeCalculatorComponent` | `B2C612B0-2C57-47CE-B9FE-E10621F18934` |
| `TestStatefulPrimeCalculatorComponent` | `C2C612B0-2C57-47CE-B9FE-E10621F18935` |
| `TestStatefulTreePrimeCalculatorComponent` | `E2DB56F0-C597-432C-9774-82DF431CC848` |
| `TestStateManagerDebounceComponent` | `F4C612B0-2C57-47CE-B9FE-E10621F18937` |
| `TestStateManagerRestorationComponent` | `E3C612B0-2C57-47CE-B9FE-E10621F18936` |

## OutputAdapters (1)

| Component file | Component GUID |
| --- | --- |
| `OutputAdapterTreeGatheringTestComponent` | `7C2E4A6B-1D8F-4C3A-9E5B-0F2D7A4C6B8E` |

## Providers — per-provider contract suite (42)

Six variants per provider: `StandardCall` (sync path), `BatchCall` (`IAIBatchProvider`), `Cancellation`, `Decode`, `Encode`, `Tools`, `Vision`. Ribbon: `SmartHopper Tests → Testing Providers`, displayed as `Test <Provider> <Variant>`.

| Component | GUID |
| --- | --- |
| `TestAnthropicStandardCallComponent` | `96E4F7C5-B7E4-4C0E-8CE4-538D50D724E5` |
| `TestAnthropicBatchCallComponent` | `963561E5-8242-4B9F-AF16-16635E173401` |
| `TestAnthropicCancellationComponent` | `D5B281DA-988B-4E6D-9F76-A905DC12EAFA` |
| `TestAnthropicDecodeComponent` | `9B40A72D-F7AA-4C37-BFFF-A3C58DA6838F` |
| `TestAnthropicEncodeComponent` | `817E8806-0DE0-4104-A650-1D28D7763E93` |
| `TestAnthropicToolsComponent` | `8C2D73B5-34DA-41A9-BECB-3F7F33766FAF` |
| `TestAnthropicVisionComponent` | `B13ECB85-AFE7-496C-AF1D-D3F874F4556D` |
| `TestDeepSeekStandardCallComponent` | `67D39B8C-9DCB-4E81-8FBA-BD74FF2A2B1F` |
| `TestDeepSeekBatchCallComponent` | `9D264C28-E309-47BA-894C-7571AA3CBE3F` |
| `TestDeepSeekCancellationComponent` | `E6B02D63-FAF8-43F5-AAD6-71A3BA70A79A` |
| `TestDeepSeekDecodeComponent` | `F4B36D4B-7037-440F-A9E6-B7791C622AC5` |
| `TestDeepSeekEncodeComponent` | `BC0FB82E-85A6-4706-AFFA-69A740D173E4` |
| `TestDeepSeekToolsComponent` | `9CD3C815-439A-4F4B-B9E4-998833232858` |
| `TestDeepSeekVisionComponent` | `43A8D588-79AD-4F81-B92A-2FC8CEA04682` |
| `TestGeminiStandardCallComponent` | `8F3D9C89-8676-41B8-9D14-39488727E533` |
| `TestGeminiBatchCallComponent` | `C352A91F-366B-4E84-853F-7EFB928B5388` |
| `TestGeminiCancellationComponent` | `67A7822D-BEC2-4920-90DC-E4FA3A113F96` |
| `TestGeminiDecodeComponent` | `BA230457-1318-4346-A6F3-453F32EDBD38` |
| `TestGeminiEncodeComponent` | `BFA00A8F-3DA2-4261-87A6-DBCE0E861188` |
| `TestGeminiFunctionCallingComponent` | `A12F1236-3B3B-498D-9DB9-DC94FEFB0660` |
| `TestGeminiToolsComponent` | `4A7B2C9D-1E3F-4A5B-8C6D-9E0F1A2B3C4D` |
| `TestGeminiVisionComponent` | `06FB22C2-2C27-4117-8330-56904038A513` |
| `TestMistralAIStandardCallComponent` | `17302B7A-B89E-480B-AE4D-11006CED1DF9` |
| `TestMistralAIBatchCallComponent` | `37AF00FA-75EA-4512-A2BA-95EF1E0D2764` |
| `TestMistralAICancellationComponent` | `DC5C78E6-CE9A-4171-BA76-772BA553AFAE` |
| `TestMistralAIDecodeComponent` | `8D2545BA-C1BC-45DD-B94B-A07838C00B15` |
| `TestMistralAIEncodeComponent` | `495A06B5-DE3F-4860-AC3B-E97A34921C29` |
| `TestMistralAIToolsComponent` | `5160C10D-CB11-48EA-9CAB-099F56D560AC` |
| `TestMistralAIVisionComponent` | `B2C7D4E8-1234-4ABC-9DEF-123456789ABC` |
| `TestOpenAIStandardCallComponent` | `70B1A4C5-DDAE-40B0-BCDD-509F06B607C5` |
| `TestOpenAIBatchCallComponent` | `2B36EFF6-46F3-4CAB-B032-369D0102D954` |
| `TestOpenAICancellationComponent` | `30DE33AE-6039-4D91-A527-7707AA4084DE` |
| `TestOpenAIDecodeComponent` | `6FBC99E6-AF7B-4390-B1EA-5B7F65E2C7EA` |
| `TestOpenAIEncodeComponent` | `AD538781-65B9-4123-B4EE-874D03BD6FC3` |
| `TestOpenAIToolsComponent` | `63FEAE05-B951-4642-8BC4-8D5F1A1FAAD5` |
| `TestOpenAIVisionComponent` | `D4E5F6A7-5678-4DEF-A012-345678901234` |
| `TestOpenRouterStandardCallComponent` | `B5D9A785-3865-4978-9D0E-416B38F5447D` |
| `TestOpenRouterBatchCallComponent` | `D16711BF-CB36-4E28-A12C-6A81AB8550B4` |
| `TestOpenRouterCancellationComponent` | `35D8D280-7901-475E-BF45-86814ABD6266` |
| `TestOpenRouterDecodeComponent` | `0DF2FEEA-79E4-423B-91C5-3806F4EADC3E` |
| `TestOpenRouterEncodeComponent` | `04900353-70D4-4F0E-96E1-5EE2BD0C2418` |
| `TestOpenRouterToolsComponent` | `84F357D4-3A8E-47F6-9D11-CB512F152301` |
| `TestOpenRouterVisionComponent` | `A3845F33-35BD-463F-923C-68A60CCF96A0` |

## Notes

- `ProviderTestComponentBase` (`Providers/`) is the shared abstract base — no `ComponentGuid`.
- All components register `GH_Exposure.hidden`/septenary-level exposure and the `SmartHopper Tests` category; they exist to exercise Rhino/GH-dependent paths that xUnit cannot reach (per `.devin/rules/testing-and-build.md`).
