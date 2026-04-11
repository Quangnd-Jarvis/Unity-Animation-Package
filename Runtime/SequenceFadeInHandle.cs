using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;

public class SequenceFadeInHandle : MonoBehaviour
{
    [SerializeField] private SequenceFadeInStep[] _steps;
    [SerializeField] private SequenceFadeInData   _data;

    [Button]
    private void Play()
    {
        ExecuteSequenceFadeIn().Forget();
    }
    
    public async UniTask ExecuteSequenceFadeIn()
    {
        var tasks = new List<UniTask>();
        for (var i = 0; i < _steps.Length; i++)
        {
            tasks.Add(_steps[i].ExecuteFadeIn(_data.MinAlpha, _data.MaxAlpha, _data.Duration, _data.DelayInterval * i, _data.EaseType, null));
        }
        
        await UniTask.WhenAll(tasks);
    }
}

[Serializable]
public class SequenceFadeInData
{
    public float DelayInterval;
    public float Duration;
    public float MinAlpha;
    public float MaxAlpha;
    public Ease  EaseType;
}
