#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subak.EditorTools
{
    /// <summary>
    /// 현재 씬에 카메라/통/벽/데드라인/GameManager/Spawner를 자동 배치하고
    /// 모든 인스펙터 참조를 와이어링한다. HUD는 3단계 PR에서 추가.
    /// 메뉴: Tools/Subak/Build Scene
    /// </summary>
    public static class SceneBootstrapper
    {
        const string PrefabFolder = "Assets/Generated/Prefabs";
        const string FruitPrefabPath = "Assets/Generated/Prefabs/Fruit.prefab";

        [MenuItem("Tools/Subak/Build Scene")]
        public static void BuildScene()
        {
            // 1) 데이터베이스 보장
            var dbPath = "Assets/Generated/Data/FruitDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<FruitDatabase>(dbPath);
            if (db == null)
            {
                if (EditorUtility.DisplayDialog("Subak",
                    "FruitDatabase가 없어요. 먼저 'Generate Fruit Assets'를 실행할까요?",
                    "네", "아니오"))
                {
                    FruitAssetGenerator.Generate();
                    db = AssetDatabase.LoadAssetAtPath<FruitDatabase>(dbPath);
                }
                if (db == null) return;
            }

            // 2) Fruit 프리팹 보장
            var fruitPrefab = EnsureFruitPrefab();

            // 3) 카메라
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.transform.position = new Vector3(0f, 1f, -10f);
            cam.backgroundColor = new Color(0.98f, 0.94f, 0.85f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // 4) 통(컨테이너) — 좌/우/바닥 벽 + 데드라인
            var container = GameObject.Find("Container");
            if (container != null) Object.DestroyImmediate(container);
            container = new GameObject("Container");

            float wallThickness = 0.3f;
            float halfWidth = 2.0f;
            float floorY = -3.5f;
            float wallHeight = 7f;

            CreateWall(container.transform, "Wall_Left",
                new Vector2(-halfWidth - wallThickness * 0.5f, floorY + wallHeight * 0.5f),
                new Vector2(wallThickness, wallHeight));
            CreateWall(container.transform, "Wall_Right",
                new Vector2(halfWidth + wallThickness * 0.5f, floorY + wallHeight * 0.5f),
                new Vector2(wallThickness, wallHeight));
            CreateWall(container.transform, "Floor",
                new Vector2(0f, floorY - wallThickness * 0.5f),
                new Vector2(halfWidth * 2f + wallThickness * 2f, wallThickness));

            // 데드라인 트리거 + 가시화 라인
            var deadlineGO = new GameObject("Deadline");
            deadlineGO.transform.SetParent(container.transform);
            deadlineGO.transform.position = new Vector2(0f, floorY + wallHeight - 0.5f);
            var deadlineCol = deadlineGO.AddComponent<BoxCollider2D>();
            deadlineCol.size = new Vector2(halfWidth * 2f, 0.5f);
            deadlineCol.offset = new Vector2(0f, 0.25f);
            deadlineCol.isTrigger = true;
            var lineSR = deadlineGO.AddComponent<SpriteRenderer>();
            lineSR.sprite = CreatePixelSprite();
            lineSR.color = new Color(0.95f, 0.35f, 0.35f, 0.8f);
            lineSR.drawMode = SpriteDrawMode.Sliced;
            lineSR.size = new Vector2(halfWidth * 2f, 0.04f);
            lineSR.sortingOrder = 20;
            deadlineGO.AddComponent<DeadlineDetector>();

            // 5) GameManager
            var gmGO = GameObject.Find("GameManager");
            if (gmGO == null) gmGO = new GameObject("GameManager");
            var gm = gmGO.GetComponent<GameManager>();
            if (gm == null) gm = gmGO.AddComponent<GameManager>();
            gm.database = db;
            gm.fruitPrefab = fruitPrefab;

            // 6) FruitSpawner
            var spGO = GameObject.Find("FruitSpawner");
            if (spGO == null) spGO = new GameObject("FruitSpawner");
            var sp = spGO.GetComponent<FruitSpawner>();
            if (sp == null) sp = spGO.AddComponent<FruitSpawner>();
            sp.database = db;
            sp.fruitPrefab = fruitPrefab;
            sp.mainCamera = cam;
            sp.spawnY = floorY + wallHeight + 0.5f;
            sp.minX = -halfWidth;
            sp.maxX = halfWidth;
            gm.spawner = sp;

            // 7) 씬 저장 dirty 마크 (HUD 없음 — 3단계 PR에서 추가)
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Subak",
                "씬 구성 완료! Ctrl+S로 저장 후 ▶️ Play 해보세요.\n(HUD/UI는 3단계 PR에서 추가됩니다)",
                "확인");
        }

        static Fruit EnsureFruitPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Generated", "Prefabs");

            var existing = AssetDatabase.LoadAssetAtPath<Fruit>(FruitPrefabPath);
            if (existing != null) return existing;

            var go = new GameObject("Fruit");
            go.AddComponent<SpriteRenderer>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1.0f;
            rb.angularDamping = 0.05f;
            rb.linearDamping = 0f;
            go.AddComponent<CircleCollider2D>();
            go.AddComponent<Fruit>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, FruitPrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Fruit>();
        }

        static void CreateWall(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreatePixelSprite();
            sr.color = new Color(0.55f, 0.40f, 0.25f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.sortingOrder = 0;
        }

        static Sprite _pixelSprite;
        static Sprite CreatePixelSprite()
        {
            if (_pixelSprite != null) return _pixelSprite;
            string path = "Assets/Generated/Sprites/_Pixel.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) { _pixelSprite = existing; return existing; }

            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder("Assets/Generated/Sprites"))
                AssetDatabase.CreateFolder("Assets/Generated", "Sprites");

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 4f;
            importer.SaveAndReimport();
            _pixelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return _pixelSprite;
        }
    }
}
#endif
