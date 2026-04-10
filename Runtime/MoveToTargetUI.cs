using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using NaughtyAttributes;
using UnityEngine;

public class MoveToTargetUI : MonoBehaviour
{
    [SerializeField] private RectTransform _startPoint;
    [SerializeField] private RectTransform _endPoint;
    [SerializeField] private RectTransform _movingObject;
    [SerializeField] private float _duration = 2f;
    [SerializeField] private MoveToTargetUIConfig _config = new();

    private Vector2 _defaultPivot;
    private bool    _hasDefaultPivot = false;

    [Button]
    public void Play()
    {
        ExecuteMovementAsync().Forget();
    }

    public async UniTask ExecuteMovementAsync()
    {
        if (!TryValidateTargets())
        {
            return;
        }

        CacheDefaultPivot();
        ResetMovingObject();
        await PlayPressPhaseAsync();
        await PlayLiftPhaseAsync();
        await PlayDropPhaseAsync();
        await PlayLandingPhaseAsync();
    }

    private bool TryValidateTargets()
    {
        return _startPoint != null
            && _endPoint != null
            && _movingObject != null;
    }

    private void ResetMovingObject()
    {
        RestoreDefaultPivot();
        _movingObject.anchoredPosition = _startPoint.anchoredPosition;
        _movingObject.localScale = Vector3.one;
        _movingObject.localEulerAngles = Vector3.zero;
    }

    private async UniTask PlayPressPhaseAsync()
    {
        var phase = _config.PressPhase;
        await PlayScaleAsync(
            phase.TargetScale,
            GetPhaseDuration(phase.DurationRatio),
            phase.Ease);
    }

    private async UniTask PlayLiftPhaseAsync()
    {
        var phase = _config.LiftPhase;
        var midpoint = GetMidpoint();
        await PlayTravelPhaseAsync(midpoint, phase);
    }

    private async UniTask PlayDropPhaseAsync()
    {
        var phase = _config.DropPhase;
        var target = _endPoint.anchoredPosition;
        await PlayTravelPhaseAsync(target, phase);
    }

    private async UniTask PlayTravelPhaseAsync(Vector2 target, BasePhaseConfig phase)
    {
        var duration = GetPhaseDuration(phase.DurationRatio);
        await UniTask.WhenAll(
            PlayPositionAsync(target, duration, phase.Ease),
            PlayRotationAsync(phase.TargetRotation, duration, phase.Ease),
            PlayScaleAsync(phase.TargetScale, duration, phase.Ease));
    }

    private async UniTask PlayLandingPhaseAsync()
    {
        var phase = _config.LandingPhase;
        await PlayLandingRotationAsync(phase);
        await PlayScaleAsync(
            phase.TargetScale,
            GetPhaseDuration(phase.DurationRatio),
            phase.Ease);
        RestoreDefaultPivot();
        SnapToEndPoint();
    }

    private async UniTask PlayLandingRotationAsync(LandingPhaseConfig phase)
    {
        await PlayRotationAsync(0, phase.StepRatio, phase.Ease);
        await PlayLandingStepAsync(phase.SecondaryRotation, phase.StepRatio, phase.Ease);
        await PlayRotationAsync(0, phase.StepRatio, phase.Ease);
        await PlayLandingStepAsync(phase.TargetRotation, phase.StepRatio, phase.Ease);
        await PlayRotationAsync(0, phase.StepRatio, phase.Ease);
        ApplyLandingPivot(phase.FinalRotation);
    }

    private async UniTask PlayLandingStepAsync(float rotation, float ratio, Ease ease)
    {
        ApplyLandingPivot(rotation);
        await PlayRotationAsync(rotation, GetPhaseDuration(ratio), ease);
    }

    private UniTask PlayPositionAsync(Vector2 target, float duration, Ease ease)
    {
        var handle = LMotion.Create(_movingObject.anchoredPosition, target, duration)
            .WithEase(ease)
            .BindToAnchoredPosition(_movingObject)
            .AddTo(this);
        return handle.ToUniTask(cancellationToken: destroyCancellationToken);
    }

    private UniTask PlayScaleAsync(Vector3 target, float duration, Ease ease)
    {
        var handle = LMotion.Create(_movingObject.localScale, target, duration)
            .WithEase(ease)
            .BindToLocalScale(_movingObject)
            .AddTo(this);
        return handle.ToUniTask(cancellationToken: destroyCancellationToken);
    }

