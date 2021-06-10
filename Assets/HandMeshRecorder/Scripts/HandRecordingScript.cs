using Microsoft.MixedReality.Toolkit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using System.IO;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine.Events;
using TMPro;
using UnityEngine.UI;
#if UNITY_WSA && !UNITY_EDITOR
using Windows.Storage;
#endif
using System.Threading.Tasks;



public class HandRecordingScript : MonoBehaviour
{
    public Material handMeshMaterial;

    #region Variables for Recording Functionality

    bool recording = false;

    // Data Storage for recorded frames
    FrameCollection leftHandData = new FrameCollection();
    FrameCollection rightHandData = new FrameCollection();

    // Variables for GameObject recording
    GameObject primitiveSelectorContainer;
    [NonSerialized]
    public GameObject gameobjectToRecord = null;
    Vector3 gameobjectRecordOriginalPos;
    TransformRecording transformRecording = null;

    // For user to insert previous recording files
    public TextAsset handRecordingJSON = null;
    public TextAsset gameobjectRecordingJSON = null;

    int maxFrames = 0;

    // The start and end of the recording (cropped and not)
    int startFrame = 0;
    int endFrame = 0;
    GameObject startLimit;
    GameObject endLimit;

    #endregion

    #region Variables for Recorder UI

    // Instantiate left and right hand mesh GOs
    // These are what displays the recorded handmeshes during playback
    GameObject leftHand;
    GameObject rightHand;
    MeshFilter leftHandDisplayFilter;
    MeshFilter rightHandDisplayFilter;

    bool leftHandPlayback = true;
    bool rightHandPlayback = true;

    // Start and end frame are the cropped points by user
    public GameObject scrubBar;
    PinchSlider pinchSlider;
    GameObject sliderThumb;

    // Button Colors for toggle
    Color buttonBlue = new Color(0.188f, 0.325f, 1.0f);
    Color buttonGray = new Color(0.4f, 0.4f, 0.4f);

    GameObject leftHandEnabledButtonQuad;
    GameObject rightHandEnabledButtonQuad;
    GameObject recordStartStopButtonQuad;


    // For the user to attach all the buttons they will need
    public GameObject leftHandEnabledButton = null;
    public GameObject rightHandEnabledButton = null;
    public GameObject playRecordingButton = null;
    public GameObject recordStartStopButton = null;
    public GameObject saveToFileButton = null;
    public GameObject setEndFrameButton = null;
    public GameObject setStartFrameButton = null;
    public GameObject addKeyframeButton = null;
    public GameObject clearAllKeyframesButton = null;
    public GameObject spawnGameObjectButton = null;
    public GameObject deleteGOToRecordButton = null;


    Coroutine buttonBlinkCoroutine = null;
    #endregion

    #region Variables for Keyframe Functionality
    // Stores the keyframes
    List<float> keyframeList = new List<float>();
    // Stores the GameObject markers for the keyframe 
    List<GameObject> keyframeMarkerList = new List<GameObject>();
    #endregion

