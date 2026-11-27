using UnityEngine;

namespace WhoseStory
{
    // 烛光忽明忽暗，偶尔猛地暗一下。
    [RequireComponent(typeof(Light))]
    public class CandleFlicker : MonoBehaviour
    {
        public float amount = 0.25f;
        public float speed = 7f;

        Light candle;
        float baseIntensity;
        float seed;
        float dipUntil;

        void Awake()
        {
            candle = GetComponent<Light>();
            baseIntensity = candle.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
            float k = 1f + n * 2f * amount;
            if (Time.time > dipUntil && Random.value < 0.002f)
            {
                dipUntil = Time.time + Random.Range(0.08f, 0.25f);
            }
            if (Time.time < dipUntil)
            {
                k *= 0.35f;
            }
            candle.intensity = baseIntensity * k;
        }
    }
}
