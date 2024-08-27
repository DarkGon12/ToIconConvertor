using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

public class ScreenshotEditorWindow : EditorWindow
{
    private Texture2D _originalImage;
    private Texture2D _finalImage;

    private int _selectedResolutionIndex = 0;
    private int _customWidth = 512;
    private int _customHeight = 512;

    private float _customDistance = 10f;

    private List<GameObject> _targetObjects = new List<GameObject>();
    private List<int> _originalLayers = new List<int>();

    private readonly string[] _resolutions = { "512x512", "1024x1024", "2048x2048", "Custom" };

    private bool _centerObject = true; // ���� ��� ������������� �������

    [MenuItem("Window/To Icon Convertor Window")]
    [MenuItem("Tools/To Icon Convertor")]
    public static void OpenWindow()
    {
        ScreenshotEditorWindow window = GetWindow<ScreenshotEditorWindow>("TIC window");
        window.minSize = new Vector2(600, 750);
        window.maxSize = new Vector2(650, 775);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(350));
        GUILayout.Label("� ������", EditorStyles.boldLabel);

        GUILayout.Label("�������� ����������", EditorStyles.boldLabel);
        _selectedResolutionIndex = EditorGUILayout.Popup(_selectedResolutionIndex, _resolutions);

        if (_selectedResolutionIndex == _resolutions.Length - 1)
        {
            _customWidth = EditorGUILayout.IntField("������", _customWidth);
            _customHeight = EditorGUILayout.IntField("������", _customHeight);
        }

        GUILayout.Space(5);

        _centerObject = EditorGUILayout.Toggle("������������ ������", _centerObject);

        if (_centerObject)
        {
            _customDistance = EditorGUILayout.FloatField("��������� �� �������", _customDistance);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("��������� ����������� � ���������"))
            CaptureScreenshotFromSceneView();

        if (GUILayout.Button("��������� ����������� � ������� ������") && _targetObjects.Count > 0)
            CaptureScreenshot();

        GUILayout.Space(5);

        GUILayout.Label("������������ �����������", EditorStyles.boldLabel);
        if (_originalImage != null)
            GUILayout.Label(_originalImage, GUILayout.Width(256), GUILayout.Height(256));
        else
            GUILayout.Label("��� �������.");

        GUILayout.Space(5);

        GUILayout.Label("��������� �����������", EditorStyles.boldLabel);
        if (_finalImage != null)
            GUILayout.Label(_finalImage, GUILayout.Width(256), GUILayout.Height(256));
        else
            GUILayout.Label("��� �������.");

        GUILayout.Space(5);

        if (_finalImage != null && GUILayout.Button("��������� � PNG"))
            SaveFinalImageToPNG();

        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("������� �������", EditorStyles.boldLabel);

        for (int i = 0; i < _targetObjects.Count; i++)
        {
            _targetObjects[i] = (GameObject)EditorGUILayout.ObjectField("������ " + (i + 1), _targetObjects[i], typeof(GameObject), true);

            if (GUILayout.Button("�������", GUILayout.Width(100)))
            {
                _targetObjects.RemoveAt(i);
                _originalLayers.RemoveAt(i);
            }
        }

        GUILayout.Space(6);

        if (GUILayout.Button("�������� ������"))
        {
            _targetObjects.Add(null);
            _originalLayers.Add(0);
        }

        GUILayout.Space(5);

