using System.Collections;
using System.Collections.Generic;
using System;
using System.IO; 
using System.Linq;
using System.Data;
using Unity.Sentis.Layers;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.UI;


using System.Threading.Tasks;
using UnityEngine.XR;
using UnityEngine.XR.WSA;
using UnityEngine.XR.WSA.Input;
using UnityEngine;
using Unity.Sentis;
using TMPro;

using HoloLensCameraStream;

#if WINDOWS_UWP && XR_PLUGIN_OPENXR
using Windows.Perception.Spatial;
#endif

#if WINDOWS_UWP
using Windows.UI.Input.Spatial;
using Windows.Storage;
using Windows.Storage.Streams;
#endif


public class Detection : MonoBehaviour, IMixedRealityGestureHandler
{
    public float confidenceThreshold;
    public int imgSizeWidth = 320;
    public int imgSizeHeight = 320;
    public int inferenceImgSize = 320;

    public int keypointsNumber = 6;
    public ModelAsset modelAsset;
    private Model runtimeModel;
    private IWorker worker;
    public IBackend backend;
    private TensorFloat resultTensor;
    private TextMeshPro textMesh;
    private TextMeshProUGUI stepstext;

    private Texture2D pictureTexture;
    private Texture2D inputTexture;
    private Texture2D annotatedTexture;
    private Texture2D croppedTexture;

    private Camera mainCamera;
    private HoloLensCameraStream.Resolution resolution;
    public int cameraResolutionWidth;
    public int cameraResolutionHeight;
    private VideoCapture videoCapture;

    Matrix4x4 camera2WorldMatrix;
    Matrix4x4 projectionMatrix;
    Matrix4x4 camera2WorldMatrix_local;
    Matrix4x4 projectionMatrix_local;

    public Material laserMaterial;

    private IMixedRealitySpatialAwarenessMeshObserver meshObserver;
    private List<PoseEstimationResult> results;
    private List<Tuple<GameObject, Renderer>> labels;

    // private GameObject cubeObject;
    private List<GameObject> cylinderObjects = new List<GameObject>();
    public GameObject cylinderPrefab;
    private Vector3 plain_position;
    public GameObject left_leg_prefab;
    private GameObject left_leg;
    public GameObject right_leg_prefab;
    private GameObject right_leg;
    private GameObject pause_menu;
    private GameObject start_menu;
    private GameObject finish;

    public float hip_width;
    private bool first_step_reached;
    private bool is_collision = false;
    private bool isPaused = false;
    private bool isStarted = false;

    private string triggeringObject;
    private int step_counter;
    public int steps_to_finish = 5;
    private PinchSlider pinchSlider;
    private float min_distance = 0.3f;
    private float step_length = 0.4f;
    private Vector3 main_direction;
    private Vector3 start_direction;


#if WINDOWS_UWP && XR_PLUGIN_OPENXR
    SpatialCoordinateSystem _spatialCoordinateSystem;
#endif

    private byte[] _latestImageBytes;
    private bool stopVideo;

    private class SampleStruct
    {
        public float[] camera2WorldMatrix, projectionMatrix;
        public byte[] data;
    }

