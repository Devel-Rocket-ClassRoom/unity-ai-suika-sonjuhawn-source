using System;
using UnityEngine;

namespace Subak
{
    /// <summary>
    /// 인스턴스화된 과일에 붙는 컴포넌트.
    /// 같은 단계 과일과 충돌하면 정적 이벤트로 머지 요청을 발행.
    /// 실제 머지 처리(과일 생성/소멸/점수)는 GameManager(PR #5)에서 이벤트를 구독해 처리.
    /// 중복 처리 방지를 위해 InstanceID가 작은 쪽만 이벤트를 발행.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class Fruit : MonoBehaviour
    {
        // ----- Events (구독자가 실제 머지 로직을 수행) -----
        /// <summary>같은 단계 두 과일이 충돌했을 때 발행. (낮은 InstanceID, 높은 InstanceID, 중점)</summary>
        public static event Action<Fruit, Fruit, Vector2> OnMergeRequested;
        /// <summary>수박(11단계) 두 개가 충돌했을 때 발행.</summary>
        public static event Action<Fruit, Fruit> OnWatermelonMerged;

        // ----- 상태 -----
        public int Stage { get; private set; }
        public FruitData Data { get; private set; }
        public bool IsMerged { get; private set; }
        /// <summary>스폰된 시각. 데드라인 그레이스 판정에 사용.</summary>
        public float SpawnTime { get; private set; }

        Rigidbody2D _rb;
        CircleCollider2D _col;
        SpriteRenderer _sr;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CircleCollider2D>();
            _sr = GetComponent<SpriteRenderer>();
            SpawnTime = Time.time;
        }

        /// <summary>스폰 직후 호출. 데이터 기준으로 비주얼/크기/물리 셋업.</summary>
        public void Init(FruitData data, bool kinematicWhileAiming = false)
        {
            if (data == null) return;
            Data = data;
            Stage = data.stage;

            _sr.sprite = data.sprite;
            _sr.color = data.tint;
            _sr.sortingOrder = data.stage; // 큰 과일이 앞쪽

            // FruitAssetGenerator가 만든 스프라이트는 256x256 텍스처에 반지름 size*0.48의 원을 그림.
            // PPU=size 이므로 sprite local 공간에서 시각 원의 반지름 = 0.48.
            // 시각 반지름이 data.radius(월드 단위)가 되도록 transform 스케일을 결정하고,
            // 콜라이더 로컬 반지름은 sprite local 반지름과 동일하게 두면
            // 효과적 콜라이더 월드 반지름 = 0.48 × scale = data.radius 로 시각/물리가 일치.
            const float SPRITE_LOCAL_RADIUS = 0.48f;
            float scale = data.radius / SPRITE_LOCAL_RADIUS;
            transform.localScale = new Vector3(scale, scale, 1f);
            _col.radius = SPRITE_LOCAL_RADIUS;

            _rb.bodyType = kinematicWhileAiming ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            _rb.angularDamping = 0.05f;
            _rb.linearDamping = 0f;

            IsMerged = false;
            _col.enabled = true;
            SpawnTime = Time.time;
        }

        /// <summary>조준 중에서 실제 낙하로 전환할 때 호출.</summary>
        public void ReleaseToDynamic()
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            SpawnTime = Time.time; // 데드라인 그레이스 기준 재설정
        }

        /// <summary>머지 처리됨을 표시하고 충돌을 비활성화. Destroy는 호출자가 결정.</summary>
        public void MarkMerged()
        {
            IsMerged = true;
            _col.enabled = false;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsMerged) return;
            var other = collision.collider.GetComponent<Fruit>();
            if (other == null || other.IsMerged) return;
            if (other.Stage != Stage) return;

            // 한쪽만 처리: InstanceID가 더 작은 쪽이 머지를 트리거
            if (GetInstanceID() > other.GetInstanceID()) return;

            if (Stage >= 11)
            {
                OnWatermelonMerged?.Invoke(this, other);
                return;
            }

            Vector2 midpoint = (Vector2)(transform.position + other.transform.position) * 0.5f;
            OnMergeRequested?.Invoke(this, other, midpoint);
        }
    }
}
