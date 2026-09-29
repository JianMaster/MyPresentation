using UnityEngine;

[CreateAssetMenu(fileName = "DefaultScoringProfile", menuName = "Presentation/Scoring Profile")]
public sealed class ScoringProfile : ScriptableObject {
    [Header("Versioning")]
    [SerializeField] private string _algorithmVersion = "training-v3.0.0-ad-segments";
    [SerializeField] private string _scriptVersion = "company-speech-v2-ad";
    [SerializeField] private string _phase = "training";
    [SerializeField] private string _condition = "implicit-positive-feedback";

    [Header("Weights")]
    [SerializeField, Min(0f)] private float _deliveryWeight = 0.25f;
    [SerializeField, Min(0f)] private float _speedWeight = 0.25f;
    [SerializeField, Min(0f)] private float _volumeWeight = 0.25f;
    [SerializeField, Min(0f)] private float _gazeWeight = 0.25f;

    [Header("openSMILE target bands")]
    [SerializeField] private float _speechRateMediumMin = 2.0f;
    [SerializeField] private float _speechRateMediumMax = 5.0f;
    [SerializeField, Min(0.001f)] private float _speechRateFalloff = 3.0f;
    [SerializeField] private float _volumeMediumMin = 0.3f;
    [SerializeField] private float _volumeMediumMax = 0.6f;
    [SerializeField, Min(0.001f)] private float _volumeFalloff = 0.3f;

    [Header("Validity and feedback")]
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
    public int FeedbackConsecutiveMatches => _feedbackConsecutiveMatches;
    public float FeedbackMinimumDimensionScore => _feedbackMinimumDimensionScore;
    public float ApplauseThreshold => _applauseThreshold;

    private void OnValidate() {
        _speechRateMediumMax = Mathf.Max(_speechRateMediumMin, _speechRateMediumMax);
        _speechRateFalloff = Mathf.Max(0.001f, _speechRateFalloff);
        _volumeMediumMax = Mathf.Max(_volumeMediumMin, _volumeMediumMax);
        _volumeFalloff = Mathf.Max(0.001f, _volumeFalloff);
        _feedbackConsecutiveMatches = Mathf.Max(1, _feedbackConsecutiveMatches);
    }
}