    void Start()
    {
#if WINDOWS_UWP

#if XR_PLUGIN_OPENXR
        _spatialCoordinateSystem = Microsoft.MixedReality.OpenXR.PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as SpatialCoordinateSystem;

#endif
#endif
        CameraStreamHelper.Instance.GetVideoCaptureAsync(OnVideoCaptureCreated);

        runtimeModel = ModelLoader.Load(modelAsset);
        if (runtimeModel != null)
        {
            worker = WorkerFactory.CreateWorker(BackendType.GPUCompute, runtimeModel);
            Debug.Log("Model loaded properly.");
        }
        else
        {
            Debug.LogError("Model not initialized properly.");
        }
        
        inputTexture = new Texture2D(imgSizeWidth, imgSizeHeight, TextureFormat.RGB24, false);

        results = new List<PoseEstimationResult>();
        labels = new List<Tuple<GameObject, Renderer>>();
        // cubeObject = GameObject.Find("Cube");
        pause_menu = GameObject.Find("PauseMenu");
        start_menu = GameObject.Find("StartMenu");
        finish = GameObject.Find("Finish");
        var panel = GameObject.Find("StartPanel");
        pinchSlider = panel.GetComponentInChildren<PinchSlider>();
        pinchSlider.OnValueUpdated.AddListener(UpdateText);


        left_leg = Instantiate(left_leg_prefab, new Vector3(0, 0, 0), Quaternion.identity);
        right_leg = Instantiate(right_leg_prefab, new Vector3(0, 0, 0), Quaternion.identity);
        left_leg.SetActive(false);
        right_leg.SetActive(false);
        pause_menu.SetActive(false);
        start_menu.SetActive(true);
        finish.SetActive(false);
        isPaused = true;

        hip_width = 0.3f;
        step_counter = -2;
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityGestureHandler>(this);

        var _ = GameObject.Find("Prediction");
        textMesh = _.GetComponent<TextMeshPro>();
        stepstext = panel.GetComponentInChildren<TextMeshProUGUI>();
        stepstext.text = $"Choose number of steps: 3"; // Format to 2 decimal places

       
        meshObserver = CoreServices.GetSpatialAwarenessSystemDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();
        if (meshObserver != null)
        {
            meshObserver.DisplayOption = SpatialAwarenessMeshDisplayOptions.Visible;
        }
        
        StartCoroutine(wait(2.0f));
        mainCamera = Camera.main;
        StartCoroutine(waitForStart());

    }

    private IEnumerator wait(float time)
    {
        yield return new WaitForSeconds(time);
    }
    private IEnumerator waitForStart()
    {
        yield return new WaitUntil(() => isStarted);
        StartCoroutine(DetectWebcam());
    }

    public void SetStart()
    {
        cylinderObjects.Clear();
        if (mainCamera != null)
        {
            float first_step = 0.5f;
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 firstPosition = mainCamera.transform.position + cameraForward * first_step;
            DisplayFirstStep(firstPosition, plain_position);
            first_step_reached = false;
            start_menu.SetActive(false);
        }

        Vector3 left_leg_position = mainCamera.transform.position;
        Vector3 right_leg_position = mainCamera.transform.position;
        left_leg_position.y = plain_position.y - 0.1f;
        right_leg_position.y = plain_position.y - 0.1f;
        left_leg.transform.position = left_leg_position;
        right_leg.transform.position = right_leg_position;

        left_leg.SetActive(true);
        right_leg.SetActive(true);
        isStarted = true;
        isPaused = false;
        
    }
    
    public void ReturnToMenu()
    {
        ResetScene();
        isStarted = false;
        pause_menu.SetActive(false); // Hide the pause menu
        finish.SetActive(false);
        PositionObjectInFrontOfCamera(start_menu);
        StartCoroutine(wait(1.0f));
        start_menu.SetActive(true);
    }

    private void ResetScene()
    {
        foreach ( GameObject obj in cylinderObjects)
        {
            Destroy(obj);
        }
        cylinderObjects.Clear();
        left_leg.SetActive(false);
        right_leg.SetActive(false);
        step_counter = -2;
    }

