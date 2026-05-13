using UnityEngine;

namespace Subak
{
    /// <summary>
    /// 11단계 과일의 데이터 목록.
    /// fruits[0]은 stage 1(체리), fruits[10]은 stage 11(수박).
    /// </summary>
    [CreateAssetMenu(menuName = "Subak/Fruit Database", fileName = "FruitDatabase")]
    public class FruitDatabase : ScriptableObject
    {
        [Tooltip("stage 1~11 순서대로 11개")]
        public FruitData[] fruits = new FruitData[11];

        [Tooltip("자연 드롭으로 등장 가능한 최대 단계 (포함). GDD 기준 5단계(감).")]
        public int maxNaturalDropStage = 5;

        /// <summary>1-indexed stage로 안전 접근. 범위 밖이면 null.</summary>
        public FruitData GetByStage(int stage)
        {
            int idx = stage - 1;
            if (fruits == null || idx < 0 || idx >= fruits.Length) return null;
            return fruits[idx];
        }

        /// <summary>다음 단계 과일 데이터. 수박(11)이면 null.</summary>
        public FruitData GetNext(int stage)
        {
            if (stage >= 11) return null;
            return GetByStage(stage + 1);
        }

        /// <summary>자연 드롭으로 등장할 무작위 단계 (1~maxNaturalDropStage).</summary>
        public int RandomDropStage()
        {
            int max = Mathf.Clamp(maxNaturalDropStage, 1, 11);
            return Random.Range(1, max + 1);
        }
    }
}
