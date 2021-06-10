using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class RunAtKeyframeEvent : UnityEvent { };
public class KeyframeManager : MonoBehaviour
{
    [NonSerialized]
    public float keyframeTimestamp = -99;
    [SerializeField]
    public RunAtKeyframeEvent runAtKeyframeEvent = null;

    public void AddKeyframeListeners()
    {
        Type[] argument = new Type[1];
        argument[0] = typeof(float);
        MethodInfo classMethods = this.GetType().GetMethod(nameof(RunActions), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, argument, null);
        UnityAction<float> methodDelegate = Delegate.CreateDelegate(typeof(UnityAction<float>), this, classMethods) as UnityAction<float>;
        HandRecordingPlaybackScript handRecordingPlaybackScript = gameObject.transform.parent.GetComponent<HandRecordingPlaybackScript>();
#if UNITY_EDITOR
        UnityEventTools.AddFloatPersistentListener(handRecordingPlaybackScript.keyframeEvents, methodDelegate, keyframeTimestamp);
#endif
    }
   
    public void RunActions(float seconds)
    {
        StartCoroutine(RunActionsCoroutine(seconds));
    }

    IEnumerator RunActionsCoroutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        runAtKeyframeEvent?.Invoke();
        yield break;
    }
}