        GUILayout.Label("���������� �� ���������� ������:", EditorStyles.boldLabel);
        GUILayout.Label("�������� �� �������� �������", EditorStyles.label);
        GUILayout.Label("����������� ��� �� ������� ���,", EditorStyles.label);
        GUILayout.Label("���������� ������ ������", EditorStyles.label);
        GUILayout.Label("'�������� ������'", EditorStyles.boldLabel);
        GUILayout.Label("� ��������� � �������� ��������", EditorStyles.label);
        GUILayout.Label("������ ������ ����� ����������", EditorStyles.label);
        GUILayout.Label("�������� ����� ������ ������", EditorStyles.label);
        GUILayout.Label("��������� ����������� � ������� ������", EditorStyles.boldLabel);
        GUILayout.Label("��� ������� ������ � ������", EditorStyles.label);
        GUILayout.Label("�� ��� �� ������", EditorStyles.label);
        GUILayout.Label("'��������� ����������� � ���������", EditorStyles.boldLabel);
        GUILayout.Label("��� ������� ��������� ������ � �����", EditorStyles.label);
        GUILayout.Label("��� ��������� ����� � ���������", EditorStyles.label);

        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void CaptureScreenshotFromSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogError("SceneView �� ������");
            Debug.Log("<color=red>������������� �� ���� ����� � ��������� �������</color>");
            return;
        }

        Camera tempCamera = new GameObject("TempCamera").AddComponent<Camera>();
        tempCamera.CopyFrom(sceneView.camera);

        if (_centerObject && _targetObjects.Count > 0)
            CenterObjectInView(tempCamera, _targetObjects[0]);

        CaptureScreenshot(tempCamera);

        DestroyImmediate(tempCamera.gameObject);
    }

    private void CaptureScreenshot()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("������ �� �������!");
            return;
        }

        if (_centerObject && _targetObjects.Count > 0)
            CenterObjectInView(mainCamera, _targetObjects[0]);

        CaptureScreenshot(mainCamera);
    }

    private void CaptureScreenshot(Camera camera)
    {
        if (_targetObjects.Count == 0)
        {
            Debug.LogError("������� ������� �� �������!");
            return;
        }

        int width, height;

        if (_selectedResolutionIndex == _resolutions.Length - 1)
        {
            width = _customWidth;
            height = _customHeight;
        }
        else
        {
            string[] resolutionParts = _resolutions[_selectedResolutionIndex].Split('x');
            width = int.Parse(resolutionParts[0]);
            height = int.Parse(resolutionParts[1]);
        }

        for (int i = 0; i < _targetObjects.Count; i++)
        {
            if (_targetObjects[i] != null)
            {
                _originalLayers[i] = _targetObjects[i].layer;

                int maskedLayer = LayerMask.NameToLayer("MaskedObject");
                if (maskedLayer == -1)
                {
                    Debug.LogError("���� 'MaskedObject' �� ������. �������� ���� 'MaskedObject'.");
                    return;
                }

                _targetObjects[i].layer = maskedLayer;
            }
        }

        RenderTexture rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        _originalImage = new Texture2D(width, height, TextureFormat.RGBA32, false);
        camera.Render();
        RenderTexture.active = rt;
        _originalImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _originalImage.Apply();
        camera.targetTexture = null;
        RenderTexture.active = null;

        Camera maskCamera = new GameObject("MaskCamera").AddComponent<Camera>();
        maskCamera.CopyFrom(camera);

        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = new Color(0, 0, 0, 0);

        maskCamera.cullingMask = 1 << LayerMask.NameToLayer("MaskedObject");

        RenderTexture maskRT = new RenderTexture(width, height, 24);
        maskCamera.targetTexture = maskRT;
        Texture2D maskImage = new Texture2D(width, height, TextureFormat.RGBA32, false);
        maskCamera.Render();
        RenderTexture.active = maskRT;
        maskImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        maskImage.Apply();
        maskCamera.targetTexture = null;
        RenderTexture.active = null;

        DestroyImmediate(maskCamera.gameObject);

        _finalImage = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] maskPixels = maskImage.GetPixels();
        Color[] originalPixels = _originalImage.GetPixels();

        for (int i = 0; i < maskPixels.Length; i++)
        {
            if (maskPixels[i].a > 0)
                _finalImage.SetPixel(i % width, i / width, originalPixels[i]);
            else
                _finalImage.SetPixel(i % width, i / width, new Color(0, 0, 0, 0));
        }

        _finalImage.Apply();

        for (int i = 0; i < _targetObjects.Count; i++)
        {
            if (_targetObjects[i] != null)
                _targetObjects[i].layer = _originalLayers[i];
        }
    }

    private void SaveFinalImageToPNG()
    {
        if (_finalImage == null)
        {
            Debug.LogError("������ �� ������� � ������.");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Save PNG", "", "final_image.png", "png");

        if (!string.IsNullOrEmpty(path))
        {
            byte[] pngData = _finalImage.EncodeToPNG();
            if (pngData != null)
            {
                File.WriteAllBytes(path, pngData);
                Debug.Log("<color=green>������ ��������� � </color>" + path);
            }
        }
    }

    private void CenterObjectInView(Camera camera, GameObject targetObject)
    {
        Bounds bounds = targetObject.GetComponent<Renderer>().bounds;
        Vector3 objectCenter = bounds.center;
        camera.transform.position = objectCenter - camera.transform.forward * _customDistance;
        camera.transform.LookAt(objectCenter);
    }
}
