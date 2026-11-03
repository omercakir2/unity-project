using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineThirdPersonFollow))]
public class RunCameraShake: MonoBehaviour
{
    [SerializeField] PlayerController player;

    [Header("Sallanma (±)")]
    [SerializeField] float swayX = 0.07f;      // sağ-sol mesafe
    [SerializeField] float bobY = 0.03f;       // yukarı-aşağı mesafe
    [SerializeField] float randomness = 0.2f;  // 0 = tam düzenli, 0.2 = hafif doğal varyasyon

    [Header("Ritim")]
    [SerializeField] float stepTime = 0.35f;   // bir yan geçişin süresi (sağdan sola)

    [Header("Yumuşaklık")]
    [SerializeField] float smoothIn = 0.12f;
    [SerializeField] float smoothOut = 0.3f;

    CinemachineThirdPersonFollow follow;
    Vector3 baseOffset;
    Vector2 target, current, vel;
    float timer;
    int side = 1; // 1 = sağ, -1 = sol

    void Awake()
    {
        follow = GetComponent<CinemachineThirdPersonFollow>();
        baseOffset = follow.ShoulderOffset;
    }

    void Update()
    {
        bool running = player.IsSprintingNow;

        if (running)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                float r = 1f + Random.Range(-randomness, randomness);
                // x: sırayla sağ/sol, y: her adımda hafif aşağı-yukarı
                target = new Vector2(side * swayX * r, (side > 0 ? bobY : -bobY) * r);
                side = -side;
                timer = stepTime * (1f + Random.Range(-randomness, randomness));
            }
        }
        else
        {
            target = Vector2.zero;
            timer = 0f;
        }

        current = Vector2.SmoothDamp(current, target, ref vel, running ? smoothIn : smoothOut);
        follow.ShoulderOffset = baseOffset + new Vector3(current.x, current.y, 0f);
    }
}