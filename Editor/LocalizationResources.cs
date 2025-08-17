using System.Collections.Generic;

namespace MeshUVMaskGenerator
{
    public static class LocalizationResources
    {
        private static readonly Dictionary<string, Dictionary<SupportedLanguage, string>> resources = 
            new Dictionary<string, Dictionary<SupportedLanguage, string>>
            {
                // ウィンドウ・ヘッダー
                ["window.title"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "Mesh UV Mask Generator",
                    [SupportedLanguage.English] = "Mesh UV Mask Generator",
                    [SupportedLanguage.Korean] = "Mesh UV Mask Generator"
                },
                
                ["header.newVersionAvailable"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "新しいバージョンが利用可能です",
                    [SupportedLanguage.English] = "New version available",
                    [SupportedLanguage.Korean] = "새 버전을 사용할 수 있습니다"
                },
                
                ["button.openReleasePage"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "リリースページを開く",
                    [SupportedLanguage.English] = "Open Release Page",
                    [SupportedLanguage.Korean] = "릴리스 페이지 열기"
                },

                // オブジェクト選択
                ["label.selectedObject"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "選択中のオブジェクト",
                    [SupportedLanguage.English] = "Selected Object",
                    [SupportedLanguage.Korean] = "선택된 오브젝트"
                },
                
                ["help.selectMeshObject"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "MeshFilterまたはSkinnedMeshRendererを持つオブジェクトを選択してください。",
                    [SupportedLanguage.English] = "Please select an object with MeshFilter or SkinnedMeshRenderer.",
                    [SupportedLanguage.Korean] = "MeshFilter 또는 SkinnedMeshRenderer가 있는 오브젝트를 선택하세요."
                },

                // タブ
                ["tab.meshSelection"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "メッシュ選択",
                    [SupportedLanguage.English] = "Mesh Selection",
                    [SupportedLanguage.Korean] = "메시 선택"
                },
                
                ["tab.export"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "エクスポート",
                    [SupportedLanguage.English] = "Export",
                    [SupportedLanguage.Korean] = "내보내기"
                },

                // メッシュ選択タブ
                ["label.materialSelection"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "マテリアル選択",
                    [SupportedLanguage.English] = "Material Selection",
                    [SupportedLanguage.Korean] = "머티리얼 선택"
                },
                
                ["label.selectedMeshes"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "選択中のメッシュ: {0:N0}",
                    [SupportedLanguage.English] = "Selected Meshes: {0:N0}",
                    [SupportedLanguage.Korean] = "선택된 메시: {0:N0}"
                },
                
                ["help.noMeshSelected"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "メッシュ未選択。選択されたマテリアル「{0}」のサブメッシュが表示されます。",
                    [SupportedLanguage.English] = "No mesh selected. Sub-mesh of selected material '{0}' will be displayed.",
                    [SupportedLanguage.Korean] = "메시가 선택되지 않았습니다. 선택된 머티리얼 '{0}'의 서브 메시가 표시됩니다."
                },
                
                ["help.meshesSelected"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "{0:N0}個のメッシュが選択されています。",
                    [SupportedLanguage.English] = "{0:N0} meshes are selected.",
                    [SupportedLanguage.Korean] = "{0:N0}개의 메시가 선택되었습니다."
                },

                // 選択モード
                ["label.selectionMode"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "選択モード",
                    [SupportedLanguage.English] = "Selection Mode",
                    [SupportedLanguage.Korean] = "선택 모드"
                },
                
                ["button.individualMesh"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "個別メッシュ",
                    [SupportedLanguage.English] = "Individual Mesh",
                    [SupportedLanguage.Korean] = "개별 메시"
                },
                
                ["button.uvIsland"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "UVアイランド",
                    [SupportedLanguage.English] = "UV Island",
                    [SupportedLanguage.Korean] = "UV 아일랜드"
                },
                
                ["label.uvThreshold"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "UV閾値",
                    [SupportedLanguage.English] = "UV Threshold",
                    [SupportedLanguage.Korean] = "UV 임계값"
                },
                
                ["help.uvIslandMode"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "UVアイランドモード: 1つのメッシュをクリックするとUV空間で接続されたすべてのメッシュが選択されます",
                    [SupportedLanguage.English] = "UV Island Mode: Clicking one mesh selects all connected meshes in UV space",
                    [SupportedLanguage.Korean] = "UV 아일랜드 모드: 하나의 메시를 클릭하면 UV 공간에서 연결된 모든 메시가 선택됩니다"
                },
                
                ["help.individualMode"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "個別メッシュモード: クリックしたメッシュのみが選択されます",
                    [SupportedLanguage.English] = "Individual Mesh Mode: Only the clicked mesh is selected",
                    [SupportedLanguage.Korean] = "개별 메시 모드: 클릭한 메시만 선택됩니다"
                },
                
                ["button.addMode"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "追加モード",
                    [SupportedLanguage.English] = "Add Mode",
                    [SupportedLanguage.Korean] = "추가 모드"
                },
                
                ["button.removeMode"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "削除モード",
                    [SupportedLanguage.English] = "Remove Mode",
                    [SupportedLanguage.Korean] = "제거 모드"
                },

                // 選択操作ボタン
                ["button.selectAll"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "全選択",
                    [SupportedLanguage.English] = "Select All",
                    [SupportedLanguage.Korean] = "모두 선택"
                },
                
                ["button.clearSelection"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "選択解除",
                    [SupportedLanguage.English] = "Clear Selection",
                    [SupportedLanguage.Korean] = "선택 해제"
                },
                
                ["button.invertSelection"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "選択反転",
                    [SupportedLanguage.English] = "Invert Selection",
                    [SupportedLanguage.Korean] = "선택 반전"
                },
                
                ["help.sceneViewInstruction"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "Scene Viewでメッシュをクリックして選択/解除",
                    [SupportedLanguage.English] = "Click meshes in Scene View to select/deselect",
                    [SupportedLanguage.Korean] = "Scene View에서 메시를 클릭하여 선택/해제"
                },

                // エクスポートタブ
                ["label.uvSettingsAndExport"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "UV設定とエクスポート",
                    [SupportedLanguage.English] = "UV Settings and Export",
                    [SupportedLanguage.Korean] = "UV 설정 및 내보내기"
                },
                
                ["label.uvSettings"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "UV設定",
                    [SupportedLanguage.English] = "UV Settings",
                    [SupportedLanguage.Korean] = "UV 설정"
                },
                
                ["label.textureSize"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "テクスチャサイズ",
                    [SupportedLanguage.English] = "Texture Size",
                    [SupportedLanguage.Korean] = "텍스처 크기"
                },
                
                ["label.backgroundColor"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "背景色",
                    [SupportedLanguage.English] = "Background Color",
                    [SupportedLanguage.Korean] = "배경색"
                },
                
                ["label.maskColor"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "マスク色",
                    [SupportedLanguage.English] = "Mask Color",
                    [SupportedLanguage.Korean] = "마스크 색상"
                },
                
                ["label.fillPolygons"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "ポリゴン塗りつぶし",
                    [SupportedLanguage.English] = "Fill Polygons",
                    [SupportedLanguage.Korean] = "폴리곤 채우기"
                },
                
                ["label.lineThickness"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "線の太さ",
                    [SupportedLanguage.English] = "Line Thickness",
                    [SupportedLanguage.Korean] = "선 두께"
                },

                // エクスポート
                ["label.export"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "エクスポート",
                    [SupportedLanguage.English] = "Export",
                    [SupportedLanguage.Korean] = "내보내기"
                },
                
                ["label.generatedSize"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "生成サイズ: {0}x{1}",
                    [SupportedLanguage.English] = "Generated Size: {0}x{1}",
                    [SupportedLanguage.Korean] = "생성 크기: {0}x{1}"
                },
                
                ["label.mesh"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "メッシュ: {0}",
                    [SupportedLanguage.English] = "Mesh: {0}",
                    [SupportedLanguage.Korean] = "메시: {0}"
                },
                
                ["button.saveTexture"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "テクスチャを保存",
                    [SupportedLanguage.English] = "Save Texture",
                    [SupportedLanguage.Korean] = "텍스처 저장"
                },
                
                ["help.previewNotGenerated"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "プレビューが生成されていません。",
                    [SupportedLanguage.English] = "Preview has not been generated.",
                    [SupportedLanguage.Korean] = "미리보기가 생성되지 않았습니다."
                },

                // プレビュー
                ["label.preview"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "プレビュー",
                    [SupportedLanguage.English] = "Preview",
                    [SupportedLanguage.Korean] = "미리보기"
                },
                
                ["help.previewNotGenerated2"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "プレビュー未生成",
                    [SupportedLanguage.English] = "Preview not generated",
                    [SupportedLanguage.Korean] = "미리보기 미생성"
                },

                // エラーメッセージ
                ["error.updateCheckFailed"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "アップデート確認に失敗: {0}",
                    [SupportedLanguage.English] = "Update check failed: {0}",
                    [SupportedLanguage.Korean] = "업데이트 확인 실패: {0}"
                },

                // バージョン情報
                ["label.currentToLatest"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "現在: {0} → 最新: {1}",
                    [SupportedLanguage.English] = "Current: {0} → Latest: {1}",
                    [SupportedLanguage.Korean] = "현재: {0} → 최신: {1}"
                },

                // マテリアル情報
                ["label.materials"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "マテリアル ({0}個)",
                    [SupportedLanguage.English] = "Materials ({0})",
                    [SupportedLanguage.Korean] = "머티리얼 ({0}개)"
                },
                
                ["label.selectedMaterial"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "選択中: {0}",
                    [SupportedLanguage.English] = "Selected: {0}",
                    [SupportedLanguage.Korean] = "선택됨: {0}"
                },
                
                ["label.material"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "マテリアル {0}",
                    [SupportedLanguage.English] = "Material {0}",
                    [SupportedLanguage.Korean] = "머티리얼 {0}"
                },

                // 言語選択
                ["label.language"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "言語",
                    [SupportedLanguage.English] = "Language",
                    [SupportedLanguage.Korean] = "언어"
                },

                // ファイル操作
                ["error.noTextureToSave"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "保存するテクスチャがありません。",
                    [SupportedLanguage.English] = "No texture to save.",
                    [SupportedLanguage.Korean] = "저장할 텍스처가 없습니다."
                },
                
                ["log.uvMaskSaved"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "UVマスクを保存しました: {0}",
                    [SupportedLanguage.English] = "UV mask saved: {0}",
                    [SupportedLanguage.Korean] = "UV 마스크를 저장했습니다: {0}"
                },
                
                ["error.textureSaveFailed"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "テクスチャの保存に失敗しました: {0}",
                    [SupportedLanguage.English] = "Failed to save texture: {0}",
                    [SupportedLanguage.Korean] = "텍스처 저장에 실패했습니다: {0}"
                },

                // ダイアログ
                ["dialog.saveTexture"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "テクスチャを保存",
                    [SupportedLanguage.English] = "Save Texture",
                    [SupportedLanguage.Korean] = "텍스처 저장"
                },

                // ログメッセージ
                ["log.newVersionAvailable"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "[Mesh UV Mask Generator] 新しいバージョンが利用可能です: {0} → {1}",
                    [SupportedLanguage.English] = "[Mesh UV Mask Generator] New version available: {0} → {1}",
                    [SupportedLanguage.Korean] = "[Mesh UV Mask Generator] 새 버전을 사용할 수 있습니다: {0} → {1}"
                },
                
                ["log.updateCheckFailed"] = new Dictionary<SupportedLanguage, string>
                {
                    [SupportedLanguage.Japanese] = "[Mesh UV Mask Generator] アップデート確認に失敗: {0}",
                    [SupportedLanguage.English] = "[Mesh UV Mask Generator] Update check failed: {0}",
                    [SupportedLanguage.Korean] = "[Mesh UV Mask Generator] 업데이트 확인 실패: {0}"
                }
            };

        public static string GetText(string key, SupportedLanguage language = SupportedLanguage.Japanese)
        {
            if (resources.TryGetValue(key, out var languageDict))
            {
                if (languageDict.TryGetValue(language, out var text))
                {
                    return text;
                }
                
                // フォールバック: 日本語
                if (languageDict.TryGetValue(SupportedLanguage.Japanese, out var fallbackText))
                {
                    return fallbackText;
                }
            }

            // キーが見つからない場合
            return $"[{key}]";
        }
    }
}