using System;
using UnityEngine;

namespace Subak
{
    /// <summary>
    /// 게임 전체 상태(점수, 게임오버, 재시작)를 중앙 관리.
    /// Fruit의 정적 이벤트(OnMergeRequested, OnWatermelonMerged)를 구독해 실제 머지 처리.
    ///
    /// 1단계 검증 범위: 머지 동작 + 게임오버 트리거(콘솔 로그).
    /// 점수/재시작 메서드도 함께 구현되어 있으나, 3단계 HUD PR에서 UI와 연결될 예정.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public FruitDatabase database;
        public Fruit fruitPrefab;
        public FruitSpawner spawner;

        [Header("Merge Tuning")]
        [Tooltip("머지로 새로 생긴 과일에 줄 위쪽 임펄스")]
        public float mergeUpImpulse = 1.5f;
        [Tooltip("수박+수박 머지 시 보너스 점수")]
        public int doubleWatermelonBonus = 1000;

        [Header("Debug (1단계 검증용)")]
        [Tooltip("점수/머지/게임오버를 콘솔에 출력")]
        public bool debugLog = true;

        // 이벤트 (3단계 HUD가 구독 예정)
        public event Action<int> OnScoreChanged;
        public event Action<int> OnHighestStageChanged;
        public event Action OnGameOver;
        public event Action OnRestart;

        public int Score { get; private set; }
        public int HighestStage { get; private set; }
        public bool IsGameOver { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            Fruit.OnMergeRequested += HandleMergeRequested;
            Fruit.OnWatermelonMerged += HandleWatermelonMerged;
        }

        void OnDisable()
        {
            Fruit.OnMergeRequested -= HandleMergeRequested;
            Fruit.OnWatermelonMerged -= HandleWatermelonMerged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // -------- Merge handlers --------

        void HandleMergeRequested(Fruit a, Fruit b, Vector2 midpoint)
        {
            if (a == null || b == null || a.IsMerged || b.IsMerged) return;
            if (database == null || fruitPrefab == null) return;

            int newStage = a.Stage + 1;
            var newData = database.GetByStage(newStage);
            if (newData == null) return;

            a.MarkMerged();
            b.MarkMerged();
            Destroy(a.gameObject);
            Destroy(b.gameObject);

            // 새 과일 생성
            var merged = Instantiate(fruitPrefab,
                new Vector3(midpoint.x, midpoint.y, 0f), Quaternion.identity);
            merged.Init(newData, kinematicWhileAiming: false);

            // 약한 상향 임펄스
            var rb = merged.GetComponent<Rigidbody2D>();
            if (rb != null) rb.AddForce(Vector2.up * mergeUpImpulse, ForceMode2D.Impulse);

            // 점수 가산 (삼각수)
            int delta = FruitData.TriangularScore(newStage);
            AddScore(delta);

            // 최고 단계 갱신
            if (newStage > HighestStage)
            {
                HighestStage = newStage;
                OnHighestStageChanged?.Invoke(HighestStage);
                if (debugLog) Debug.Log($"[Subak] 최고 단계 갱신: {HighestStage} ({newData.displayName})");
            }

            if (debugLog) Debug.Log($"[Subak] Merge stage {a.Stage}+{b.Stage} → {newStage} (+{delta}, total {Score})");
        }

        void HandleWatermelonMerged(Fruit a, Fruit b)
        {
            if (a == null || b == null || a.IsMerged || b.IsMerged) return;

            a.MarkMerged();
            b.MarkMerged();
            Destroy(a.gameObject);
            Destroy(b.gameObject);

            AddScore(doubleWatermelonBonus);
            if (debugLog) Debug.Log($"[Subak] 수박+수박! 보너스 +{doubleWatermelonBonus} (total {Score})");
        }

        void AddScore(int delta)
        {
            Score += delta;
            OnScoreChanged?.Invoke(Score);
        }

        // -------- Game Over / Restart --------

        public void TriggerGameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            if (spawner != null) spawner.SetEnabled(false);
            OnGameOver?.Invoke();
            if (debugLog) Debug.Log($"[Subak] GAME OVER — Score: {Score}, 최고: {HighestStage}");
        }

        public void Restart()
        {
            // 모든 과일 제거
            foreach (var f in FindObjectsByType<Fruit>(FindObjectsSortMode.None))
            {
                if (f != null) Destroy(f.gameObject);
            }

            Score = 0;
            HighestStage = 0;
            IsGameOver = false;
            OnScoreChanged?.Invoke(Score);
            OnHighestStageChanged?.Invoke(HighestStage);
            if (spawner != null) spawner.SetEnabled(true);
            OnRestart?.Invoke();
            if (debugLog) Debug.Log("[Subak] Restart");
        }
    }
}
