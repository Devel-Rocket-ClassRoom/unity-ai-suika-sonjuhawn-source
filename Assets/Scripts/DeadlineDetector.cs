using System.Collections.Generic;
using UnityEngine;

namespace Subak
{
    /// <summary>
    /// 데드라인 트리거 위에 과일이 머문 시간을 체크.
    /// 임계 시간을 넘기면 GameManager.TriggerGameOver() 호출.
    /// 스폰 직후의 일시적 솟음(grace period)은 무시.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DeadlineDetector : MonoBehaviour
    {
        [Tooltip("이 시간(초) 이상 머물면 게임오버")]
        public float thresholdSeconds = 2.5f;

        [Tooltip("스폰 직후 무시할 grace 시간(초)")]
        public float spawnGrace = 1.2f;

        // 과일 -> 트리거 진입 시각
        readonly Dictionary<Fruit, float> _enterTimes = new();

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var f = other.GetComponent<Fruit>();
            if (f == null) return;
            if (!_enterTimes.ContainsKey(f)) _enterTimes[f] = Time.time;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var f = other.GetComponent<Fruit>();
            if (f == null) return;
            _enterTimes.Remove(f);
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
            if (_enterTimes.Count == 0) return;

            float now = Time.time;
            var keys = new List<Fruit>(_enterTimes.Keys);
            foreach (var f in keys)
            {
                if (f == null)
                {
                    _enterTimes.Remove(f);
                    continue;
                }
                // 스폰 직후 grace 동안은 카운트 시작 시각을 미룸
                float effectiveEnter = Mathf.Max(_enterTimes[f], f.SpawnTime + spawnGrace);
                if (now - effectiveEnter >= thresholdSeconds)
                {
                    GameManager.Instance.TriggerGameOver();
                    return;
                }
            }
        }

        public void ClearTracking() => _enterTimes.Clear();
    }
}
