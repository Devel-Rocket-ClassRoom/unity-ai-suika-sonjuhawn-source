using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Subak
{
    /// <summary>
    /// 화면 상단에서 마우스/터치로 좌우 이동하며 클릭/탭 시 과일을 떨어뜨리는 스포너.
    /// - 조준 중 과일은 Kinematic 상태로 포인터 X를 따라감
    /// - 클릭/탭 시 Dynamic으로 전환되어 자유낙하
    /// - Next 시스템: 다음에 떨어질 과일 단계를 미리 계산해서 이벤트로 알림
    /// - 드롭 쿨다운: 연타 방지
    /// - 클램프: 과일 반지름 고려 + wallSlack 튜닝값으로 벽 너머 살짝 허용
    /// </summary>
    public class FruitSpawner : MonoBehaviour
    {
        [Header("References")]
        public FruitDatabase database;
        public Fruit fruitPrefab;
        public Camera mainCamera;

        [Header("Spawn Area")]
        [Tooltip("스폰 라인의 월드 Y 위치 (통 위쪽)")]
        public float spawnY = 4.0f;
        [Tooltip("통 안쪽 벽 X 좌표 (가장자리가 닿을 수 있는 한계)")]
        public float minX = -2.0f;
        public float maxX = 2.0f;
        [Tooltip("벽 클램프 여유. + 값이면 가장자리가 벽 너머로 wallSlack만큼 나가는 것 허용(관대), 0이면 벽에 딱 맞음, - 값이면 벽보다 그만큼 안쪽에서 멈춤(보수적).")]
        public float wallSlack = 0.15f;

        [Header("Timing")]
        [Tooltip("드롭 후 다음 과일 등장까지의 쿨다운(초)")]
        public float dropCooldown = 0.6f;

        // ----- Events -----
        /// <summary>다음 과일 단계가 결정될 때마다 발행 (UI 갱신용).</summary>
        public event Action<int> OnNextChanged;

        // ----- State -----
        Fruit _current;
        int _nextStage;
        float _nextSpawnTime;
        bool _enabled = true;

        public int CurrentStage => _current != null ? _current.Stage : 0;
        public int NextStage => _nextStage;

        void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (database == null || fruitPrefab == null)
            {
                Debug.LogError("[FruitSpawner] database 또는 fruitPrefab이 인스펙터에서 비어있어요.");
                enabled = false;
                return;
            }
            _nextStage = database.RandomDropStage();
            OnNextChanged?.Invoke(_nextStage);
            SpawnNew();
        }

        void Update()
        {
            if (!_enabled) return;
            if (_current == null)
            {
                if (Time.time >= _nextSpawnTime) SpawnNew();
                return;
            }

            // 마우스/터치 위치 따라가기 (New Input System)
            Vector2 pointerPos;
            if (Mouse.current != null)
            {
                pointerPos = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                pointerPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else
            {
                return; // 입력 장치 없음
            }

            Vector3 world = mainCamera.ScreenToWorldPoint(
                new Vector3(pointerPos.x, pointerPos.y, -mainCamera.transform.position.z));

            // 과일 가장자리가 벽에 닿도록 반지름 + wallSlack을 반영해 클램프
            float r = (_current.Data != null) ? _current.Data.radius : 0f;
            float leftLimit = minX + r - wallSlack;
            float rightLimit = maxX - r + wallSlack;
            if (leftLimit > rightLimit)
            {
                leftLimit = rightLimit = (minX + maxX) * 0.5f;
            }
            float clampedX = Mathf.Clamp(world.x, leftLimit, rightLimit);
            _current.transform.position = new Vector3(clampedX, spawnY, 0f);

            // 클릭 또는 터치
            bool clicked =
                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

            if (clicked) Drop();
        }

        void SpawnNew()
        {
            int stageToSpawn = _nextStage;
            _nextStage = database.RandomDropStage();
            OnNextChanged?.Invoke(_nextStage);

            var data = database.GetByStage(stageToSpawn);
            if (data == null) return;

            var f = Instantiate(fruitPrefab, new Vector3(0f, spawnY, 0f), Quaternion.identity);
            f.Init(data, kinematicWhileAiming: true);
            _current = f;
        }

        void Drop()
        {
            if (_current == null) return;
            _current.ReleaseToDynamic();
            _current = null;
            _nextSpawnTime = Time.time + dropCooldown;
        }

        /// <summary>게임 매니저(PR #5)에서 게임오버 시 호출.</summary>
        public void SetEnabled(bool on)
        {
            _enabled = on;
            if (!on && _current != null)
            {
                Destroy(_current.gameObject);
                _current = null;
            }
        }
    }
}
