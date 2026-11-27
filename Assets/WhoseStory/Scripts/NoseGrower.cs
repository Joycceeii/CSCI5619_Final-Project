using UnityEngine;

namespace WhoseStory
{
    // 鼻子按事件变长：模型标记为撒谎时长一截，之后慢慢缩回去。
    // 直接跟着标记取值会让它一路停在最长处，分辨不出任何东西。
    // 两种驱动方式可以同时用：拉伸一个 Transform（占位模型），或者推一个 BlendShape（正式模型）。
    public class NoseGrower : MonoBehaviour
    {
        [Tooltip("鼻子的骨骼或网格，沿 growAxis 拉伸，轴心应在鼻根。用 BlendShape 时可以留空")]
        public Transform nose;

        [Tooltip("拉伸方向（本地坐标）。3D 鼻子是 Z；立牌上的鼻子贴图是 X")]
        public Vector3 growAxis = Vector3.forward;

        [Tooltip("带鼻子 BlendShape 的网格，可以留空")]
        public SkinnedMeshRenderer blendShapeMesh;

        [Tooltip("BlendShape 名字，权重 0 是原长，100 是最长")]
        public string blendShapeName = "NoseLong";

        [Tooltip("每撒一次谎增加的倍数")]
        public float growthPerLie = 0.35f;

        [Tooltip("最长到原长的几倍")]
        public float maxScale = 2.5f;

        [Tooltip("每秒缩回的倍数")]
        public float shrinkPerSecond = 0.08f;

        [Tooltip("长出来的动画时长，秒")]
        public float growDuration = 0.4f;

        float current = 1f;
        float target = 1f;
        float velocity;
        Vector3 baseScale = Vector3.one;
        int blendShapeIndex = -1;

        void Awake()
        {
            if (nose != null)
            {
                baseScale = nose.localScale;
            }

            if (blendShapeMesh != null && blendShapeMesh.sharedMesh != null)
            {
                blendShapeIndex = blendShapeMesh.sharedMesh.GetBlendShapeIndex(blendShapeName);
                if (blendShapeIndex < 0)
                {
                    Debug.LogWarning($"[WhoseStory] blend shape '{blendShapeName}' not found on {blendShapeMesh.name}");
                }
            }
        }

        void Update()
        {
            target = Mathf.Max(1f, target - shrinkPerSecond * Time.deltaTime);
            current = Mathf.SmoothDamp(current, target, ref velocity, growDuration);

            if (nose != null)
            {
                Vector3 axis = new Vector3(Mathf.Abs(growAxis.x), Mathf.Abs(growAxis.y), Mathf.Abs(growAxis.z));
                nose.localScale = baseScale + Vector3.Scale(baseScale, axis) * (current - 1f);
            }

            if (blendShapeIndex >= 0)
            {
                float weight = Mathf.InverseLerp(1f, maxScale, current) * 100f;
                blendShapeMesh.SetBlendShapeWeight(blendShapeIndex, weight);
            }
        }

        public void OnLie()
        {
            target = Mathf.Min(maxScale, target + growthPerLie);
        }
    }
}