    private void TapPause()
    {
        if (isStarted)
        {
            isPaused = !isPaused;
            if (isPaused)
            {
                pause_menu.SetActive(true); // Show the pause menu
                PositionObjectInFrontOfCamera(pause_menu);
            }
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (!isPaused)
        {
            Time.timeScale = 1f; // Resume the game
            pause_menu.SetActive(false); // Hide the pause menu
        }
    }

    private void OnDestroy()
    {
        if (videoCapture == null)
            return;

        videoCapture.FrameSampleAcquired += null;
        videoCapture.Dispose();
        worker.Dispose();
    }

    public void UpdateText(SliderEventData eventData)
    {
        // Get the current value of the slider
        float sliderValue = eventData.NewValue*17;
        sliderValue += 3;
        // Set the text of the TextMeshPro component
        stepstext.text = $"Choose number of steps: {sliderValue.ToString("F2")}"; // Format to 2 decimal places
        steps_to_finish = Mathf.RoundToInt(sliderValue);
    }


    public void OnGestureStarted(InputEventData eventData)
    {
        Debug.Log("Gesture started.");
    }

    public void OnGestureUpdated(InputEventData eventData)
    {
        // Handle gesture update
        Debug.Log("Gesture updated.");
    }

    public void OnGestureCompleted(InputEventData eventData)
    {
        Debug.Log("Gesture completed.");
        TapPause();
        
    }

    public void OnGestureCanceled(InputEventData eventData)
    {
        // Handle gesture cancellation
        Debug.Log("Gesture canceled.");
    }

    private void OnVideoCaptureCreated(VideoCapture v)
    {
        if (v == null)
        {
            Debug.LogError("No VideoCapture found");
            return;
        }

        videoCapture = v;

#if WINDOWS_UWP
#if XR_PLUGIN_OPENXR
        CameraStreamHelper.Instance.SetNativeISpatialCoordinateSystem(_spatialCoordinateSystem);

#endif
#endif

        resolution = videoCapture.GetSupportedResolutions().OrderBy((r) => r.width * r.height).ElementAt(2);
        resolution = new HoloLensCameraStream.Resolution(cameraResolutionWidth, cameraResolutionHeight);
        float frameRate = CameraStreamHelper.Instance.GetHighestFrameRate(resolution);

        videoCapture.FrameSampleAcquired += OnFrameSampleAcquired;

        CameraParameters cameraParams = new CameraParameters();
        cameraParams.cameraResolutionHeight = resolution.height;
        cameraParams.cameraResolutionWidth = resolution.width;
        cameraParams.frameRate = Mathf.RoundToInt(frameRate);
        cameraParams.pixelFormat = CapturePixelFormat.BGRA32;

        UnityEngine.WSA.Application.InvokeOnAppThread(() => { pictureTexture = new Texture2D(resolution.width, resolution.height, TextureFormat.BGRA32, false); }, false);

        videoCapture.StartVideoModeAsync(cameraParams, OnVideoModeStarted);

        Debug.LogWarning($"{resolution.height},  {resolution.width}, {cameraParams.frameRate}");
    }

    private void OnVideoModeStarted(VideoCaptureResult result)
    {
        if (result.success == false)
        {
            Debug.LogWarning("Could not start video mode.");
            return;
        }

        Debug.Log("Video capture started.");
    }

    private void OnFrameSampleAcquired(VideoCaptureSample sample)
    {
        // Allocate byteBuffer
        if (_latestImageBytes == null || _latestImageBytes.Length < sample.dataLength)
            _latestImageBytes = new byte[sample.dataLength];

        // Fill frame struct 
        SampleStruct s = new SampleStruct();
        sample.CopyRawImageDataIntoBuffer(_latestImageBytes);
        s.data = _latestImageBytes;

        // Get the cameraToWorldMatrix and projectionMatrix
        if (!sample.TryGetCameraToWorldMatrix(out s.camera2WorldMatrix) || !sample.TryGetProjectionMatrix(out s.projectionMatrix))
            return;

        sample.Dispose();

        camera2WorldMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(s.camera2WorldMatrix);
        projectionMatrix = LocatableCameraUtils.ConvertFloatArrayToMatrix4x4(s.projectionMatrix);

        UnityEngine.WSA.Application.InvokeOnAppThread(() =>
        {
            pictureTexture.LoadRawTextureData(s.data);
            pictureTexture.Apply();

            Vector3 inverseNormal = -camera2WorldMatrix.GetColumn(2);
            // Position the canvas object slightly in front of the real world web camera.
            Vector3 imagePosition = camera2WorldMatrix.GetColumn(3) - camera2WorldMatrix.GetColumn(2);

#if XR_PLUGIN_WINDOWSMR || XR_PLUGIN_OPENXR

            Camera unityCamera = Camera.main;
            Matrix4x4 invertZScaleMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1));
            Matrix4x4 localToWorldMatrix = camera2WorldMatrix * invertZScaleMatrix;
            unityCamera.transform.localPosition = localToWorldMatrix.GetColumn(3);
            unityCamera.transform.localRotation = Quaternion.LookRotation(localToWorldMatrix.GetColumn(2), localToWorldMatrix.GetColumn(1));
#endif
        }, false);
    }

    void Update()
    {
        // Renderer renderer = cubeObject.GetComponent<Renderer>();
        // renderer.material.mainTexture = annotatedTexture;
        
        // PositionCubeObjectInFrontOfCamera();
        if(isPaused)
        {
        PositionObjectInFrontOfCamera(start_menu);
        PositionObjectInFrontOfCamera(pause_menu);
        PositionObjectInFrontOfCamera(finish);
        }
        if (textMesh != null)
        {
            var curr_steps = step_counter < 0 ? "step on start" : step_counter.ToString();
            textMesh.text = $"Results: {results.Count}\n current steps: {curr_steps}";
        }
        foreach (SpatialAwarenessMeshObject meshObject in meshObserver.Meshes.Values)
        {
            plain_position = meshObject.Filter.mesh.bounds.center;
            plain_position.y -= 0.1f;
            Quaternion plain_rotation = Quaternion.LookRotation(meshObject.Filter.mesh.normals[0]);
        }
    }

    // private void PositionCubeObjectInFrontOfCamera()
    // {
    //     if (cubeObject != null)
    //     {
    //         Camera mainCamera = Camera.main;
    //         if (mainCamera != null)
    //         {
    //             // Set the cube position in front of the camera at a fixed distance
    //             float distanceFromCamera = 0.7f; // Set this to the desired distance from the camera
    //             Vector3 cameraForward = mainCamera.transform.forward;
    //             Vector3 cameraRight = mainCamera.transform.right;
    //             Vector3 newPosition = mainCamera.transform.position + cameraForward * distanceFromCamera + cameraRight * 0.15f;  //Set camera slightly on the right of the image
    //             cubeObject.transform.position = newPosition;
    //             // Make the cube face the camera
    //             cubeObject.transform.LookAt(mainCamera.transform);
    //         }
    //     }
    // }

    private void PositionObjectInFrontOfCamera(GameObject obj)
    {
        if (obj != null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float distanceFromCamera = 0.6f; // Set this to the desired distance from the camera
                Vector3 cameraForward = mainCamera.transform.forward;
                Vector3 cameraup = mainCamera.transform.up;
                Vector3 newPosition = mainCamera.transform.position + cameraForward * distanceFromCamera + cameraup * -0.08f ;  //Set camera slightly on the right of the image
                obj.transform.position = newPosition;
                obj.transform.LookAt(mainCamera.transform);
                obj.transform.Rotate(0, 180, 0);
            }
        }
    }

