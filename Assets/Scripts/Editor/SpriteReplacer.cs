#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Subak.EditorTools
{
    /// <summary>
    /// 사용자가 만든 PNG들을 일괄로 import 설정하고 각 FruitData의 sprite 슬롯을 교체한다.
    /// 파일명에 stage 번호(01~11) 또는 한/영 과일명이 포함되어 있으면 자동 매칭.
    ///
    /// 메뉴: Tools/Subak/Replace Fruit Sprites...
    ///
    /// 사용 흐름:
    /// 1) Unity Editor의 Project 창에서 Assets/Generated/CustomSprites 같은 폴더 생성
    /// 2) AI/그림판 등으로 만든 11개 PNG를 그 폴더에 저장
    ///    파일명 예: 01_체리.png, fruit_02_strawberry.png, 수박.png, watermelon.png 등
    /// 3) 메뉴 실행 → 폴더 선택 → 매칭 결과 확인 → 교체 확정
    /// </summary>
    public static class SpriteReplacer
    {
        const string DefaultCustomFolder = "Assets/Generated/CustomSprites";
        const string DataFolder = "Assets/Generated/Data";

        // 파일명에 들어있을 수 있는 키워드 → stage
        static readonly Dictionary<string, int> NameToStage = new()
        {
            // 한국어
            { "체리",     1 },
            { "딸기",     2 },
            { "포도",     3 },
            { "데코폰",   4 },
            { "감",       5 },
            { "사과",     6 },
            { "배",       7 },
            { "복숭아",   8 },
            { "파인애플", 9 },
            { "멜론",     10 },
            { "수박",     11 },
            // 영어
            { "cherry",     1 },
            { "strawberry", 2 },
            { "grape",      3 },
            { "dekopon",    4 },
            { "persimmon",  5 },
            { "apple",      6 },
            { "pear",       7 },
            { "peach",      8 },
            { "pineapple",  9 },
            { "melon",      10 },
            { "watermelon", 11 },
        };

        [MenuItem("Tools/Subak/Replace Fruit Sprites...")]
        public static void ReplaceSprites()
        {
            string projectAssets = Application.dataPath; // 절대 경로 (.../Assets)

            // 폴더 선택
            string folder = EditorUtility.OpenFolderPanel(
                "커스텀 스프라이트가 들어있는 폴더 선택", DefaultCustomFolder, "");
            if (string.IsNullOrEmpty(folder)) return;

            if (!folder.StartsWith(projectAssets))
            {
                EditorUtility.DisplayDialog("Subak",
                    "프로젝트의 Assets/ 폴더 내부를 선택해야 해요.\n" +
                    "예: Assets/Generated/CustomSprites/",
                    "확인");
                return;
            }

            string relativeFolder = "Assets" + folder.Substring(projectAssets.Length);

            // 폴더 내 PNG 수집
            var pngFiles = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
            if (pngFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Subak", "선택한 폴더에 PNG 파일이 없어요.", "확인");
                return;
            }

            // 파일명 → stage 매칭
            var stageToFile = new Dictionary<int, string>();
            var unmatched = new List<string>();

            foreach (var fullPath in pngFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(fullPath).ToLowerInvariant();
                int stage = MatchStage(filename);
                if (stage > 0 && !stageToFile.ContainsKey(stage))
                {
                    stageToFile[stage] = fullPath;
                }
                else
                {
                    unmatched.Add(Path.GetFileName(fullPath));
                }
            }

            // 매칭 결과 리포트
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"매칭됨 {stageToFile.Count}/11개");
            sb.AppendLine();
            string[] names =
                { "체리", "딸기", "포도", "데코폰", "감", "사과", "배", "복숭아", "파인애플", "멜론", "수박" };
            for (int s = 1; s <= 11; s++)
            {
                if (stageToFile.ContainsKey(s))
                    sb.AppendLine($"  ✓ {s:00} {names[s - 1]}: {Path.GetFileName(stageToFile[s])}");
                else
                    sb.AppendLine($"  ✗ {s:00} {names[s - 1]}: (없음)");
            }
            if (unmatched.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("매칭 안 된 파일:");
                foreach (var u in unmatched) sb.AppendLine($"  - {u}");
            }

            if (!EditorUtility.DisplayDialog("Subak — 매칭 결과",
                sb.ToString() + "\n계속해서 교체할까요?", "교체", "취소"))
                return;

            // FruitData 미리 모아두기 (stage → FruitData)
            var stageToData = new Dictionary<int, FruitData>();
            string[] dataGuids = AssetDatabase.FindAssets("t:FruitData", new[] { DataFolder });
            foreach (var guid in dataGuids)
            {
                var dataPath = AssetDatabase.GUIDToAssetPath(guid);
                var d = AssetDatabase.LoadAssetAtPath<FruitData>(dataPath);
                if (d != null) stageToData[d.stage] = d;
            }

            // 각 매칭된 파일 import + sprite 슬롯 교체
            int replaced = 0;
            foreach (var kvp in stageToFile)
            {
                int stage = kvp.Key;
                string fullPath = kvp.Value;
                string relPath = "Assets" + fullPath.Substring(projectAssets.Length);

                // PNG 임포트 설정
                AssetDatabase.ImportAsset(relPath);
                var importer = AssetImporter.GetAtPath(relPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    // 512x512 가정 시 PPU=512이면 sprite world width = 1 unit.
                    // 텍스처 크기를 모를 수 있으니 max size 기준 PPU 자동 계산
                    importer.spritePixelsPerUnit = 512f;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(relPath);
                if (sprite == null) continue;

                if (stageToData.TryGetValue(stage, out var data))
                {
                    data.sprite = sprite;
                    data.tint = Color.white; // tint 리셋해서 원본 색 보존
                    EditorUtility.SetDirty(data);
                    replaced++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Subak",
                $"{replaced}개 FruitData의 sprite 슬롯이 교체됐어요.\n" +
                $"폴더: {relativeFolder}\n" +
                "▶️ Play로 확인해보세요.",
                "확인");
        }

        /// <summary>
        /// 파일명에서 stage 추출. 우선순위:
        /// 1) 숫자 토큰이 1~11 범위면 그것
        /// 2) 한/영 과일명 키워드가 있으면 그것
        /// 매칭 실패 시 0 리턴.
        /// </summary>
        static int MatchStage(string filename)
        {
            // 1) 숫자 토큰
            var digits = new System.Text.StringBuilder();
            foreach (var ch in filename)
            {
                if (char.IsDigit(ch))
                {
                    digits.Append(ch);
                }
                else if (digits.Length > 0)
                {
                    if (int.TryParse(digits.ToString(), out int n) && n >= 1 && n <= 11) return n;
                    digits.Clear();
                }
            }
            if (digits.Length > 0)
            {
                if (int.TryParse(digits.ToString(), out int n) && n >= 1 && n <= 11) return n;
            }

            // 2) 키워드 (긴 이름 우선 - "복숭아"가 "복" 보다 먼저)
            foreach (var kvp in NameToStage)
            {
                if (filename.Contains(kvp.Key)) return kvp.Value;
            }
            return 0;
        }
    }
}
#endif
