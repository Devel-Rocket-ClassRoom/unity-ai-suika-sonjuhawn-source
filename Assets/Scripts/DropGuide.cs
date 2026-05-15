using UnityEngine;

namespace Subak
{
    [RequireComponent(typeof(LineRenderer))]
    public class DropGuide : MonoBehaviour
    {
        [Header("References")]
        public FruitSpawner spawner;

        [Header("Range")]
        public float topY = 4.2f;
        public float bottomY = -3.5f;

        [Header("Style")]
        public Color color = new Color(1f, 1f, 1f, 0.45f);
        public float width = 0.08f;
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
            float length = Mathf.Abs(topY - bottomY);
            if (_lr.material != null)
            {
                _lr.material.mainTextureScale = new Vector2(length * textureTilingPerUnit, 1f);
            }
        }
    }
}