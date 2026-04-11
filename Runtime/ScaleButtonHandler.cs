using System;
using LitMotion;
using LitMotion.Extensions;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

public class ScaleButtonHandler : MonoBehaviour
{
    [SerializeField]
    private Button       _button;
    [SerializeField]
    private ButtonClickData _config;
    private MotionHandle _clickHandle;

    private void Reset()
    {
        _button ??= GetComponent<Button>();
    }
    
    private void Awake() { _button.onClick.AddListener(OnButtonClicked); }

    private void OnDestroy()
    {
        _button.onClick.RemoveListener(OnButtonClicked);
        CancelClickAnimation();
    }

    private void OnButtonClicked()
    {
        CancelClickAnimation();
        _clickHandle = LSequence
           .Create()
           .Append(LMotion
                   .Create(Vector3.one, Vector3.one * _config.ClickScale, _config.Duration / 2f)
                   .WithEase(_config.EaseType)
                   .BindToLocalScale(_button.transform)
                )
           .Append(LMotion
                   .Create(Vector3.one * _config.ClickScale, Vector3.one, _config.Duration/ 2f)
                   .WithEase(_config.EaseType)
                   .BindToLocalScale(_button.transform)
                )
           .Run();
    }
    
    private void CancelClickAnimation()
    {
        if (!_clickHandle.IsActive()) return;
        _clickHandle.TryCancel();
    }
    
    [Button]
    private void PlayClickAnimation()
    {
        OnButtonClicked();
    }
}

[Serializable]
public class ButtonClickData
{
    public float   Duration;
    public float ClickScale;
    public Ease    EaseType;
}