    private UniTask PlayRotationAsync(float zRotation, float duration, Ease ease)
    {
        var handle = LMotion.Create(GetSignedZRotation(), zRotation, duration)
            .WithEase(ease)
            .BindToLocalEulerAnglesZ(_movingObject)
            .AddTo(this);
        return handle.ToUniTask(cancellationToken: destroyCancellationToken);
    }

    private Vector2 GetMidpoint()
    {
        var midpoint = (_startPoint.anchoredPosition + _endPoint.anchoredPosition) * 0.5f;
        var direction = (_endPoint.position - _startPoint.position).normalized;
        var perpendicular = new Vector2(-direction.y, direction.x);
        return midpoint + perpendicular * _config.JumpHeight;
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
            ? _config.LandingPhase.LeftBottomPivot
            : _config.LandingPhase.RightBottomPivot;
    }

    private int GetPivotCornerIndex(Vector2 pivot)
    {
        return pivot.x <= 0.5f ? 0 : 3;
    }

    private void PreserveCornerWhileChangingPivot(Vector2 pivot, int cornerIndex)
    {
        var corners = new Vector3[4];
        _movingObject.GetWorldCorners(corners);
        var beforeCorner = corners[cornerIndex];
        _movingObject.pivot = pivot;
        _movingObject.GetWorldCorners(corners);
        var offset = beforeCorner - corners[cornerIndex];
        _movingObject.position += offset;
    }

    private float GetSignedZRotation()
    {
        var zRotation = _movingObject.localEulerAngles.z;
        return zRotation > 180f ? zRotation - 360f : zRotation;
    }

    private float GetPhaseDuration(float ratio)
    {
        return _duration * ratio;
    }

    private void SnapToEndPoint()
    {
        _movingObject.anchoredPosition = _endPoint.anchoredPosition;
    }

    private void CacheDefaultPivot()
    {
        _defaultPivot = _movingObject.pivot;
        _hasDefaultPivot = true;
    }

    private void RestoreDefaultPivot()
    {
        if (!_hasDefaultPivot)
        {
            return;
        }
        PreserveCornerWhileChangingPivot(_defaultPivot, 0);
    }
}

[Serializable]
public class MoveToTargetUIConfig
{
    public float              JumpHeight   = 90f;
    public BasePhaseConfig    PressPhase   = new(0.2f, Ease.InQuad, new Vector3(1.12f,  0.82f,      1f), 0f);
    public BasePhaseConfig    LiftPhase    = new(0.35f, Ease.Linear, new Vector3(0.82f, 1.12f, 1f), -12f);
    public BasePhaseConfig    DropPhase    = new(0.3f, Ease.Linear, new Vector3(1f,  1f,      1f), 10f);
    public LandingPhaseConfig LandingPhase = new(0.15f, Ease.Linear, Vector3.one, -8f, 6f, 0f, 0.2f);
}

[Serializable]
public class BasePhaseConfig
{
    public float DurationRatio = 0.5f;
    public Ease Ease = Ease.OutQuad;
    public Vector3 TargetScale = Vector3.one;
    public float TargetRotation;

    public BasePhaseConfig()
    {
    }

    public BasePhaseConfig(float durationRatio, Ease ease, Vector3 targetScale, float targetRotation)
    {
        DurationRatio = durationRatio;
        Ease = ease;
        TargetScale = targetScale;
        TargetRotation = targetRotation;
    }
}

[Serializable]
public class LandingPhaseConfig : BasePhaseConfig
{
    public float SecondaryRotation;
    public float FinalRotation;
    public Vector2 LeftBottomPivot = new(0f, 0f);
    public Vector2 RightBottomPivot = new(1f, 0f);
    public float StepRatio;

    public LandingPhaseConfig()
    {
    }

    public LandingPhaseConfig(
        float durationRatio,
        Ease ease,
        Vector3 targetScale,
        float targetRotation,
        float secondaryRotation,
        float finalRotation,
        float stepRatio
        ) : base(durationRatio, ease, targetScale, targetRotation)
    {
        SecondaryRotation = secondaryRotation;
        FinalRotation     = finalRotation;
        StepRatio         = stepRatio;
    }
}
