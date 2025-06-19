using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;
using UnityEngine.Experimental.Rendering;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Color = UnityEngine.Color;

public class AtmosphereCustomPass : CustomPass
{
    [Header("Required References")]
    public GameObject planet;
    public Transform sun;
    public Material atmosphereMaterial;
    public Shader atmosphereShader;
    public RenderTexture m_rt;


    [Header("Settings")]

    public float planetRadius = 1.0f;
    public float atmosphereHeight = 0.1f;
    public float atmosphereDensity = 2.0f;
    public float densityFalloff = 1.0f;

    [Header("Scattering Settings")]
    public float MieCoef = 0.1f;
    public UnityEngine.Vector3 RayleighScatteringCoeff = new UnityEngine.Vector3(5.8f, 13.5f, 33.1f);
    public float ScatteringIntensity = 3.0f;
    public float ScatteringScale = 20.0f;
    public float ScatteringPower = 3.0f;
    public float mieScatteringFromSpace = 2.0f;
    public float mieAnisotropy = 0.76f;

    [Header("Sun Settings")]
    public float sunIntensity = 50.0f;
    public float sunFalloff = 10.0f;
    public Texture2D sunTexture;
    [Range(0, 1)] public float sunTextureSize = 0.1f;

    [Header("Time of Day")]
    public float timeOfDay = 12.0f;
    public float sunriseTime = 6.0f;
    public float sunsetTime = 18.0f;
    public Color sunDaytimeColor = new Color(1.0f, 1.0f, 0.9f, 1.0f);
    public Color sunSunriseColor = new Color(1.0f, 0.5f, 0.2f, 1.0f);
    public Color sunSunsetColor = new Color(1.0f, 0.4f, 0.1f, 1.0f);
    public Color sunNightColor = new Color(0.2f, 0.2f, 0.5f, 1.0f);

    [Header("Advanced Settings")]
    public bool fixAtmosphereScale = true;
    public float atmosphereScaleFactor = 1.0f;
    public bool useDoublePrecision = true;

    private Material depthPrepassMaterial;
    private UnityEngine.Vector4 _planetCenterHigh = UnityEngine.Vector4.zero;
    private UnityEngine.Vector4 _planetCenterLow = UnityEngine.Vector4.zero;
    private Vector3d _planetCenterHighPrecision = Vector3d.zero;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        if(atmosphereShader == null)
             atmosphereShader = Shader.Find("Custom/Atmosphere");
        try
        {
            if (atmosphereMaterial == null)
                atmosphereMaterial = CoreUtils.CreateEngineMaterial(atmosphereShader);
            m_rt.antiAliasing = 1;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create atmosphere material: " + e.Message);
            // Should never happen...
        }
    }
    protected override void Execute(CustomPassContext ctx)
    {
        if (atmosphereMaterial == null || planet == null || sun == null)
        {
            Debug.LogError("Atmosphere material or required references are not set.");
            return;
        }
        UnityEngine.Vector3 planetCenter = _planetCenterHighPrecision != Vector3d.zero
            ? _planetCenterHighPrecision.ToVector3()
            : planet.transform.position;

        float worldScaleFactor = GetPlanetWorldScale();
        float scaledPlanetRadius = planetRadius * worldScaleFactor;
        float scaledAtmosphereHeight = atmosphereHeight * worldScaleFactor * atmosphereScaleFactor;

        SetAtmosphereMaterialProperties(planetCenter, scaledPlanetRadius, scaledAtmosphereHeight);
        ExecuteAtmospherePass(ctx);
    }
    private void ExecuteAtmospherePass(CustomPassContext ctx)
    {
        CoreUtils.SetRenderTarget(
            ctx.cmd,
            ctx.cameraColorBuffer,
            ctx.cameraDepthBuffer,
            ClearFlag.None
        );

        ctx.propertyBlock.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        ctx.propertyBlock.SetFloat("_ZWrite", 0f);

        CoreUtils.DrawFullScreen(
            ctx.cmd,
            atmosphereMaterial,
            ctx.propertyBlock,
            shaderPassId: 0
        );
    }

    private void SetAtmosphereMaterialProperties(Vector3 planetCenter, float planetRadius, float atmosphereHeight)
    {
        // Double precision handling
        if (useDoublePrecision)
        {
            DoubleToTwoFloats(planetCenter.x, planetCenter.y, planetCenter.z,
                            out _planetCenterHigh, out _planetCenterLow);
            atmosphereMaterial.SetVector("_PlanetCenterHigh", _planetCenterHigh);
            atmosphereMaterial.SetVector("_PlanetCenterLow", _planetCenterLow);
            atmosphereMaterial.SetFloat("_UseDoublePrecision", 1.0f);
        }
        else
        {
            atmosphereMaterial.SetVector("_PlanetCenter", planetCenter);
            atmosphereMaterial.SetFloat("_UseDoublePrecision", 0.0f);
        }
        
        // Core atmosphere properties
        atmosphereMaterial.SetFloat("_PlanetRadius", planetRadius);
        atmosphereMaterial.SetFloat("_AtmosphereHeight", atmosphereHeight);
        atmosphereMaterial.SetVector("_SunPosition", sun.position);
        atmosphereMaterial.SetVector("_RayleighScatteringCoeff", RayleighScatteringCoeff);
        atmosphereMaterial.SetFloat("_MieScatteringCoeff", MieCoef);

        // Time of day and colors
        atmosphereMaterial.SetFloat("_TimeOfDay", timeOfDay);
        atmosphereMaterial.SetColor("_SunDaytimeColor", sunDaytimeColor);
        atmosphereMaterial.SetColor("_SunSunriseColor", sunSunriseColor);
        atmosphereMaterial.SetColor("_SunSunsetColor", sunSunsetColor);
        atmosphereMaterial.SetColor("_SunNightColor", sunNightColor);

        // Textures
        if (sunTexture != null)
            atmosphereMaterial.SetTexture("_SunTexture", sunTexture);
        atmosphereMaterial.SetFloat("_SunTextureSize", sunTextureSize);

        // Render settings
        atmosphereMaterial.renderQueue = (int)RenderQueue.Transparent;
        atmosphereMaterial.SetInt("_SrcBlend", (int)BlendMode.One);
        atmosphereMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        atmosphereMaterial.SetInt("_ZWrite", 1);
        atmosphereMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
    }
    private float GetPlanetWorldScale()
    {
        if (planet == null) return 1.0f;
        UnityEngine.Vector3 scale = planet.transform.lossyScale;
        return Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
    }
    private void DoubleToTwoFloats(double x, double y, double z, out Vector4 high, out Vector4 low)
    {
        float xHigh = (float)x;
        float yHigh = (float)y;
        float zHigh = (float)z;

        high = new Vector4(xHigh, yHigh, zHigh, 0);
        low = new Vector4((float)(x - xHigh), (float)(y - yHigh), (float)(z - zHigh), 0);
    }
}
public struct Vector3d
{
    public double x, y, z;

    public Vector3d(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static readonly Vector3d zero = new Vector3d(0, 0, 0);

    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3((float)x, (float)y, (float)z);

    public override bool Equals(object obj) => obj is Vector3d d && x == d.x && y == d.y && z == d.z;
    public static bool operator ==(Vector3d a, Vector3d b) => a.x == b.x && a.y == b.y && a.z == b.z;
    public static bool operator !=(Vector3d a, Vector3d b) => !(a == b);
    public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
}