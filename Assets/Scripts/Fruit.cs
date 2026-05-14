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

            // 스프라이트 캔버스 대비 본체 반지름 비율을 사용해 콜라이더/시각 정합성 확보.
            // - sprite.bounds.extents.x: sprite local에서의 캔버스 절반 너비
            //   (PPU=텍스처 너비이면 0.5, 다르면 다른 값)
            // - data.visualBodyRadiusFraction: 캔버스 너비 대비 본체 원의 반지름 비율
            //   (잎 없는 깔끔한 원 ≈ 0.48, 잎 있는 체리/감 ≈ 0.35~0.45)
            // → 본체 로컬 반지름 = (캔버스 너비) × fraction = (extents.x × 2) × fraction
            float spriteHalfWidth = (data.sprite != null) ? data.sprite.bounds.extents.x : 0.5f;
            float fraction = Mathf.Clamp(data.visualBodyRadiusFraction, 0.05f, 0.5f);
            float bodyLocalRadius = spriteHalfWidth * 2f * fraction;
            // 본체 시각 반지름이 data.radius(월드)가 되도록 스케일 결정
            float scale = (bodyLocalRadius > 0.0001f) ? (data.radius / bodyLocalRadius) : 1f;
            transform.localScale = new Vector3(scale, scale, 1f);
            // 콜라이더 로컬 반지름 = 본체 로컬 반지름 → 효과 월드 반지름 = data.radius
            _col.radius = bodyLocalRadius;

            _rb.bodyType = kinematicWhileAiming ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            _rb.angularDamping = 0.5f;
            _rb.linearDamping = 0.4f;

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