    void Start()
    {
        #region Setting up GameObject Spawner
        primitiveSelectorContainer = GameObject.Find("PrimitiveSelectorContainer");
        primitiveSelectorContainer.SetActive(false);
        #endregion

        #region Setting up left and right hand models for Playback Visualisation
        // Create the left and right hand models
        leftHand = new GameObject("Left Hand Model");
        leftHand.transform.parent = gameObject.transform;
        leftHandDisplayFilter = leftHand.AddComponent<MeshFilter>();
        leftHand.AddComponent<MeshRenderer>().material = handMeshMaterial;

        rightHand = new GameObject("Right Hand Model");
        rightHand.transform.parent = gameObject.transform;
        rightHandDisplayFilter = rightHand.AddComponent<MeshFilter>();
        rightHand.AddComponent<MeshRenderer>().material = handMeshMaterial;
        #endregion

        #region Setting up OnClick Events for UI Buttons
        if (leftHandEnabledButton != null)
        {
            leftHandEnabledButtonQuad = leftHandEnabledButton.transform.GetChild(0).transform.GetChild(0).gameObject;
            leftHandEnabledButton.GetComponent<Interactable>().OnClick.AddListener(() => ToggleLeftHandPlayback());
        }
        if (rightHandEnabledButton != null)
        {
            rightHandEnabledButtonQuad = rightHandEnabledButton.transform.GetChild(0).transform.GetChild(0).gameObject;
            rightHandEnabledButton.GetComponent<Interactable>().OnClick.AddListener(() => ToggleRightHandPlayback());
        }
        if (playRecordingButton != null)
        {
            playRecordingButton.GetComponent<Interactable>().OnClick.AddListener(() => StartPlayback());
        }
        if (recordStartStopButton != null)
        {
            recordStartStopButton.GetComponent<Interactable>().OnClick.AddListener(() => ToggleRecording());
            recordStartStopButtonQuad = recordStartStopButton.transform.GetChild(0).transform.GetChild(0).gameObject;
            UnityAction startToRecord = null;
            startToRecord += StartRecording;
            recordStartStopButton.GetComponent<SpeechInputHandler>().AddResponse("Start", startToRecord);
            UnityAction endRecord = null;
            endRecord += StopRecording;
            recordStartStopButton.GetComponent<SpeechInputHandler>().AddResponse("Stop", endRecord);
        }
        if (saveToFileButton != null)
        {
            saveToFileButton.GetComponent<Interactable>().OnClick.AddListener(() => SaveRecording());
        }
        if (setEndFrameButton != null)
        {
            setEndFrameButton.GetComponent<Interactable>().OnClick.AddListener(() => SetEndFrame());
        }
        if (setStartFrameButton != null)
        {
            setStartFrameButton.GetComponent<Interactable>().OnClick.AddListener(() => SetStartFrame());
        }
        if (scrubBar != null)
        {
            scrubBar.GetComponent<PinchSlider>().OnValueUpdated.AddListener((evt) => SlideToFrame(evt));
        }
        if (addKeyframeButton != null)
        {
            addKeyframeButton.GetComponent<Interactable>().OnClick.AddListener(() => AddKeyframeAtScrubber());
        }
        if (clearAllKeyframesButton != null)
        {
            clearAllKeyframesButton.GetComponent<Interactable>().OnClick.AddListener(() => ClearAllKeyframes());
        }
        if (spawnGameObjectButton != null)
        {
            spawnGameObjectButton.GetComponent<Interactable>().OnClick.AddListener(() => SpawnGOToRecord());
        }
        if (deleteGOToRecordButton != null)
        {
            deleteGOToRecordButton.GetComponent<Interactable>().OnClick.AddListener(() => DeleteGOToRecord());
        }
        #endregion

        #region Setting up Scrubbing Bar
        // Place the start and end limit GOs at slider value 0 and 1 positions
        pinchSlider = scrubBar.GetComponent<PinchSlider>();
        startLimit = scrubBar.transform.GetChild(2).gameObject;
        endLimit = scrubBar.transform.GetChild(3).gameObject;
        sliderThumb = scrubBar.transform.GetChild(0).gameObject;
        #endregion

        #region Setting up attached previous Hand and GO recordings
        if (gameobjectRecordingJSON != null)
        {
            transformRecording = JsonUtility.FromJson<TransformRecording>(gameobjectRecordingJSON.text);
            maxFrames = transformRecording.position.Count;
        }

        // Load the attached JSON text file if present
        if (handRecordingJSON != null)
        {
            Recording loadedRecord = JsonUtility.FromJson<Recording>(handRecordingJSON.text);
            // Load the hand recording frames from the JSON
            leftHandData = loadedRecord.hands[0];
            rightHandData = loadedRecord.hands[1];
            startFrame = 0;
            maxFrames = Math.Max(leftHandData.frameList.Count, maxFrames);
            maxFrames = Math.Max(rightHandData.frameList.Count, maxFrames);
            endFrame = maxFrames;
            // Load the recorded keyframes from the JSON
            keyframeList = loadedRecord.keyframes;
            // Place the keyframe markers by converting seconds to frames and to slider value
            foreach (float keyframe in keyframeList)
            {
                // Set the slider to that position
                pinchSlider.SliderValue = keyframe / Time.fixedDeltaTime / endFrame;
                // Make a marker at that position
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                sphere.transform.position = sliderThumb.transform.position;
                sphere.SetActive(false);
                // Add the marker to the list of markers
                keyframeMarkerList.Add(sphere);
            }
        }
        scrubBar.SetActive(false);
        leftHand.transform.position = new Vector3(-100, -100, -100);
        rightHand.transform.position = new Vector3(-100, -100, -100);
    }
    #endregion


