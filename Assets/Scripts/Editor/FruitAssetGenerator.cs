#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Subak.EditorTools
{
    /// <summary>
    /// 11개 과일의 (1) 단색 원 스프라이트, (2) FruitData ScriptableObject,
    /// (3) FruitDatabase ScriptableObject를 한 번에 생성하는 에디터 유틸리티.
    /// 메뉴: Tools/Subak/Generate Fruit Assets
    /// 결과 경로: Assets/Generated/{Sprites, Data}
    /// 1단계 검증용 플레이스홀더 — 2단계에서 실제 일러스트로 교체.
    /// </summary>
    public static class FruitAssetGenerator
    {
        const string RootFolder = "Assets/Generated";
        const string SpritesFolder = "Assets/Generated/Sprites";
        const string DataFolder = "Assets/Generated/Data";

        // GDD 기준 11단계 데이터 (이름, 색상, 반지름)
        static readonly (string name, Color color, float radius)[] Fruits = new (string, Color, float)[]
        {
            ("체리",     new Color(0.86f, 0.16f, 0.27f), 0.20f),
            ("딸기",     new Color(0.96f, 0.30f, 0.43f), 0.26f),
            ("포도",     new Color(0.51f, 0.31f, 0.72f), 0.32f),
            ("데코폰",   new Color(1.00f, 0.61f, 0.20f), 0.40f),
            ("감",       new Color(0.95f, 0.45f, 0.18f), 0.48f),
            ("사과",     new Color(0.86f, 0.20f, 0.20f), 0.58f),
            ("배",       new Color(0.92f, 0.85f, 0.35f), 0.70f),
            ("복숭아",   new Color(1.00f, 0.72f, 0.78f), 0.84f),
            ("파인애플", new Color(0.98f, 0.82f, 0.30f), 1.00f),
            ("멜론",     new Color(0.55f, 0.78f, 0.40f), 1.18f),
            ("수박",     new Color(0.20f, 0.55f, 0.25f), 1.40f),
        };

        [MenuItem("Tools/Subak/Generate Fruit Assets")]
        public static void Generate()
        {
            EnsureFolders();

            var sprites = new Sprite[Fruits.Length];
            for (int i = 0; i < Fruits.Length; i++)
            {
                sprites[i] = CreateCircleSpriteAsset(i + 1, Fruits[i].color);
            }

            var datas = new FruitData[Fruits.Length];
            for (int i = 0; i < Fruits.Length; i++)
            {
                int stage = i + 1;
                var path = $"{DataFolder}/FruitData_{stage:00}_{Fruits[i].name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<FruitData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<FruitData>();
                    AssetDatabase.CreateAsset(data, path);
                }
                data.stage = stage;
                data.displayName = Fruits[i].name;
                data.radius = Fruits[i].radius;
                data.score = FruitData.TriangularScore(stage);
                data.sprite = sprites[i];
                data.tint = Color.white;
                EditorUtility.SetDirty(data);
                datas[i] = data;
            }

            var dbPath = $"{DataFolder}/FruitDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<FruitDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<FruitDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            db.fruits = datas;
            db.maxNaturalDropStage = 5;
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Subak",
                $"과일 에셋 생성 완료\n- 스프라이트 {sprites.Length}개\n- FruitData {datas.Length}개\n- FruitDatabase 1개\n경로: {RootFolder}",
                "확인");
            Selection.activeObject = db;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(SpritesFolder))
                AssetDatabase.CreateFolder(RootFolder, "Sprites");
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder(RootFolder, "Data");
        }

        /// <summary>단색 원 스프라이트를 PNG로 저장하고 임포트 설정 후 로드해서 리턴.</summary>
        static Sprite CreateCircleSpriteAsset(int stage, Color color)
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            Vector2 c = new(size * 0.5f, size * 0.5f);
            float r = size * 0.48f;
            float rim = r * 0.92f;
            Color shade = Color.Lerp(color, Color.black, 0.25f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    if (d > r) pixels[y * size + x] = new Color(0, 0, 0, 0);
                    else if (d > rim) pixels[y * size + x] = shade;
                    else
                    {
                        float hl = Mathf.Clamp01(1f - Vector2.Distance(
                            new Vector2(x, y),
                            new Vector2(c.x - r * 0.35f, c.y + r * 0.35f)) / (r * 0.55f));
                        pixels[y * size + x] = Color.Lerp(color, Color.white, hl * 0.35f);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            var path = $"{SpritesFolder}/Fruit_{stage:00}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size; // 1 unit = 한 변
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
