using UnityEngine;

namespace Subak
{
    /// <summary>
    /// 한 단계 과일의 데이터.
    /// stage: 1~11 (체리 ~ 수박)
    /// radius: 월드 단위 반지름 (Fruit 컴포넌트에서 스케일/콜라이더 계산에 사용)
    /// score: 머지로 이 과일이 새로 생성될 때 가산되는 점수 (삼각수 공식: n(n+1)/2)
    /// </summary>
    [CreateAssetMenu(menuName = "Subak/Fruit Data", fileName = "FruitData_")]
    public class FruitData : ScriptableObject
    {
        [Tooltip("과일 단계 (1=체리 ~ 11=수박)")]
        public int stage = 1;

        [Tooltip("한국어 표시명")]
        public string displayName = "체리";

        [Tooltip("월드 단위 반지름 (스프라이트 크기와 매칭)")]
        public float radius = 0.25f;

        [Tooltip("머지 시 가산 점수 (삼각수 공식 권장)")]
        public int score = 1;

        [Tooltip("화면 표시용 스프라이트")]
        public Sprite sprite;

        [Tooltip("선택: 색조 (스프라이트가 흰색 마스크일 때 사용)")]
        public Color tint = Color.white;

        [Tooltip("스프라이트 캔버스 가로 길이 대비, 본체 원의 반지름 비율.\n잎/줄기 없는 깔끔한 원이면 ≈ 0.48,\n잎이 위로 튀어나오는 과일(체리/감 등)이면 0.35~0.45,\n캔버스에 작게 그려진 경우 0.25~0.35.")]
        [Range(0.15f, 0.5f)]
        public float visualBodyRadiusFraction = 0.48f;

        /// <summary>삼각수 점수 헬퍼: n(n+1)/2.</summary>
        public static int TriangularScore(int stage) => stage * (stage + 1) / 2;
    }
}
