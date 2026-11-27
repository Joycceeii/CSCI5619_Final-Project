using UnityEngine;

namespace WhoseStory
{
    // 漫剧风格的 2.5D 立牌：一张立绘加几张表情差分，始终转向玩家。
    // 差分必须和 idle 同尺寸、同构图，只有眼睛或嘴不一样，切换时才不会跳。
    public class MangaStandee : MonoBehaviour
    {
        public SpriteRenderer body;

        [Header("差分")]
        public Sprite idle;
        [Tooltip("闭眼")]
        public Sprite blink;
        [Tooltip("说话时轮流显示的张嘴图，可以放多张")]
        public Sprite[] talk;

        [Header("眨眼")]
        public float blinkIntervalMin = 2f;
        public float blinkIntervalMax = 5f;
        public float blinkDuration = 0.12f;

        [Header("说话")]
        public float mouthRate = 9f;

        [Header("朝向和呼吸")]
        public Transform lookTarget;
        public float turnSpeed = 4f;
        public float breathAmount = 0.008f;
        public float breathRate = 0.3f;

        InterrogationCharacter character;
        float nextBlink;
        float blinkUntil;
        Vector3 bodyScale;
        float phase;

        void Awake()
        {
            character = GetComponentInParent<InterrogationCharacter>();
            if (body != null) bodyScale = body.transform.localScale;
            phase = Random.value * 10f;
            ScheduleBlink();
        }

        void ScheduleBlink()
        {
            nextBlink = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
        }

        void Update()
        {
            FacePlayer();

            if (body == null)
            {
                return;
            }

            float t = Time.time + phase;
            body.transform.localScale = new Vector3(bodyScale.x,
                bodyScale.y * (1f + Mathf.Sin(t * breathRate * Mathf.PI * 2f) * breathAmount), bodyScale.z);

            if (Time.time >= nextBlink)
            {
                blinkUntil = Time.time + blinkDuration;
                ScheduleBlink();
            }

            bool speaking = character != null && character.IsSpeaking;
            Sprite frame = idle;

            if (speaking && talk != null && talk.Length > 0)
            {
                // 用噪声决定嘴张多大，比固定节奏自然
                float n = Mathf.PerlinNoise(t * mouthRate, 0.37f);
                if (n > 0.38f)
                {
                    int index = Mathf.Min(talk.Length - 1, (int)((n - 0.38f) / 0.62f * talk.Length));
                    frame = talk[index];
                }
            }
            else if (Time.time < blinkUntil && blink != null)
            {
                frame = blink;
            }

            if (frame != null && body.sprite != frame)
            {
                body.sprite = frame;
            }
        }

        // 只绕竖直轴转，立牌不会前后倒
        void FacePlayer()
        {
            Transform target = lookTarget != null ? lookTarget : (Camera.main != null ? Camera.main.transform : null);
            if (target == null)
            {
                return;
            }

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // 立绘画在 XY 平面上，正面朝 -Z（SpriteRenderer 的默认朝向），所以让 -Z 对着玩家
            Quaternion goal = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, goal, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }
    }
}
