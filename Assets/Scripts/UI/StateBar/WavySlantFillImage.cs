using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class WavySlantFillImage : MonoBehaviour
{
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int SlantId = Shader.PropertyToID("_Slant");
    private static readonly int WaveStrengthId = Shader.PropertyToID("_WaveStrength");
    private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
    private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");

    [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;
    [SerializeField, Range(-0.5f, 0.5f)] private float slant = 0.12f;
    [SerializeField, Range(0f, 0.1f)] private float waveStrength = 0.015f;
    [SerializeField, Range(0f, 80f)] private float waveFrequency = 28f;
    [SerializeField, Range(-10f, 10f)] private float waveSpeed = 3f;
    [SerializeField, Range(0.0001f, 0.05f)] private float edgeSoftness = 0.006f;
    [SerializeField] private bool forceSimpleImageType = true;

    private Image image;
    private Material runtimeMaterial;

    public float FillAmount => fillAmount;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        ApplyProperties();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }
    }

    private void OnValidate()
    {
        fillAmount = Mathf.Clamp01(fillAmount);
        slant = Mathf.Clamp(slant, -0.5f, 0.5f);
        waveStrength = Mathf.Max(0f, waveStrength);
        waveFrequency = Mathf.Max(0f, waveFrequency);
        edgeSoftness = Mathf.Max(0.0001f, edgeSoftness);

        if (!Application.isPlaying)
        {
            image = GetComponent<Image>();
        }

        ApplyProperties();
    }

    public void SetFill(float value)
    {
        fillAmount = Mathf.Clamp01(value);
        ApplyProperties();
    }

    public void SetSlant(float value)
    {
        slant = Mathf.Clamp(value, -0.5f, 0.5f);
        ApplyProperties();
    }

    public void SetWave(float strength, float frequency, float speed)
    {
        waveStrength = Mathf.Max(0f, strength);
        waveFrequency = Mathf.Max(0f, frequency);
        waveSpeed = speed;
        ApplyProperties();
    }

    private void Initialize()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (image == null)
        {
            return;
        }

        if (image.type == Image.Type.Filled)
        {
            fillAmount = image.fillAmount;
        }

        if (forceSimpleImageType)
        {
            image.type = Image.Type.Simple;
            image.fillAmount = 1f;
        }

        if (runtimeMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("UI/Wavy Slant Fill");
        if (shader == null)
        {
            Debug.LogError("Cannot find shader: UI/Wavy Slant Fill", this);
            return;
        }

        runtimeMaterial = new Material(shader)
        {
            name = $"{name} Wavy Slant Fill (Instance)"
        };
        image.material = runtimeMaterial;
    }

    private void ApplyProperties()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.SetFloat(FillAmountId, fillAmount);
        runtimeMaterial.SetFloat(SlantId, slant);
        runtimeMaterial.SetFloat(WaveStrengthId, waveStrength);
        runtimeMaterial.SetFloat(WaveFrequencyId, waveFrequency);
        runtimeMaterial.SetFloat(WaveSpeedId, waveSpeed);
        runtimeMaterial.SetFloat(EdgeSoftnessId, edgeSoftness);
    }
}
