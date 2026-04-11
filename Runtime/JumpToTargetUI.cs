using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using NaughtyAttributes;
using UnityEngine;

public class JumpToTargetUI : MonoBehaviour
{
    [SerializeField] private RectTransform        _movingRect;
    [SerializeField] private MoveToTargetUIConfig _config   = new();

    private Vector3                 _startPoint;
    private Vector3                 _endPoint;
    private Action                  _onComplete;
    private Vector2                 _defaultPivot;
    private CancellationTokenSource _cancellationTokenSource = new ();

    private void OnDestroy()
    {
        CancelMoveAnimation();
    }
    
    [SerializeField]
    private RectTransform start;
    [SerializeField]
    private RectTransform end;

    [Button]
    private void Play()
    {
        ExecuteMovement(start.anchoredPosition, end.anchoredPosition, null).Forget();
    }

    public async UniTask ExecuteMovement(Vector3 startPoint, Vector3 endPoint, Action onComplete)
    {
        _startPoint = startPoint;
        _endPoint   = endPoint;
        _onComplete = onComplete;
        await ExecuteMovementAsync();
    }

    private async UniTask ExecuteMovementAsync()
    {
        CancelMoveAnimation();
        CacheDefaultPivot();
        ResetMovingObject();
        await PlayPressPhaseAsync();
        await JumpToTargetAsync();
        _onComplete?.Invoke();
        await PlayLandingPhaseAsync();
    }
    
    private async UniTask JumpToTargetAsync()
    {
        var amplitude = GetAmplitude();
        var handle = LMotion
           .Create(0f, 1f, _config.JumpPhase.Duration)
           .WithEase(_config.JumpPhase.Ease)
           .Bind(ApplyJumpFrame);
        await handle.ToUniTask(cancellationToken: _cancellationTokenSource.Token);
        return;

        void ApplyJumpFrame(float value)
        {
            var state = value <= 0.5f ? GetJumpFirstState(value) : GetJumpSecondState(value);
            _movingRect.anchoredPosition = state.position + Vector2.up * Mathf.Sin(value * Mathf.PI) * amplitude;
            _movingRect.localScale = state.scale;
            _movingRect.localEulerAngles = new Vector3(0f, 0f, state.rotation);
        }
    }
    
    private (Vector2 position, Vector3 scale, float rotation) GetJumpFirstState(float value)
    {
        var progress       = value / 0.5f;
        var phase          = _config.JumpPhase;
        var targetRotation = _startPoint.y > _endPoint.y ? phase.TargetRotation : -phase.TargetRotation;
        var fromScale      = Vector3.one;
        var fromRotation   = 0f;
        var position       = Vector2.Lerp(_startPoint, _endPoint, value);
        var scale          = Vector3.Lerp(fromScale, phase.TargetScale, progress);
        var rotation       = Mathf.Lerp(fromRotation, targetRotation, progress);
        return (position, scale, rotation);
    }
    
    private (Vector2 position, Vector3 scale, float rotation) GetJumpSecondState(float value)
    {
        var progress       = (value - 0.5f) / 0.5f;
        var phase          = _config.LandingPhase;
        var sign           = (_endPoint - _startPoint).normalized.x >= 0f ? 1f : -1f;
        var targetRotation = sign * phase.TargetRotation;
        var fromScale      = _config.JumpPhase.TargetScale;
        var fromRotation   = _startPoint.y > _endPoint.y ? _config.JumpPhase.TargetRotation : -_config.JumpPhase.TargetRotation;
        var position       = Vector2.Lerp(_startPoint, _endPoint, value);
        var scale          = Vector3.Lerp(fromScale, phase.TargetScale, progress);
        var rotation       = Mathf.Lerp(fromRotation, targetRotation, progress);
        return (position, scale, rotation);
    }

    private void ResetMovingObject()
    {
        PreserveCornerWhileChangingPivot(_defaultPivot, 0);
        _movingRect.anchoredPosition = _startPoint;
        _movingRect.localScale = Vector3.one;
        _movingRect.localEulerAngles = Vector3.zero;
    }

    private async UniTask PlayPressPhaseAsync()
    {
        var phase = _config.PressPhase;
        await PlayScaleAsync(
            phase.TargetScale,
            phase.Duration,
            phase.Ease);
    }

    private async UniTask PlayLandingPhaseAsync()
    {
        var phase = _config.LandingPhase;
        await PlayLandingRotationAsync(phase);
        await PlayScaleAsync(
            phase.TargetScale,
            phase.Duration,
            phase.Ease);
        PreserveCornerWhileChangingPivot(_defaultPivot, 0);
        SnapToEndPoint();
    }

