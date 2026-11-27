using UnityEngine;

namespace WhoseStory
{
    // 让角色有点活气：轻微呼吸起伏、头转向玩家、说话时嘴巴开合；玩家看向谁，谁就整个转过身来面对玩家。
    // 换成带骨骼动画的模型后可以去掉这个组件，或者只保留转头。
    public class CharacterPuppet : MonoBehaviour
    {
        [Tooltip("整个模型的根，呼吸时上下浮动")]
        public Transform body;

        [Tooltip("头部轴心（脖子处），会转向玩家")]
        public Transform head;

        [Tooltip("嘴，说话时沿 Y 轴张合")]
        public Transform mouth;

        [Tooltip("眼睛，眨眼时沿 Y 轴压扁")]
        public Transform[] eyes;

        public float blinkIntervalMin = 2f;
        public float blinkIntervalMax = 5f;
        public float blinkDuration = 0.14f;

        [Tooltip("看向的目标，留空就用主相机")]
        public Transform lookTarget;

        public float maxYaw = 40f;
        public float maxPitch = 20f;
        public float turnSpeed = 3f;
        public float breathAmount = 0.006f;
        public float breathRate = 0.25f;

        [Header("转身")]
        [Tooltip("玩家看向这个角色（或正在问他、他在回答）时，整个身体转过来面对玩家；移开视线后转回原位")]
        public bool turnBody = true;
        [Tooltip("转身用的时间（秒），先慢后快再慢，走一段弧")]
        public float bodyTurnTime = 0.55f;
        public float maxBodyYaw = 80f;
        [Tooltip("转身时像木偶被提线拎起来一样轻轻离地的高度（米）")]
        public float turnHop = 0.025f;

        InterrogationCharacter character;
        Vector3 bodyRest;
        Quaternion headRest;
        Vector3 mouthRest;
        Vector3[] eyeRest;
        float phase;
        float nextBlink;
        float blinkStart = -1f;
        InterrogationManager manager;
        float bodyYaw;
        float bodyYawVelocity;

        void Awake()
        {
            character = GetComponentInParent<InterrogationCharacter>();
            if (body != null) bodyRest = body.localPosition;
            if (head != null) headRest = head.localRotation;
            if (mouth != null) mouthRest = mouth.localScale;
            if (eyes != null)
            {
                eyeRest = new Vector3[eyes.Length];
                for (int i = 0; i < eyes.Length; i++) eyeRest[i] = eyes[i].localScale;
            }
            phase = Random.value * 10f;
            nextBlink = Time.time + Random.Range(blinkIntervalMin, blinkIntervalMax);
        }

        void Start()
        {
            manager = FindFirstObjectByType<InterrogationManager>();
        }

        // 被看着、正在被问、或者正在回答的时候算"在对话"
        bool Attending()
        {
            if (character == null) return false;
            if (character.IsBusy || character.IsSpeaking) return true;
            return manager != null && manager.Focused == character;
        }

        // 身体（Model）绕竖直轴转向玩家，返回这一帧该抬起的高度
        float TurnBody()
        {
            if (!turnBody || body == null) return 0f;
            float goal = 0f;
            Transform target = lookTarget != null ? lookTarget : (Camera.main != null ? Camera.main.transform : null);
            if (target != null && Attending())
            {
                Vector3 local = transform.InverseTransformDirection(target.position - body.position);
                goal = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxBodyYaw, maxBodyYaw);
            }
            bodyYaw = Mathf.SmoothDampAngle(bodyYaw, goal, ref bodyYawVelocity, bodyTurnTime);
            body.localRotation = Quaternion.Euler(0f, bodyYaw, 0f);
            // 转得越快离地越高，停下就落回去
            return turnHop * Mathf.Clamp01(Mathf.Abs(bodyYawVelocity) / 160f);
        }

        void Blink()
        {
            if (eyes == null || eyes.Length == 0)
            {
                return;
            }

            if (Time.time >= nextBlink)
            {
                blinkStart = Time.time;
                // 偶尔连眨两下
                float gap = Random.value < 0.2f ? blinkDuration * 1.6f : Random.Range(blinkIntervalMin, blinkIntervalMax);
                nextBlink = Time.time + gap;
            }

            // 0 → 闭 → 0，用正弦走一遍
            float k = blinkStart < 0f ? 0f : Mathf.Clamp01((Time.time - blinkStart) / blinkDuration);
            float closed = k > 0f && k < 1f ? Mathf.Sin(k * Mathf.PI) : 0f;
            for (int i = 0; i < eyes.Length; i++)
            {
                Vector3 scale = eyeRest[i];
                scale.y *= Mathf.Lerp(1f, 0.08f, closed);
                eyes[i].localScale = scale;
            }
        }

        void Update()
        {
            float t = Time.time + phase;
            Blink();

            float hop = TurnBody();
            if (body != null)
            {
                body.localPosition = bodyRest + Vector3.up * (Mathf.Sin(t * breathRate * Mathf.PI * 2f) * breathAmount + hop);
            }

            if (head != null)
            {
                Transform target = lookTarget != null ? lookTarget : (Camera.main != null ? Camera.main.transform : null);
                Quaternion goal = headRest;
                if (target != null)
                {
                    Vector3 local = head.parent.InverseTransformDirection(target.position - head.position);
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                    float flat = new Vector2(local.x, local.z).magnitude;
                    float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                    goal = headRest * Quaternion.Euler(pitch, yaw, 0f);
                }
                head.localRotation = Quaternion.Slerp(head.localRotation, goal, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            if (mouth != null)
            {
                float open = 1f;
                if (character != null && character.IsSpeaking)
                {
                    open = 1f + 2.2f * Mathf.PerlinNoise(t * 9f, 0.5f);
                }
                Vector3 scale = mouthRest;
                scale.y = Mathf.Lerp(mouth.localScale.y, mouthRest.y * open, 1f - Mathf.Exp(-20f * Time.deltaTime));
                mouth.localScale = scale;
            }
        }
    }
}
