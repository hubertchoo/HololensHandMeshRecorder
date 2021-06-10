using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
using System.Collections.Generic;

[Serializable]
public class KeyframeEvents : UnityEvent { };
public class HandRecordingPlaybackScript : MonoBehaviour
{
   
    public TextAsset handRecordingJSON = null;

    public Material handMeshMaterial;
    FrameCollection leftHandLoadedData = new FrameCollection();
    FrameCollection rightHandLoadedData = new FrameCollection();

    [NonSerialized]
    public int totalFrames = 0;

    // Left and Right hand GameObjects used for visualisation during playback
    GameObject leftHand;
    GameObject rightHand;
    MeshFilter leftHandFilter;
    MeshFilter rightHandFilter;


    public TextAsset gameobjectRecordingJSON = null;
    TransformRecording goRecording;
    public GameObject gameobjectToPlayback = null;
    Transform goToPlaybackOriginalTransform;

    public event Action playbackStarted;

    [SerializeField]
    public KeyframeEvents keyframeEvents = null;

    Coroutine playback = null;


    void Start()
    {
        #region Set up left and right hand for playback
        // create the left and right hand, get the meshfilters
        leftHand = new GameObject("Left Hand Model");
        leftHand.transform.parent = gameObject.transform;
        leftHandFilter = leftHand.AddComponent<MeshFilter>();
        leftHand.AddComponent<MeshRenderer>().material = handMeshMaterial;

        rightHand = new GameObject("Right Hand Model");
        rightHand.transform.parent = gameObject.transform;
        rightHandFilter = rightHand.AddComponent<MeshFilter>();
        rightHand.AddComponent<MeshRenderer>().material = handMeshMaterial;

        leftHand.SetActive(false);
        rightHand.SetActive(false);

        #endregion

        #region Set up loaded hand and GameObject recordings
        if (handRecordingJSON != null)
        {
            Recording loadedRecord = JsonUtility.FromJson<Recording>(handRecordingJSON.text);
            leftHandLoadedData = loadedRecord.hands[0];
            rightHandLoadedData = loadedRecord.hands[1];
            totalFrames = Math.Max(leftHandLoadedData.frameList.Count, rightHandLoadedData.frameList.Count);
        }

        if (gameobjectRecordingJSON != null)
        {
            TransformRecording loadedGORecord = JsonUtility.FromJson<TransformRecording>(gameobjectRecordingJSON.text);
            goRecording = loadedGORecord;
            totalFrames = Math.Max(totalFrames, goRecording.position.Count);
        }
        if (gameobjectToPlayback != null)
        {
            goToPlaybackOriginalTransform = gameobjectToPlayback.transform;
        }
        #endregion

    }

    #region Playback Recording Functions

    public void PlaybackRecording(bool loopPlayback)
    {
        leftHand.SetActive(true);
        rightHand.SetActive(true);
        playbackStarted?.Invoke();
        playback = StartCoroutine(PlayFrames(loopPlayback));
        keyframeEvents?.Invoke();
    }

    public void EndPlayback()
    {
        StopCoroutine(playback);
        ResetToDefault();
    }

    public void ResetToDefault()
    {
        leftHand.SetActive(false);
        rightHand.SetActive(false);
        if (gameobjectToPlayback != null)
        {
            gameobjectToPlayback.transform.position = goToPlaybackOriginalTransform.position;
            gameobjectToPlayback.transform.rotation = goToPlaybackOriginalTransform.rotation;
            gameobjectToPlayback.transform.localScale = goToPlaybackOriginalTransform.localScale;
        }
    }

    public void DisplayFrame(FrameCollection handData, MeshFilter modelHandMeshFilter, int frameNumber)
    {
        modelHandMeshFilter.mesh.Clear();
        if (frameNumber < handData.frameList.Count)
        {
            Frame currentFrame = handData.frameList[frameNumber];
            modelHandMeshFilter.sharedMesh.vertices = currentFrame.vertices;
            modelHandMeshFilter.sharedMesh.normals = currentFrame.normals;
            modelHandMeshFilter.sharedMesh.triangles = currentFrame.triangles;
            modelHandMeshFilter.sharedMesh.uv = currentFrame.uvs;
            modelHandMeshFilter.sharedMesh.RecalculateBounds();

            modelHandMeshFilter.transform.rotation = currentFrame.rotation;
            modelHandMeshFilter.transform.position = gameObject.transform.position + currentFrame.position;
        }
        
    }

    IEnumerator PlayFrames(bool loopPlayback)
    {
        for (int i = 0; i < totalFrames; i++)
        {
            DisplayFrame(leftHandLoadedData, leftHandFilter, i);
            DisplayFrame(rightHandLoadedData, rightHandFilter, i);
            if (gameobjectToPlayback != null && gameobjectRecordingJSON != null)
            {
                gameobjectToPlayback.transform.position = goRecording.position[i]+ goToPlaybackOriginalTransform.position;
                gameobjectToPlayback.transform.rotation = goRecording.rotation[i];
                gameobjectToPlayback.transform.localScale = goRecording.localScale[i];
            }
            yield return new WaitForFixedUpdate();
        }
        leftHand.SetActive(false);
        rightHand.SetActive(false);
        if (loopPlayback) { PlaybackRecording(loopPlayback); }
        yield break;
    }

    #endregion

    public List<float> loadKeyframes()
    {
        List<float> keyframeList = new List<float>();
        if (handRecordingJSON != null)
        {
            Recording loadedRecord = JsonUtility.FromJson<Recording>(handRecordingJSON.text);
            keyframeList = loadedRecord.keyframes;
        }

        return keyframeList;
    }
}

// Editor Script and Button to autoload saved keyframes as inspector events
#if UNITY_EDITOR
[CustomEditor(typeof(HandRecordingPlaybackScript), true)]
public class customButton : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HandRecordingPlaybackScript handRecordingPlaybackScript = (HandRecordingPlaybackScript)target;
        if (GUILayout.Button("Load Defined Keyframes"))
        {
            List<float> keyframeList = handRecordingPlaybackScript.loadKeyframes();
            if (keyframeList.Count == 0)
            {
                Debug.Log("There are no keyframes to be added.");
            }
            else
            {
                // Create the Keyframe Child to the HandModel Parent
                for (int i = 0; i < keyframeList.Count; i++)
                {
                    GameObject keyframeObject = new GameObject();
                    keyframeObject.transform.parent = handRecordingPlaybackScript.transform;
                    keyframeObject.name = String.Format("{0}s Keyframe", keyframeList[i]);
                    KeyframeManager keyframeManager = keyframeObject.AddComponent<KeyframeManager>();
                    keyframeManager.keyframeTimestamp = keyframeList[i];
                    keyframeManager.AddKeyframeListeners();
                    //handRecordingPlaybackScript.keyframeEvents.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
                }
                Debug.Log(String.Format("{0} keyframe(s) added", keyframeList.Count));
            }
        }
    }

}
#endif