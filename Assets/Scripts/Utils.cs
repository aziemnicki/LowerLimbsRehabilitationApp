using System.Collections.Generic;
using UnityEngine;
using System;


public class BoundingBox
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

public class Keypoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Confidence { get; set; }

    public override string ToString()
    {
        return $"{X}, {Y}, {Confidence}";
    }
}

public class PoseEstimationResult
{
    public BoundingBox Bbox { get; set; }
    public List<Keypoint> Keypoints { get; set; }
    public int LabelIdx { get; set; }
    public float Confidence { get; set; }
    public Rect Rect
    {
        get { return new Rect(Bbox.X, Bbox.Y, Bbox.Width, Bbox.Height); }
    }

    public override string ToString()
    {
        return $"{Confidence}: {Bbox}: [{string.Join(", ", Keypoints)}]";
    }
}
