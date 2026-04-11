using System;
using LitMotion;
using LitMotion.Extensions;
using NaughtyAttributes;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SwipeTransitionHandle : MonoBehaviour
{
    [SerializeField]
    private RectTransform       _coveredRect;
    [SerializeField]
    private SwipeTransitionData _config;
    [SerializeField]
    private StateData           _startState;
    [SerializeField]
    private StateData           _endState;

    private MotionHandle _swipeHandle;

    public async UniTask PlaySwipeTransitionAsync(Action onComplete)
    {
        _swipeHandle = LMotion
           .Create(_startState.AnchoredPosition, _endState.AnchoredPosition, _config.Duration)
           .WithEase(_config.EaseType)
           .BindToAnchoredPosition(_coveredRect);
        
        await _swipeHandle.ToUniTask(this.destroyCancellationToken);
        onComplete?.Invoke();
    }

    [Button]
    private void SaveStartState() { _startState.AnchoredPosition = _coveredRect.anchoredPosition; }

    [Button]
    private void SaveEndState() { _endState.AnchoredPosition = _coveredRect.anchoredPosition; }

    [Button]
    private void Play()
    {
        PlaySwipeTransitionAsync(null).Forget();
    }
}

[Serializable]
public class SwipeTransitionData
{
    public float Duration;
    public Ease  EaseType;
}

[Serializable]
public class StateData
{
    public Vector2 AnchoredPosition;
}