    private async UniTask PlayLandingRotationAsync(BasePhaseConfig phase)
    {
        var direction = (_endPoint - _startPoint).normalized;
        var sign = direction.x >= 0f ? 1f : -1f;
        await PlayRotationAsync(0, phase.Duration, phase.Ease);
        await PlayLandingStepAsync(sign * -phase.TargetRotation, phase.Duration, phase.Ease);
        await PlayRotationAsync(0, phase.Duration, phase.Ease);
        await PlayLandingStepAsync(sign * phase.TargetRotation, phase.Duration, phase.Ease);
        await PlayRotationAsync(0, phase.Duration, phase.Ease);
    }

    private async UniTask PlayLandingStepAsync(float rotation, float duration, Ease ease)
    {
        ApplyLandingPivot(rotation);
        await PlayRotationAsync(rotation, duration, ease);
    }

    private UniTask PlayScaleAsync(Vector3 target, float duration, Ease ease)
    {
        var handle = LMotion.Create(_movingRect.localScale, target, duration)
            .WithEase(ease)
            .BindToLocalScale(_movingRect)
            .AddTo(this);
        return handle.ToUniTask(cancellationToken: _cancellationTokenSource.Token);
    }

    private UniTask PlayRotationAsync(float zRotation, float duration, Ease ease)
    {
        var handle = LMotion.Create(GetSignedZRotation(), zRotation, duration)
            .WithEase(ease)
            .BindToLocalEulerAnglesZ(_movingRect)
            .AddTo(this);
        return handle.ToUniTask(cancellationToken: _cancellationTokenSource.Token);
    }
    
    private float GetAmplitude()
    {
        var direction = (_endPoint - _startPoint).normalized;
        var height    = Math.Abs(direction.x) > 0.5f ? _config.JumpHeight : _config.JumpHeight * 0;
        return height;
    }

    private void ApplyLandingPivot(float rotation)
    {
        var targetPivot = GetLandingPivot(rotation);
        var cornerIndex = GetPivotCornerIndex(targetPivot);
        PreserveCornerWhileChangingPivot(targetPivot, cornerIndex);
    }

    private Vector2 GetLandingPivot(float rotation)
    {
        return rotation >= 0f
            ? Vector2.zero
            : new Vector2(1f, 0f);
    }

    private int GetPivotCornerIndex(Vector2 pivot)
    {
        return pivot.x <= 0.5f ? 0 : 3;
    }

    private void PreserveCornerWhileChangingPivot(Vector2 pivot, int cornerIndex)
    {
        var corners = new Vector3[4];
        _movingRect.GetWorldCorners(corners);
        var beforeCorner = corners[cornerIndex];
        _movingRect.pivot = pivot;
        _movingRect.GetWorldCorners(corners);
        var offset = beforeCorner - corners[cornerIndex];
        _movingRect.position += offset;
    }

    private float GetSignedZRotation()
    {
        var zRotation = _movingRect.localEulerAngles.z;
        return zRotation > 180f ? zRotation - 360f : zRotation;
    }

    private void SnapToEndPoint()
    {
        _movingRect.anchoredPosition = _endPoint;
    }

    private void CacheDefaultPivot()
    {
        _defaultPivot = _config.DefaultPivot;
    }
    
    private void CancelMoveAnimation()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
    }
}

[Serializable]
public class MoveToTargetUIConfig
{
    public Vector2         DefaultPivot = new Vector2(0.5f, 0.5f);
    public float           JumpHeight   = 90f;
    public BasePhaseConfig PressPhase   = new(0.2f, Ease.OutBack, new Vector3(1.12f, 0.82f, 1f), 0f);
    public BasePhaseConfig JumpPhase    = new(0.7f, Ease.Linear, new Vector3(0.82f,  1.12f, 1f), -12f);
    public BasePhaseConfig LandingPhase = new(0.7f, Ease.Linear, new Vector3(0.82f,  1.12f, 1f), -12f);
}

[Serializable]
public class BasePhaseConfig
{
    public float   Duration    = 0.5f;
    public Ease    Ease        = Ease.Linear;
    public Vector3 TargetScale = Vector3.one;
    public float   TargetRotation;

    public BasePhaseConfig()
    {
    }

    public BasePhaseConfig(float duration, Ease ease, Vector3 targetScale, float targetRotation)
    {
        Duration       = duration;
        Ease           = ease;
        TargetScale    = targetScale;
        TargetRotation = targetRotation;
    }
}