using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;

[RequireComponent(typeof(CanvasGroup))]
public class SequenceFadeInStep : MonoBehaviour
{
    [SerializeField]
    private CanvasGroup _canvasGroup;
    [SerializeField]
    private RectTransform _rectTransform;

    private MotionHandle _motionHandle;
    private Action _onComplete;

    private void Reset()
    {
        _canvasGroup   ??= GetComponent<CanvasGroup>();
        _rectTransform ??= GetComponent<RectTransform>();
    }
    
    public async UniTask ExecuteFadeIn(float minAlpha, float maxAlpha, float duration, float delayInterval, Ease easeType, Action onComplete)
    {
        _canvasGroup.alpha        = minAlpha;
        _rectTransform.localScale = Vector3.one * 0.9f;

        if (_motionHandle.IsActive()) _motionHandle.TryCancel();
        
        await UniTask.Delay(TimeSpan.FromSeconds(delayInterval));

        _motionHandle = LSequence
           .Create()
           .Append(
                    LMotion
                       .Create(minAlpha, maxAlpha, duration)
                       .WithEase(easeType)
                       .BindToAlpha(_canvasGroup)
                )
           .Join(LMotion
                   .Create(Vector3.one * 0.95f, Vector3.one * 1f, 0.2f)
                   .WithEase(easeType)
                   .BindToLocalScale(_rectTransform)
                )
           .Run();

        await _motionHandle.ToUniTask(this.destroyCancellationToken);
        onComplete?.Invoke();
    }
}
