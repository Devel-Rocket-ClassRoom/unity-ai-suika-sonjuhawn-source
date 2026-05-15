using UnityEngine;

namespace Subak
{
    /// <summary>
    /// 조준 중인 과일의 X 위치를 따라 위→아래로 가이드 라인을 그린다.
    /// LineRenderer를 사용하고, material의 mainTexture가 점선 패턴이면 점선 효과.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class DropGuide : MonoBehaviour
    {
        [Header("References")]
        public FruitSpawner spawner;

        [Header("Range")]
        [Tooltip("가이드 라인 상단 Y 좌표 (월드)")]
        public float topY = 4.2f;
        [Tooltip("가이드 라인 하단 Y 좌표 (월드)")]
        public float bottomY = -3.5f;

        [Header("Style")]
        public Color color = new Color(1f, 1f, 1f, 0.45f);
        public float width = 0.08f;
        [Tooltip("텍스처 1유닛당 반복 횟수 (점선 패턴 밀도)")]
        public float textureTilingPerUnit = 4f;

        LineRenderer _lr;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.positionCount = 2;
            _lr.useWorldSpace = true;
            _lr.startWidth = width;
            _lr.endWidth = width;
            _lr.startColor = color;
            _lr.endColor = color;
            _lr.numCapVertices = 0;
            _lr.textureMode = LineTextureMode.Tile;
            _lr.alignment = LineAlignment.View;
            _lr.sortingOrder = 5;
        }

        void LateUpdate()
        {
            if (spawner == null || !spawner.HasAim)
            {
                if (_lr.enabled) _lr.enabled = false;
                return;
            }
            if (!_lr.enabled) _lr.enabled = true;

            float x = spawner.AimX;
            _lr.SetPosition(0, new Vector3(x, topY, 0f));
            _lr.SetPosition(1, new Vector3(x, bottomY, 0f));

            // 점선 밀도 유지 위해 텍스처 tiling을 라인 길이에 맞춤
            float length = Mathf.Abs(topY - bottomY);
            if (_lr.material != null)
            {
                _lr.material.mainTextureScale = new Vector2(length * textureTilingPerUnit, 1f);
            }
        }
    }
}