    void FixedUpdate()
    {
        #region Identifying user hands for recording
        // Search for left and right hand GOs in scene and assign them to the correct hand
        GameObject generatedLeftHand = null;
        GameObject generatedRightHand = null;
        List<GameObject> unidentifiedHands = new List<GameObject>();
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject go in allObjects)
        {
            if (go.name.Contains(CoreServices.InputSystem.InputSystemProfile.HandTrackingProfile.HandMeshPrefab.name + "(Clone)"))
            {
                unidentifiedHands.Add(go);
            }
        }
        foreach (GameObject hand in unidentifiedHands)
        {
            if (hand.name.Contains("Left"))
            {
                generatedLeftHand = hand;
            }
            else if (hand.name.Contains("Right"))
            {
                generatedRightHand = hand;
            }
        }
        #endregion

        if (recording)
        {
            #region Record each frame for left and right Hands
            // Create a new frame and record for leftHand and rightHand if not null
            // If null, insert an empty frame into the FrameCollection
            if (generatedLeftHand != null)
            {
                MeshFilter mshf = generatedLeftHand.GetComponent<MeshFilter>();
                if (mshf != null)
                {
                    if (mshf.mesh != null)
                    {
                        // Create a new Frame
                        Frame newLeftFrame = new Frame();
                      
                        newLeftFrame.vertices = mshf.sharedMesh.vertices;
                        newLeftFrame.normals = mshf.sharedMesh.normals;
                        newLeftFrame.triangles = mshf.sharedMesh.triangles;
                        newLeftFrame.uvs = mshf.sharedMesh.uv;

                        newLeftFrame.rotation = mshf.transform.rotation;
                        newLeftFrame.position = mshf.transform.position;
                        leftHandData.frameList.Add(newLeftFrame);
                    }
                }
            }
            else
            {
                Frame newLeftFrame = new Frame();
                newLeftFrame.vertices = new Vector3[] { };
                newLeftFrame.normals = new Vector3[] { };
                newLeftFrame.triangles = new int[] { };
                newLeftFrame.uvs = new Vector2[] { };

                newLeftFrame.rotation = new Quaternion(0, 0, 0, 0);
                newLeftFrame.position = new Vector3(-100, -100, -100);
                leftHandData.frameList.Add(newLeftFrame);
            }
            

            if (generatedRightHand != null)
            {
                MeshFilter mshf = generatedRightHand.GetComponent<MeshFilter>();
                if (mshf != null)
                {
                    if (mshf.mesh != null)
                    {
                        Frame newRightFrame = new Frame();
                        newRightFrame.vertices = mshf.sharedMesh.vertices;
                        newRightFrame.normals = mshf.sharedMesh.normals;
                        newRightFrame.triangles = mshf.sharedMesh.triangles;
                        newRightFrame.uvs = mshf.sharedMesh.uv;


                        newRightFrame.rotation = mshf.transform.rotation;
                        newRightFrame.position = mshf.transform.position;
                        rightHandData.frameList.Add(newRightFrame);
                    }
                }
            }

            else
            {
                Frame newRightFrame = new Frame();
                newRightFrame.vertices = new Vector3[] { };
                newRightFrame.normals = new Vector3[] { };
                newRightFrame.triangles = new int[] { };
                newRightFrame.uvs = new Vector2[] { };

                newRightFrame.rotation = new Quaternion(0, 0, 0, 0);
                newRightFrame.position = new Vector3(-100, -100, -100);
                rightHandData.frameList.Add(newRightFrame);
            }
            #endregion

            #region Record GameObject change in transform if applicable
            // record GameObject if attached
            if (gameobjectToRecord != null)
            {
                transformRecording.position.Add(gameobjectToRecord.transform.position - gameobjectRecordOriginalPos);
                transformRecording.rotation.Add(gameobjectToRecord.transform.rotation);
                transformRecording.localScale.Add(gameobjectToRecord.transform.localScale);
            }
            #endregion
        }
    }

    // Functions

    #region Start/Stop Recording

    // ToggleRecording is used for button onClick activation to toggle recording state
    public void ToggleRecording()
    {
        if (recording) { StopRecording(); }
        else { StartRecording(); }
    }

    // Start and Stop recording functions are used in ToggleRecording, as well as for speech commands
    public void StartRecording()
    {
        recording = true;
        // clear all prior recorded in memory
        leftHandData = new FrameCollection();
        rightHandData = new FrameCollection();
        startFrame = 0;
        endFrame = 0;
        if (buttonBlinkCoroutine == null) { buttonBlinkCoroutine = StartCoroutine(RecordButtonBlink()); }
        if (gameobjectToRecord != null)
        {
            gameobjectRecordOriginalPos = gameobjectToRecord.transform.position;
            transformRecording = new TransformRecording();
        }
        else
        {
            transformRecording = null;
        }
        scrubBar.SetActive(false);
        leftHand.transform.position = new Vector3(-100, -100, -100);
        rightHand.transform.position = new Vector3(-100, -100, -100);
        // Hide all the keyframe spheres
        foreach (GameObject keyframeSphere in keyframeMarkerList)
        {
            keyframeSphere.SetActive(false);
        }
    }

    public void StopRecording()
    {
        recording = false;
        startFrame = 0;
        endFrame = leftHandData.frameList.Count;
        recordStartStopButtonQuad.GetComponent<Renderer>().material.color = buttonBlue;
        StopCoroutine(buttonBlinkCoroutine);
        buttonBlinkCoroutine = null;
        maxFrames = Math.Max(rightHandData.frameList.Count, leftHandData.frameList.Count);
        if (transformRecording != null)
        {
            maxFrames = Math.Max(transformRecording.position.Count, maxFrames);
        }
    }

    // This is for the aesthetics of blinking record button
    IEnumerator RecordButtonBlink()
    {
        while (true)
        {
            recordStartStopButtonQuad.GetComponent<Renderer>().material.color = Color.red;
            yield return new WaitForSeconds(1);
            recordStartStopButtonQuad.GetComponent<Renderer>().material.color = buttonBlue;
            yield return new WaitForSeconds(1);
        }
    }

    #endregion

    #region Save Recording


    public void SaveRecording()
    {
        _ = WriteToFileAsync(leftHandPlayback, rightHandPlayback);
    }

    public async Task WriteToFileAsync(bool saveLeftHand, bool saveRightHand)
    {
        string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HH_mm_ss");

        Recording newRecord = new Recording();
        FrameCollection emptyCollection = new FrameCollection();

        // Only include the keyframes that are between start and end frame
        // Also adjust the keyframe timings to become the accurate duration after new startframe
        List<float> finalKeyframeList = new List<float>();
        foreach (float keyframe in keyframeList)
        {
            int keyframeFrame = (int)(keyframe / Time.fixedDeltaTime);
            if (keyframeFrame >= startFrame && keyframeFrame < endFrame)
            {
                float keyframeAccurateTime = (keyframeFrame - startFrame) * Time.fixedDeltaTime;
                finalKeyframeList.Add(keyframeAccurateTime);
            }
        }
        newRecord.keyframes = finalKeyframeList;

        // Only retrieve the frames between startFrame and endFrame for saving
        FrameCollection leftHandDataToSave = new FrameCollection();
        for (int i = 0; i < leftHandData.frameList.Count; i++)
        {
            if (i >= startFrame && i < endFrame) { leftHandDataToSave.frameList.Add(leftHandData.frameList[i]); }
        }
        FrameCollection rightHandDataToSave = new FrameCollection();
        for (int i = 0; i < rightHandData.frameList.Count; i++)
        {
            if (i >= startFrame && i < endFrame) { rightHandDataToSave.frameList.Add(rightHandData.frameList[i]); }
        }


        if (transformRecording != null)
        {
            // Only retrieve the frames to be saved
            TransformRecording transformRecordingToSave = new TransformRecording();
            for (int i = 0; i < transformRecording.position.Count; i++)
            {
                if (i >= startFrame && i < endFrame)
                {
                    transformRecordingToSave.position.Add(transformRecording.position[i]);
                    transformRecordingToSave.rotation.Add(transformRecording.rotation[i]);
                    transformRecordingToSave.localScale.Add(transformRecording.localScale[i]);
                }
            }

            string transformRecordingJSON = JsonUtility.ToJson(transformRecordingToSave);

#if UNITY_WSA && !UNITY_EDITOR
            StorageFolder goStorageFolder = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.MusicLibrary);
            StorageFile goTextFileForWrite = null;
            try
            {
                string fileName = "GORecording" + "_" + currentDateTime + ".txt";
                goTextFileForWrite = await goStorageFolder.CreateFileAsync(fileName);
                Debug.Log("File created with name: " + fileName);
            }
            catch (Exception)
            {
                Debug.Log("Error creating file");
            }
            if (goTextFileForWrite != null)
            {
                await FileIO.WriteTextAsync(goTextFileForWrite, transformRecordingJSON);
                Debug.Log("Recording saved to file");
            }
            else
            {
                Debug.Log("File is null");
            }
#else
            string docuPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

            using (StreamWriter outputFile = new StreamWriter(Path.Combine(docuPath, "GORecording" + "_" + currentDateTime + ".txt")))
            {
                outputFile.Write(transformRecordingJSON);
            }
#endif
        }

        // Only retrieve the FrameCollection for the hands selected to be saved
        if (saveLeftHand) { newRecord.hands.Add(leftHandDataToSave); }
        else { newRecord.hands.Add(emptyCollection); }
        if (saveRightHand) { newRecord.hands.Add(rightHandDataToSave); }
        else { newRecord.hands.Add(emptyCollection); }

        // JSON text file is saved in the music library. Make sure permssion is set in app manifest
        string jsonStore = JsonUtility.ToJson(newRecord);