public IEnumerator DetectWebcam()
{
    List<PoseEstimationResult> tmpResults = new List<PoseEstimationResult>();
    while (true)
    {
        if (pictureTexture && !isPaused)
        {
            camera2WorldMatrix_local = camera2WorldMatrix;
            projectionMatrix_local = projectionMatrix;
            CropTexture(imgSizeWidth, imgSizeHeight);

            using (TensorFloat inputTensor = TextureConverter.ToTensor(inputTexture))
            {
                TensorFloat inputTensorResized = inputTensor;
                IEnumerator schedule = worker.ExecuteLayerByLayer(inputTensorResized);
                // Execute the model layer by layer over multiple frames
                int it = 0;
                while (schedule.MoveNext())
                {
                    if (++it % 30 == 0)
                    {
                        yield return null; // Yield control back to the main thread, allowing for smooth framerates
                    }
                }

                using (TensorFloat resultTensor = worker.PeekOutput(runtimeModel.outputs[0].name) as TensorFloat)
                {
                    using (TensorFloat downloadedTensorCopy = resultTensor.ReadbackAndClone())
                    {
                        tmpResults.Clear();
                        results.Clear();
                        confidenceThreshold = 0.3f;

                        ParseYoloPoseOutput(downloadedTensorCopy, confidenceThreshold, tmpResults);
                        results = NonMaxSuppression(0.6f, tmpResults);

                        // annotatedTexture = inputTexture;
                        // showKeypoints(results);

                        // var dets = "";
                        // foreach (var l in results)
                        // {
                        //     dets += $"{l}\n";
                        // }
                        // Debug.Log(dets);

                        if (results.Count >= 1)
                        {
                            GenerateNextStep(results[0], camera2WorldMatrix_local, projectionMatrix_local);
                        }
                    }
                }
            }
            yield return null;
        }
        else
        {
            yield return null;
        }
    }
}

