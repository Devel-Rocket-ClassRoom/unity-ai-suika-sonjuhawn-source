#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TMPro;

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

            // 7) 드롭 가이드 라인
            BuildDropGuide(sp, floorY, wallHeight);

            // 8) HUD (Canvas + Score/Next/GameOver)
            BuildHUD(gm, sp, db);

            // 9) 씬 저장 dirty 마크
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Subak",
                "씬 구성 완료! Ctrl+S로 저장 후 ▶️ Play 해보세요.",
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

        // -------------------- Drop Guide --------------------
        static void BuildDropGuide(FruitSpawner sp, float floorY, float wallHeight)
        {
            var existing = GameObject.Find("DropGuide");
            if (existing != null) Object.DestroyImmediate(existing);

            var guideGO = new GameObject("DropGuide");
            var lr = guideGO.AddComponent<LineRenderer>();
            // 점선 텍스처를 main 텍스처로 가진 머티리얼
            var dashTex = CreateDashTexture();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = dashTex;
            mat.mainTexture.wrapMode = TextureWrapMode.Repeat;
            lr.material = mat;

            var guide = guideGO.AddComponent<DropGuide>();
            guide.spawner = sp;
            guide.topY = floorY + wallHeight + 0.3f;
            guide.bottomY = floorY + 0.2f;
        }

        static Texture2D _dashTex;
        static Texture2D CreateDashTexture()
        {
            if (_dashTex != null) return _dashTex;
            // 1x8 픽셀, 위 4픽셀은 흰색, 아래 4픽셀은 투명
            var tex = new Texture2D(1, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[8];
            for (int i = 0; i < 8; i++)
                pixels[i] = (i < 4) ? Color.white : new Color(0, 0, 0, 0);
            tex.SetPixels(pixels);
            tex.Apply();
            _dashTex = tex;
            return tex;
        }

        // -------------------- HUD --------------------
        static void BuildHUD(GameManager gm, FruitSpawner sp, FruitDatabase db)
        {
            // 기존 HUD GameObject 제거
            var existing = GameObject.Find("HUD");
            if (existing != null) Object.DestroyImmediate(existing);

            var hudGO = new GameObject("HUD");
            var canvas = hudGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hudGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            hudGO.AddComponent<GraphicRaycaster>();

            // EventSystem (New Input System UI Module)
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            // 점수 (상단 좌측)
            var scoreText = CreateText(hudGO.transform, "ScoreText", "Score  0",
                new Vector2(40f, -40f), TextAlignmentOptions.TopLeft, 64,
                anchor: TextAnchor.UpperLeft);

            // 최고 단계 (점수 아래)
            var highText = CreateText(hudGO.transform, "HighestStageText", "Best  -",
                new Vector2(40f, -120f), TextAlignmentOptions.TopLeft, 36,
                anchor: TextAnchor.UpperLeft);

            // Next 레이블 (상단 우측)
            CreateText(hudGO.transform, "NextLabel", "Next",
                new Vector2(-40f, -40f), TextAlignmentOptions.TopRight, 36,
                anchor: TextAnchor.UpperRight);

            // Next 미리보기 이미지
            var nextGO = new GameObject("NextImage", typeof(RectTransform));
            nextGO.transform.SetParent(hudGO.transform, false);
            var nextImg = nextGO.AddComponent<Image>();
            nextImg.preserveAspect = true;
            var nextRT = nextGO.GetComponent<RectTransform>();
            nextRT.anchorMin = new Vector2(1f, 1f);
            nextRT.anchorMax = new Vector2(1f, 1f);
            nextRT.pivot = new Vector2(1f, 1f);
            nextRT.anchoredPosition = new Vector2(-40f, -100f);
            nextRT.sizeDelta = new Vector2(140f, 140f);

            // GameOver 패널 (반투명 풀스크린)
            var panelGO = new GameObject("GameOverPanel", typeof(RectTransform));
            panelGO.transform.SetParent(hudGO.transform, false);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            CreateText(panelGO.transform, "GameOverLabel", "Game Over",
                new Vector2(0, 240), TextAlignmentOptions.Center, 120,
                anchor: TextAnchor.MiddleCenter);

            var finalScoreText = CreateText(panelGO.transform, "FinalScoreText", "Final Score  0",
                new Vector2(0, 80), TextAlignmentOptions.Center, 64,
                anchor: TextAnchor.MiddleCenter);

            var finalHighestText = CreateText(panelGO.transform, "FinalHighestStageText", "Best  -",
                new Vector2(0, 0), TextAlignmentOptions.Center, 40,
                anchor: TextAnchor.MiddleCenter);

            // 재시작 버튼
            var btnGO = new GameObject("RestartButton", typeof(RectTransform));
            btnGO.transform.SetParent(panelGO.transform, false);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(0f, -140f);
            btnRT.sizeDelta = new Vector2(380f, 110f);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(1f, 0.85f, 0.35f);
            var btn = btnGO.AddComponent<Button>();
            CreateText(btnGO.transform, "Label", "Restart",
                Vector2.zero, TextAlignmentOptions.Center, 44,
                anchor: TextAnchor.MiddleCenter);

            panelGO.SetActive(false);

            // GameHUD 컴포넌트 부착 + 와이어링
            var hud = hudGO.AddComponent<GameHUD>();
            hud.gameManager = gm;
            hud.spawner = sp;
            hud.database = db;
            hud.scoreText = scoreText;
            hud.highestStageText = highText;
            hud.nextFruitImage = nextImg;
            hud.gameOverPanel = panelGO;
            hud.finalScoreText = finalScoreText;
            hud.finalHighestStageText = finalHighestText;
            hud.restartButton = btn;
        }

        static TMP_Text CreateText(Transform parent, string name, string text,
            Vector2 anchoredPos, TextAlignmentOptions align, int fontSize,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            switch (anchor)
            {
                case TextAnchor.UpperRight:
                    rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
                    break;
                case TextAnchor.MiddleCenter:
                    rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                    break;
                case TextAnchor.UpperLeft:
                default:
                    rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                    break;
            }
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(800f, 100f);

            var tm = go.AddComponent<TextMeshProUGUI>();
            tm.text = text;
            tm.fontSize = fontSize;
            tm.alignment = align;
            tm.color = (anchor == TextAnchor.MiddleCenter) ? Color.white : Color.black;
            return tm;
        }
    }
}
#endif
