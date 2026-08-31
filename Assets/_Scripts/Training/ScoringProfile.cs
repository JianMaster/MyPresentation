using UnityEngine;

[CreateAssetMenu(fileName = "DefaultScoringProfile", menuName = "Presentation/Scoring Profile")]
public sealed class ScoringProfile : ScriptableObject {
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

    [Header("openSMILE target bands")]
    [SerializeField] private float _speechRateMediumMin = 3.279f;
    [SerializeField] private float _speechRateMediumMax = 4.054f;
    [SerializeField, Min(0.001f)] private float _speechRateFalloff = 0.775f;
    [SerializeField] private float _volumeMediumMin = 0.253f;
    [SerializeField] private float _volumeMediumMax = 0.381f;
    [SerializeField, Min(0.001f)] private float _volumeFalloff = 0.128f;

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
    public float SpeechRateMediumMin => _speechRateMediumMin;
    public float SpeechRateMediumMax => _speechRateMediumMax;
    public float SpeechRateFalloff => _speechRateFalloff;
    public float VolumeMediumMin => _volumeMediumMin;
    public float VolumeMediumMax => _volumeMediumMax;
    public float VolumeFalloff => _volumeFalloff;
    public float MinimumSpeechSeconds => _minimumSpeechSeconds;
    public float MaximumSampleAgeSeconds => _maximumSampleAgeSeconds;
    public int FeedbackConsecutiveMatches => _feedbackConsecutiveMatches;
    public float FeedbackMinimumDimensionScore => _feedbackMinimumDimensionScore;
    public float ApplauseThreshold => _applauseThreshold;

    private void OnValidate() {
        _speechRateMediumMax = Mathf.Max(_speechRateMediumMin, _speechRateMediumMax);
        _speechRateFalloff = Mathf.Max(0.001f, _speechRateFalloff);
        _volumeMediumMax = Mathf.Max(_volumeMediumMin, _volumeMediumMax);
        _volumeFalloff = Mathf.Max(0.001f, _volumeFalloff);
        _minimumSpeechSeconds = Mathf.Max(0.1f, _minimumSpeechSeconds);
        _maximumSampleAgeSeconds = Mathf.Max(0.1f, _maximumSampleAgeSeconds);
        _feedbackConsecutiveMatches = Mathf.Max(1, _feedbackConsecutiveMatches);
    }
}