private void ParseYoloPoseOutput(TensorFloat tensor, float confidenceThreshold, List<PoseEstimationResult> poseResults)
{
    for (int batch = 0; batch < tensor.shape[0]; batch++)
    {
        for (int i = 0; i < tensor.shape[2]; i++)
        {
            var label = 0;
            var confidence = tensor[batch, 4, i];
            if (confidence < confidenceThreshold)
            {
                continue;
            }

            var box = ExtractBoundingBox(tensor, i, batch);
            var kpts = ExtractKeypoints(tensor, i, batch);
            poseResults.Add(new PoseEstimationResult
            {
                Bbox = box,
                Confidence = confidence,
                LabelIdx = label,
                Keypoints = kpts
            });
        }
    }
}

    private BoundingBox ExtractBoundingBox(TensorFloat tensor, int row, int batch)
    {
        return new BoundingBox
        {
            X = tensor[batch, 0, row] + (batch * imgSizeWidth),
            Y = tensor[batch, 1, row],
            Width = tensor[batch, 2, row],
            Height = tensor[batch, 3, row]
        };

    }

    private List<Keypoint> ExtractKeypoints(TensorFloat tensor, int row, int batch)
    {
        var keypoints = new List<Keypoint>();   
        for(var i = 0; i < keypointsNumber; i++)
        {
            Keypoint kp = new Keypoint
            {
                X = tensor[batch, i * 3 + 5, row],
                Y = tensor[batch, i * 3 + 6, row],
                Confidence = tensor[batch, i * 3 + 7, row]
            };
            keypoints.Add(kp);
        }
        return keypoints;
    }

    private float IoU(Rect boundingBoxA, Rect boundingBoxB)
    {
        float intersectionArea = Mathf.Max(0, Mathf.Min(boundingBoxA.xMax, boundingBoxB.xMax) - Mathf.Max(boundingBoxA.xMin, boundingBoxB.xMin)) *
                        Mathf.Max(0, Mathf.Min(boundingBoxA.yMax, boundingBoxB.yMax) - Mathf.Max(boundingBoxA.yMin, boundingBoxB.yMin));

        float unionArea = boundingBoxA.width * boundingBoxA.height + boundingBoxB.width * boundingBoxB.height - intersectionArea;

        if (unionArea == 0)
        {
            return 0;
        }

        return intersectionArea / unionArea;
    }

    private List<PoseEstimationResult> NonMaxSuppression(float threshold, List<PoseEstimationResult> boxes)
    {
        var results = new List<PoseEstimationResult>();
        if (boxes.Count == 0)
        {
            return results;
        }
        var detections = boxes.OrderByDescending(b => b.Confidence).ToList();
        results.Add(detections[0]);

        for (int i = 1; i < detections.Count; i++)
        {
            bool add = true;
            for (int j = 0; j < results.Count; j++)
            {
                float iou = IoU(detections[i].Rect, results[j].Rect);
                if (iou > threshold)
                {
                    add = false;
                    break;
                }
            }
            if (add)
                results.Add(detections[i]);
        }

        return results;

    }

    // private void showKeypoints(List<PoseEstimationResult> results)
    // {
    //     Color blue = Color.blue;
    //     Color red = Color.red;
    //     Color yellow = Color.yellow;
    //     int radius = 2;
    //     foreach (var result in results)
    //     {
    //         // List<int> indices = new List<int> { 13, 14, 15, 16 };  //- original YOLOv8
    //         List<int> indices = new List<int> { 0, 1, 2, 3, 4, 5};
    //         int i = 0;
    //         foreach (int index in indices)
    //         {   
    //             Keypoint kp = result.Keypoints[index];
    //             int x = (int)kp.X;
    //             int y = 320 - (int)kp.Y;
    //             for (int dx = -radius; dx <= radius; dx++)
    //             {
    //                 for (int dy = -radius; dy <= radius; dy++)
    //                 {
    //                     int newX = x + dx;
    //                     int newY = y + dy;
    //                     if (newX >= 0 && newX < annotatedTexture.width && newY >= 0 && newY < annotatedTexture.height)
    //                     {
    //                         if ( i == 0 || i == 1 )
    //                         {
    //                             annotatedTexture.SetPixel(newX, newY, red);
    //                         } else if ( i == 2 || i == 3 ){
    //                             annotatedTexture.SetPixel(newX, newY, blue);
    //                         } else if ( i == 4 || i == 5 ){
    //                             annotatedTexture.SetPixel(newX, newY, yellow);
    //                         }
    //                     }
    //                 }
    //             }
    //             i += 1;
    //         }
    //         annotatedTexture.SetPixel(0, 0, blue);
    //         annotatedTexture.Apply(); // Apply the changes to the texture
    //     }
    // }

