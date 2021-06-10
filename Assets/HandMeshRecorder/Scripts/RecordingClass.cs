using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Frame // Holds one frame of recording
{

    public Vector3[] vertices;
    public Vector3[] normals;
    public int[] triangles;
    public Vector2[] uvs;
    public Quaternion rotation;
    public Vector3 position;
}

[Serializable]
public class FrameCollection
{
    public List<Frame> frameList = new List<Frame>();
}

[Serializable]
public class Recording
{
    public List<FrameCollection> hands = new List<FrameCollection>();
    // Stores the saved keyframes
    public List<float> keyframes = new List<float>();
}

[Serializable]
public class TransformRecording
{
    // position is the change in position from location of GO in first frame
    public List<Vector3> position = new List<Vector3>();
    public List<Quaternion> rotation = new List<Quaternion>();
    public List<Vector3> localScale = new List<Vector3>();
}