#if UNITY_WSA && !UNITY_EDITOR
        StorageFolder storageFolder = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.MusicLibrary);
        StorageFile textFileForWrite = null;
        try
        {
            string fileName = "HandRecording" + "_" + currentDateTime + ".txt";
            textFileForWrite = await storageFolder.CreateFileAsync(fileName);
            Debug.Log("File created with name: " + fileName);
        }
        catch (Exception)
        {
            Debug.Log("Error creating file");
        }
        if (textFileForWrite != null)
        {
            await FileIO.WriteTextAsync(textFileForWrite, jsonStore);
            Debug.Log("Recording saved to file");
        }
        else
        {
            Debug.Log("File is null");
        }
#else
        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

        using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "HandRecording" + "_" + currentDateTime + ".txt")))
        {
            outputFile.Write(jsonStore);
        }
#endif
    }
    #endregion

    #region Playback Recording

    public void StartPlayback()
    {
        scrubBar.SetActive(true);
        // Set position of start and end limits
        float initialSliderValue = pinchSlider.SliderValue;
        pinchSlider.SliderValue = (float)startFrame / maxFrames;
        startLimit.transform.position = sliderThumb.transform.position;
        pinchSlider.SliderValue = (float)endFrame / maxFrames;
        endLimit.transform.position = sliderThumb.transform.position;
        pinchSlider.SliderValue = initialSliderValue;
        // Show all the keyframe spheres
        foreach (GameObject keyframeSphere in keyframeMarkerList)
        {
            keyframeSphere.SetActive(true);
        }
        // Play back the recording
        StartCoroutine(PlaybackHandRecording());
    }

    // Sets the slider to the correct value for a given frame
    // This will trigger the OnValueUpdated event, which will call SlideToFrame function below
    IEnumerator PlaybackHandRecording()
    {
        scrubBar.SetActive(true);
        //for (int i = 0; i < handData.frameList.Count; i++)
        for (int i = startFrame; i < endFrame; i++)
        {
            // This edits the slider value which will trigger SlideToFrame function
            if (transformRecording != null)
            {
                maxFrames = Math.Max(maxFrames, transformRecording.position.Count);
            }
            pinchSlider.SliderValue = (float)i / maxFrames;
            yield return new WaitForFixedUpdate();

        }
        yield break;
    }

    // This displays the correct frame when sliderValue is updated
    public void SlideToFrame(SliderEventData sliderData)
    {
        // From slider value of zero to one, find corresponding frame in recording
        if (transformRecording != null)
        {
            maxFrames = Math.Max(maxFrames, transformRecording.position.Count);
        }
        float totalVidLen = maxFrames * Time.fixedDeltaTime;
        TextMeshPro videoTimeDisplay = scrubBar.transform.GetChild(4).GetComponent<TextMeshPro>();
        if (maxFrames != 0)
        {
            int frameNumber = (int)(sliderData.NewValue * maxFrames);
            if (frameNumber == 0) { frameNumber = 1; }
            if (frameNumber == maxFrames) { frameNumber -= 1; }
            // Set mesh to display at current frame
            if (leftHandPlayback) { DisplayFrame(leftHandData, leftHandDisplayFilter, frameNumber); }
            else { leftHandDisplayFilter.transform.position = new Vector3(-100, -100, -100); }
            if (rightHandPlayback) { DisplayFrame(rightHandData, rightHandDisplayFilter, frameNumber); }
            else { rightHandDisplayFilter.transform.position = new Vector3(-100, -100, -100); }
            // Display the GameObject at the current frame
            if (gameobjectToRecord != null)
            {
                gameobjectToRecord.transform.position = gameobjectRecordOriginalPos + transformRecording.position[frameNumber];
                gameobjectToRecord.transform.rotation = transformRecording.rotation[frameNumber];
                gameobjectToRecord.transform.localScale = transformRecording.localScale[frameNumber];
            }
            // Display the timing
            float currentVidLen = frameNumber * Time.fixedDeltaTime;
            if (frameNumber == maxFrames - 2) { currentVidLen = totalVidLen; }
            videoTimeDisplay.text = string.Format("{0:0.0} / {1:0.0} s", currentVidLen, totalVidLen);
        }
    }

    // Displays a selected frame
    public void DisplayFrame(FrameCollection handData, MeshFilter modelHandMeshFilter, int frameNumber)
    {
        modelHandMeshFilter.mesh.Clear();
        if (frameNumber < handData.frameList.Count)
        {
            Frame currentFrame = handData.frameList[frameNumber];
            modelHandMeshFilter.sharedMesh.vertices = currentFrame.vertices;
            //modelHandMeshFilter.sharedMesh.normals = currentFrame.normals;
            modelHandMeshFilter.sharedMesh.triangles = currentFrame.triangles;
            modelHandMeshFilter.sharedMesh.uv = currentFrame.uvs;
            modelHandMeshFilter.sharedMesh.RecalculateBounds();

            modelHandMeshFilter.transform.rotation = currentFrame.rotation;
            modelHandMeshFilter.transform.position = new Vector3(0, 0.1f, 0.5f) + currentFrame.position;
        }
    }

    #endregion

    #region UI Functions

    public void ToggleLeftHandPlayback()
    {
        leftHandPlayback = !leftHandPlayback;
        if (leftHandPlayback) { leftHandEnabledButtonQuad.GetComponent<Renderer>().material.color = buttonBlue; }
        else { leftHandEnabledButtonQuad.GetComponent<Renderer>().material.color = buttonGray; }
    }
    public void ToggleRightHandPlayback()
    {
        rightHandPlayback = !rightHandPlayback;
        if (rightHandPlayback) { rightHandEnabledButtonQuad.GetComponent<Renderer>().material.color = buttonBlue; }
        else { rightHandEnabledButtonQuad.GetComponent<Renderer>().material.color = buttonGray; }
    }

    public void SetStartFrame()
    {
        int newStartFrame = (int)(pinchSlider.SliderValue * maxFrames);
        if (newStartFrame >= endFrame) { Debug.Log("Failed to set StartFrame. StartFrame must be before EndFrame"); }
        else
        {
            startFrame = newStartFrame;
            startLimit.transform.position = sliderThumb.transform.position;

        }
    }
    public void SetEndFrame()
    {
        int newEndFrame = (int)(pinchSlider.SliderValue * maxFrames);
        if (newEndFrame <= startFrame) { Debug.Log("Failed to set EndFrame. EndFrame must be after StartFrame"); }
        else
        {
            endFrame = newEndFrame;
            endLimit.transform.position = sliderThumb.transform.position;
        }
    }

    public void AddKeyframeAtScrubber()
    {
        // Convert the current scrubber value to seconds
        float seconds = (int)(pinchSlider.SliderValue * maxFrames) * Time.fixedDeltaTime;
        // Check if there is already such a keyframe
        if (!keyframeList.Contains(seconds))
        {
            keyframeList.Add(seconds);
            // Make a marker at that position
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            sphere.transform.position = sliderThumb.transform.position;
            // Add the marker to the list of markers
            keyframeMarkerList.Add(sphere);
        }
    }

    public void ClearAllKeyframes()
    {
        // Find all the keyframe objects and delete them
        for (int i = 0; i < keyframeList.Count; i++)
        {
            Destroy(keyframeMarkerList[i]);
        }
        keyframeMarkerList = new List<GameObject>();
        keyframeList = new List<float>();
    }

    #endregion

    #region GameObject For Recording

    public void SpawnGOToRecord()
    {
        primitiveSelectorContainer.SetActive(true);
        GameObject handRecorderContainer = gameObject.transform.parent.gameObject;
        primitiveSelectorContainer.transform.GetChild(0).GetComponent<PrimitiveSelectorScript>().handRecorderContainer = handRecorderContainer;
        primitiveSelectorContainer.transform.GetChild(0).GetComponent<PrimitiveSelectorScript>().handRecorder = gameObject;
        handRecorderContainer.SetActive(false);
    }

    public void DeleteGOToRecord()
    {
        if (gameobjectToRecord != null)
        {
            Destroy(gameobjectToRecord);
            gameobjectToRecord = null;
        }
    }

    #endregion

}