public Vector3 GenerateNextStep(PoseEstimationResult det, Matrix4x4 camera2WorldMatrix_local, Matrix4x4 projectionMatrix_local)
{
    var x_offset = (cameraResolutionWidth - inferenceImgSize) / 2;
    var y_offset = (cameraResolutionHeight - inferenceImgSize) / 2;

    Keypoint keypoint_left = det.Keypoints[2];
    Keypoint keypoint_left_fr = det.Keypoints[5];
    Keypoint keypoint_right = det.Keypoints[3];
    Keypoint keypoint_right_fr = det.Keypoints[4];
    
    //left leg keypoints
    Vector3 keypoint_left_World = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2(keypoint_left.X + x_offset, keypoint_left.Y + y_offset));
    Vector3 keypoint_left_fr_World = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2(keypoint_left_fr.X + x_offset, keypoint_left_fr.Y + y_offset));
    //right leg keypoints
    Vector3 keypoint_right_World = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2(keypoint_right.X + x_offset, keypoint_right.Y + y_offset));
    Vector3 keypoint_right_fr_World = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2(keypoint_right_fr.X + x_offset, keypoint_right_fr.Y + y_offset));

    // Vector3 direction_left = (keypoint_left_fr_World - keypoint_left_World).normalized;
    // Vector3 direction_right = (keypoint_right_fr_World - keypoint_right_World).normalized;    
    Vector3 left_leg_position = mainCamera.transform.position + keypoint_left_fr_World;
    Vector3 right_leg_position = mainCamera.transform.position + keypoint_right_fr_World;
    left_leg_position.y = plain_position.y - 0.05f;
    left_leg.transform.position = left_leg_position;
    
    right_leg_position.y = plain_position.y - 0.05f;
    right_leg.transform.position = right_leg_position;

    // Create a copy of the cylinderObjects list and reverse it
    List<GameObject> reversedCylinderObjects = new List<GameObject>(cylinderObjects);
    reversedCylinderObjects.Reverse();
    Vector3 newCylinderCenter = reversedCylinderObjects[0].transform.position;
    if (step_counter % 2 == 0)
    {
    // newCylinderCenter += keypoint_left_fr_World + (direction_left * 0.5f);
        newCylinderCenter += main_direction * step_length + start_direction * hip_width;
    }
    else 
    {
    // newCylinderCenter += keypoint_right_fr_World + (direction_right * 0.5f);
        newCylinderCenter += main_direction * step_length - start_direction * hip_width;  
    }

    if (first_step_reached)
    {   
        DisplayStep(newCylinderCenter, plain_position);
        first_step_reached = false;
    }
    return Vector3.zero;
}

    private void DisplayFirstStep(Vector3 center,  Vector3 mesh_position)
    {
        Vector3 cameraLeft = -mainCamera.transform.right;
        Vector3 firstPosition_left = center + cameraLeft * (hip_width/2);
        Vector3 firstPosition_right = center - cameraLeft * (hip_width/2);

        firstPosition_left.y = mesh_position.y - 0.10f;
        firstPosition_right.y = mesh_position.y - 0.10f;

        GameObject cylinder_left =  Instantiate(cylinderPrefab, firstPosition_left, Quaternion.identity);
        GameObject cylinder_right = Instantiate(cylinderPrefab, firstPosition_right, Quaternion.identity);
        cylinderObjects.Add(cylinder_left);
        cylinderObjects.Add(cylinder_right);
        start_direction = (firstPosition_right - firstPosition_left).normalized;
        main_direction = Vector3.Cross(start_direction, Vector3.up).normalized;

        Debug.Log($"FIRST CYLINDER ADDED");
    }
    private void DisplayStep(Vector3 center,  Vector3 mesh_position)
    {
        Vector3 cylinder_center = center;        
        cylinder_center.y = mesh_position.y - 0.10f;
        GameObject cylinder =  Instantiate(cylinderPrefab, cylinder_center, Quaternion.identity);
        cylinderObjects.Add(cylinder);
        Debug.Log($"NEW CYLINDER ADDED");

    }

    public void GetTriggeringObjectName(string name)
    {
        triggeringObject = name;
    }

    public void SetIsCollision(bool value)
    {
        is_collision = value;
        if (!first_step_reached)
        {
            first_step_reached = true;
        }
    }

    private void CheckStepsCompleted()
    {
        if (step_counter >= steps_to_finish)
        {
            PositionObjectInFrontOfCamera(finish);
            finish.SetActive(true);
            isPaused = true;
            ResetScene();
        }
    }

    public void DeleteOldestStep()
    {
        if (cylinderObjects.Count >= 2)
        {
            GameObject oldestCircle = cylinderObjects[0];
            cylinderObjects.RemoveAt(0);
            Destroy(oldestCircle);
            step_counter += 1;
            Debug.Log($"stepcounter {step_counter}");
            CheckStepsCompleted();
        }
    }

    public Vector3 shootLaserFrom(Vector3 from, Vector3 direction, float length, Material mat = null)
    {
        Ray ray = new Ray(from, direction);
        Vector3 to = from + length * direction;

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, length))
            to = hit.point;

        return to;
    }

    public RaycastHit shootLaserRaycastHit(Vector3 from, Vector3 direction, float length, Material mat = null)
    {
        Ray ray = new Ray(from, direction);
        Vector3 to = from + length * direction;

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, length))
            to = hit.point;

        return hit;
    }

    public Vector3 GenerateBoundingBox(PoseEstimationResult det, Matrix4x4 camera2WorldMatrix_local, Matrix4x4 projectionMatrix_local)
    {
        var x_offset = (cameraResolutionWidth - inferenceImgSize) / 2;
        var y_offset = (cameraResolutionHeight - inferenceImgSize) / 2;

        Vector3 direction = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2(det.Bbox.X + x_offset, det.Bbox.Y + y_offset));
        var centerHit = shootLaserRaycastHit(camera2WorldMatrix_local.GetColumn(3), direction, 10f);

        var distance = centerHit.distance;
        distance -= 0.05f;

        Vector3 corner_0 = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2((det.Bbox.X + x_offset) - (det.Bbox.Width / 2), det.Bbox.Y + y_offset - (det.Bbox.Height / 2)));
        Vector3 corner_1 = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2((det.Bbox.X + x_offset) - (det.Bbox.Width / 2), det.Bbox.Y + y_offset + (det.Bbox.Height / 2)));
        Vector3 corner_2 = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2((det.Bbox.X + x_offset) + (det.Bbox.Width / 2), det.Bbox.Y + y_offset - (det.Bbox.Height / 2)));
        Vector3 corner_3 = LocatableCameraUtils.PixelCoordToWorldCoord(camera2WorldMatrix_local, projectionMatrix_local, resolution, new Vector2((det.Bbox.X + x_offset) + (det.Bbox.Width / 2), det.Bbox.Y + y_offset + (det.Bbox.Height / 2)));

        var point_0 = shootLaserFrom(camera2WorldMatrix_local.GetColumn(3), corner_0, distance);
        var point_1 = shootLaserFrom(camera2WorldMatrix_local.GetColumn(3), corner_1, distance);
        var point_2 = shootLaserFrom(camera2WorldMatrix_local.GetColumn(3), corner_2, distance);
        var point_3 = shootLaserFrom(camera2WorldMatrix_local.GetColumn(3), corner_3, distance);

        var go = new GameObject();

        go.transform.position = point_0;
        go.transform.rotation = Camera.main.transform.rotation;

        var renderer = go.GetComponent<Renderer>();

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.widthMultiplier = 0.01f;
        lr.loop = true;
        lr.positionCount = 4;
        lr.material = laserMaterial;
        lr.material.color = Color.red;

        lr.SetPosition(0, point_0);
        lr.SetPosition(3, point_1);
        lr.SetPosition(1, point_2);
        lr.SetPosition(2, point_3);

        // Debug.Log($"Długość generowanej linii: {distance}");
        
        labels.Add(Tuple.Create(go, renderer));

        return centerHit.point;
    }

private void CropTexture(int cropWidth, int cropHeight)
{
    int centerX = pictureTexture.width / 2 - cropWidth / 2;
    int centerY = pictureTexture.height / 2 - cropHeight / 2;
    Color[] pixels = pictureTexture.GetPixels(centerX, centerY, cropWidth, cropHeight);
    int totalPixels = cropWidth * cropHeight;
    Color[] flippedPixels = new Color[totalPixels];
    // Flip the pixels horizontally
    for (int y = 0; y < cropHeight; y++)
    {
        int sourceIndex = y * cropWidth;
        int destIndex = (cropHeight - 1 - y) * cropWidth;
        Array.Copy(pixels, sourceIndex, flippedPixels, destIndex, cropWidth);
    }

    inputTexture.SetPixels(flippedPixels);
    inputTexture.Apply();
}


}