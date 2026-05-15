using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Subak
{
    /// <summary>
    /// 인게임 HUD. 점수/최고단계/Next 표시, 게임오버 패널 토글, 재시작 버튼.
    /// 이벤트 구독으로 GameManager / FruitSpawner의 상태 변화에 반응.
    /// UI 요소들은 인스펙터(또는 SceneBootstrapper의 자동 구성)에서 연결.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("References")]
        public GameManager gameManager;
        public FruitSpawner spawner;
        public FruitDatabase database;

        [Header("HUD")]
        public TMP_Text scoreText;
        public TMP_Text highestStageText;
        public Image nextFruitImage;

        [Header("Next Preview Sizing")]
        [Tooltip("자연 드롭 최대 단계(보통 감)에서의 Next 이미지 크기(UI px). 0이면 크기 변경 없음.")]
        public float nextMaxSize = 140f;
        [Tooltip("Next 이미지의 최소 크기(UI px). 너무 작아져서 안 보이는 것 방지.")]
        public float nextMinSize = 40f;

        [Header("Game Over Panel")]
        public GameObject gameOverPanel;
        public TMP_Text finalScoreText;
        public TMP_Text finalHighestStageText;
        public Button restartButton;

        void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.OnScoreChanged += HandleScoreChanged;
                gameManager.OnHighestStageChanged += HandleHighestStageChanged;
                gameManager.OnGameOver += HandleGameOver;
                gameManager.OnRestart += HandleRestart;
            }
            if (spawner != null)
            {
                spawner.OnNextChanged += HandleNextChanged;
            }
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }
        }

        void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.OnScoreChanged -= HandleScoreChanged;
                gameManager.OnHighestStageChanged -= HandleHighestStageChanged;
                gameManager.OnGameOver -= HandleGameOver;
                gameManager.OnRestart -= HandleRestart;
            }
            if (spawner != null)
            {
                spawner.OnNextChanged -= HandleNextChanged;
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
        }

        void Start()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            HandleScoreChanged(0);
            HandleHighestStageChanged(0);
            if (spawner != null) HandleNextChanged(spawner.NextStage);
        }

        void HandleScoreChanged(int score)
        {
            if (scoreText != null) scoreText.text = $"Score  {score:N0}";
        }

        void HandleHighestStageChanged(int stage)
        {
            if (highestStageText == null && finalHighestStageText == null) return;
            string label;
            if (stage <= 0) label = "Best  -";
            else
            {
                var data = (database != null) ? database.GetByStage(stage) : null;
                label = (data != null) ? $"Best  Stage {stage}" : $"Best  Stage {stage}";
            }
            if (highestStageText != null) highestStageText.text = label;
            if (finalHighestStageText != null) finalHighestStageText.text = label;
        }

        void HandleNextChanged(int stage)
        {
            if (nextFruitImage == null || database == null) return;
            var data = database.GetByStage(stage);
            if (data == null) { nextFruitImage.enabled = false; return; }
            nextFruitImage.enabled = true;
            nextFruitImage.sprite = data.sprite;
            nextFruitImage.color = data.tint;

            // 과일 반지름에 비례해 Next 미리보기 크기 조정 (단계 구분 가시화)
            if (nextMaxSize > 0f)
            {
                int refStage = Mathf.Clamp(database.maxNaturalDropStage, 1, 11);
                var refData = database.GetByStage(refStage);
                float refRadius = (refData != null && refData.radius > 0f) ? refData.radius : data.radius;
                float ratio = (refRadius > 0f) ? data.radius / refRadius : 1f;
                float size = Mathf.Clamp(nextMaxSize * ratio, nextMinSize, nextMaxSize);
                nextFruitImage.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        void HandleGameOver()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (finalScoreText != null && gameManager != null)
                finalScoreText.text = $"Final Score  {gameManager.Score:N0}";
            // 최고 단계 라벨도 한 번 더 갱신 (게임오버 화면에서 같이 보이도록)
            if (gameManager != null) HandleHighestStageChanged(gameManager.HighestStage);
        }

        void HandleRestart()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        void OnRestartClicked()
        {
            if (gameManager != null) gameManager.Restart();
        }
    }
}
