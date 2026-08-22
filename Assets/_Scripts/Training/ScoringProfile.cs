using UnityEngine;

[CreateAssetMenu(fileName = "DefaultScoringProfile", menuName = "Presentation/Scoring Profile")]
public sealed class ScoringProfile : ScriptableObject {
    public const string ResourcePath = "Scoring/DefaultScoringProfile";

    [Header("Versioning")]
    [SerializeField] private string _algorithmVersion = "training-v1.0.0";
    [SerializeField] private string _scriptVersion = "company-speech-v1";
    [SerializeField] private string _phase = "training";
    [SerializeField] private string _condition = "implicit-positive-feedback";

    [Header("Weights")]
    [SerializeField, Min(0f)] private float _deliveryWeight = 0.25f;
    [SerializeField, Min(0f)] private float _speedWeight = 0.25f;
    [SerializeField, Min(0f)] private float _volumeWeight = 0.25f;
    [SerializeField, Min(0f)] private float _gazeWeight = 0.25f;

    [Header("Speed and volume targets")]
    [SerializeField, Min(1f)] private float _referenceSpeedCpm = 300f;
    [SerializeField] private float _slowSpeedRatio = 0.8f;
    [SerializeField] private float _normalSpeedRatio = 1f;
    [SerializeField] private float _fastSpeedRatio = 1.2f;
    [SerializeField, Min(0.01f)] private float _speedFalloff = 0.25f;
    [SerializeField] private float _lowVolumeCenter = -0.5f;
    [SerializeField] private float _normalVolumeCenter = 0f;
    [SerializeField] private float _highVolumeCenter = 0.5f;
    [SerializeField, Min(0.01f)] private float _volumeFalloff = 0.5f;

    [Header("Validity and feedback")]
    [SerializeField, Min(0.1f)] private float _minimumSpeechSeconds = 4f;
    [SerializeField, Min(0.1f)] private float _maximumSampleAgeSeconds = 2.5f;
    [SerializeField, Min(1)] private int _feedbackConsecutiveMatches = 2;
    [SerializeField, Range(0f, 100f)] private float _feedbackMinimumDimensionScore = 60f;
    [SerializeField, Range(0f, 100f)] private float _applauseThreshold = 80f;

    public string AlgorithmVersion => _algorithmVersion;
    public string ScriptVersion => _scriptVersion;
    public string Phase => _phase;
    public string Condition => _condition;
    public float DeliveryWeight => _deliveryWeight;
    public float SpeedWeight => _speedWeight;
    public float VolumeWeight => _volumeWeight;
    public float GazeWeight => _gazeWeight;
    public float ReferenceSpeedCpm => _referenceSpeedCpm;
    public float SlowSpeedRatio => _slowSpeedRatio;
    public float NormalSpeedRatio => _normalSpeedRatio;
    public float FastSpeedRatio => _fastSpeedRatio;
    public float SpeedFalloff => _speedFalloff;
    public float LowVolumeCenter => _lowVolumeCenter;
    public float NormalVolumeCenter => _normalVolumeCenter;
    public float HighVolumeCenter => _highVolumeCenter;
    public float VolumeFalloff => _volumeFalloff;
    public float MinimumSpeechSeconds => _minimumSpeechSeconds;
    public float MaximumSampleAgeSeconds => _maximumSampleAgeSeconds;
    public int FeedbackConsecutiveMatches => _feedbackConsecutiveMatches;
    public float FeedbackMinimumDimensionScore => _feedbackMinimumDimensionScore;
    public float ApplauseThreshold => _applauseThreshold;

    public static ScoringProfile LoadDefault() {
        ScoringProfile profile = Resources.Load<ScoringProfile>(ResourcePath);
        return profile != null ? profile : CreateInstance<ScoringProfile>();
    }

    private void OnValidate() {
        _referenceSpeedCpm = Mathf.Max(1f, _referenceSpeedCpm);
        _speedFalloff = Mathf.Max(0.01f, _speedFalloff);
        _volumeFalloff = Mathf.Max(0.01f, _volumeFalloff);
        _minimumSpeechSeconds = Mathf.Max(0.1f, _minimumSpeechSeconds);
        _maximumSampleAgeSeconds = Mathf.Max(0.1f, _maximumSampleAgeSeconds);
        _feedbackConsecutiveMatches = Mathf.Max(1, _feedbackConsecutiveMatches);
    }
}
