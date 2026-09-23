using M365Trace.Core;

namespace M365Trace.Rules;

public sealed class AnalysisContext
{
    private readonly List<TraceFinding> _findings = [];
    private TraceSeverity? _severity;
    private string? _sessionType;
    private int _sessionTypeConfidence;
    private string? _authentication;
    private int _authenticationConfidence;
    private string? _responseServer;
    private int _responseServerConfidence;

    public AnalysisContext(TraceSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Facts = new SessionFacts(session);
    }

    public TraceSession Session { get; }

    public SessionFacts Facts { get; }

    public int SessionTypeConfidence => _sessionTypeConfidence;

    public int AuthenticationConfidence => _authenticationConfidence;

    public int ResponseServerConfidence => _responseServerConfidence;

    public void AddFinding(TraceFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        if (_findings.Any(existing =>
                string.Equals(existing.RuleId, finding.RuleId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Rule '{finding.RuleId}' attempted to add more than one finding.");
        }

        _findings.Add(finding);
        RaiseSeverity(finding.Severity);
    }

    public bool HasFindingWithRuleIdPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return _findings.Any(finding =>
            finding.RuleId.StartsWith(prefix, StringComparison.Ordinal));
    }

    public void ApplyClassification(
        RuleClassification classification,
        string? sessionType = null)
    {
        ArgumentNullException.ThrowIfNull(classification);

        SetAuthentication(null, classification.AuthenticationConfidence);
        SetSessionType(sessionType, classification.SessionTypeConfidence);
        SetResponseServer(null, classification.ResponseServerConfidence);
        RaiseSeverity(classification.Severity);
    }

    public void SetSessionType(string? value, int confidence)
    {
        ValidateConfidence(confidence);

        if (ShouldReplace(_sessionType, _sessionTypeConfidence, value, confidence))
        {
            _sessionType = value;
            _sessionTypeConfidence = confidence;
        }
    }

    public void SetAuthentication(string? value, int confidence)
    {
        ValidateConfidence(confidence);

        if (ShouldReplace(_authentication, _authenticationConfidence, value, confidence))
        {
            _authentication = value;
            _authenticationConfidence = confidence;
        }
    }

    public void SetResponseServer(string? value, int confidence)
    {
        ValidateConfidence(confidence);

        if (ShouldReplace(_responseServer, _responseServerConfidence, value, confidence))
        {
            _responseServer = value;
            _responseServerConfidence = confidence;
        }
    }

    public void RaiseSeverity(TraceSeverity severity)
    {
        if (_severity is null || (int)severity > (int)_severity.Value)
        {
            _severity = severity;
        }
    }

    public TraceAnalysisResult Build() => new()
    {
        Severity = _severity,
        SessionType = _sessionType,
        SessionTypeConfidence = _sessionTypeConfidence,
        Authentication = _authentication,
        AuthenticationConfidence = _authenticationConfidence,
        ResponseServer = _responseServer,
        ResponseServerConfidence = _responseServerConfidence,
        Findings = _findings.ToArray()
    };

    private static bool ShouldReplace(
        string? currentValue,
        int currentConfidence,
        string? newValue,
        int newConfidence) =>
        newConfidence > currentConfidence
        || (newConfidence == currentConfidence
            && currentValue is null
            && newValue is not null);

    private static void ValidateConfidence(int confidence)
    {
        if (confidence is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                "Confidence must be between 0 and 10.");
        }
    }
}
