using UnityEngine;

// 비닐봉지가 터질 때 파티클 이펙트 생성 → 1.5초 내 자동 파괴
public class BagPopEffect : MonoBehaviour
{
    public const float EffectDuration = 1.5f;

    private ParticleSystem _ps;

    // bagSize: 봉지 Renderer bounds의 extents.magnitude
    public void Play(Color bagColor, float bagSize = 0.1f)
    {
        float maxRadius = bagSize * 1.7f; // 봉지의 1.7배 범위 내에서만 파티클 존재

        _ps = gameObject.AddComponent<ParticleSystem>();
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _ps.Clear();

        var psRenderer = GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.material = CreateParticleMaterial(bagColor);

        // === Main Module ===
        var main = _ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        // 속도를 낮춰서 maxRadius 밖으로 나가지 않도록
        // distance = speed * lifetime → speed = maxRadius / lifetime
        float maxSpeed = maxRadius / 0.6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(maxSpeed * 0.3f, maxSpeed * 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(bagSize * 0.05f, bagSize * 0.15f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(bagColor.r, bagColor.g, bagColor.b, 0.9f),
            new Color(Mathf.Min(bagColor.r * 1.3f, 1f), Mathf.Min(bagColor.g * 1.3f, 1f), Mathf.Min(bagColor.b * 1.3f, 1f), 0.6f)
        );
        main.gravityModifier = 1.5f;     // 강한 중력 → 빠르게 아래로 떨어짐
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 15;
        main.stopAction = ParticleSystemStopAction.Destroy;

        // === Emission (수동 Emit 사용 — Burst가 Stop 후 안 먹는 문제 회피) ===
        var emission = _ps.emission;
        emission.enabled = false; // Burst/Rate 비활성 → Emit()으로 직접 발사

        // === Shape (봉지 중심에서 시작) ===
        var shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = bagSize * 0.3f; // 봉지 내부에서 시작

        // === Limit Velocity (속도 제한으로 범위 밖 이탈 방지) ===
        var limitVelocity = _ps.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.limit = maxSpeed * 0.5f;
        limitVelocity.dampen = 0.5f;

        // === Size over Lifetime (점점 작아짐) ===
        var sizeOverLifetime = _ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.5f),
                new Keyframe(1f, 0f)
            ));

        // === Color over Lifetime (페이드아웃) ===
        var colorOverLifetime = _ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.3f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        Debug.Log($"[PopEffect] bagSize={bagSize:F3}, maxRadius={maxRadius:F3}, pos={transform.position}");
        _ps.Play();
        // 수동으로 15개 파티클 즉시 발사
        _ps.Emit(15);
        Destroy(gameObject, EffectDuration);
    }

    private Material CreateParticleMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.7f));
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        return mat;
    }
}
