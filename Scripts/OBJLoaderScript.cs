using UnityEngine;
using System.IO;
using Dummiesman; // OBJLoader�̂��߂̃C���|�[�g

public class OBJLoaderScript : MonoBehaviour
{
    public Vector3 boundingBoxSize = new Vector3(5, 5, 5);
    public Vector3 initialPosition = new Vector3(0, 0, 1);
    private string[] objFilePaths;
    private GameObject currentLoadedObj;
    private int currentObjIndex = 0;
    private static OBJLoaderScript instance;
    private string persistentDataPath;

    public static OBJLoaderScript Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<OBJLoaderScript>();
                if (instance == null)
                {
                    Debug.LogError("OBJLoaderScript�̃C���X�^���X���V�[���ɂ���܂���I");
                }
            }
            return instance;
        }
    }

    void Awake()
    {
        persistentDataPath = Application.persistentDataPath;
    }

    void Start()
    {
        LoadFilePaths();
        LoadFirstOBJ();
    }

    public void LoadFilePaths()
    {
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");

        if (!Directory.Exists(modelsFolderPath))
        {
            Debug.LogError($"Models�t�H���_�[�����݂��܂���: {modelsFolderPath}");
            objFilePaths = new string[0];
            return;
        }

        objFilePaths = Directory.GetFiles(modelsFolderPath, "*.obj");

        if (objFilePaths.Length == 0)
        {
            Debug.LogError("Models�t�H���_�[����OBJ�t�@�C����������܂���ł���");
        }
        else
        {
            Debug.Log("OBJ�t�@�C���̃��X�g��ǂݍ��݂܂���:");
            foreach (var path in objFilePaths)
            {
                Debug.Log(path);
            }
        }
    }

    private void LoadFirstOBJ()
    {
        if (objFilePaths.Length > 0)
        {
            LoadOBJ(objFilePaths[currentObjIndex]);
        }
        else
        {
            Debug.LogError("OBJ�t�@�C�����ǂݍ��܂�܂���ł����B");
        }
    }
    public void LoadOBJ(string objFilePath)
    {
        Debug.Log($"Loading OBJ from: {objFilePath}");
        UnityMainThreadDispatcher.Enqueue(() =>
        { 
            // �O�񃍁[�h�����I�u�W�F�N�g���폜
            if (currentLoadedObj != null)
            {
                DestroyImmediate(currentLoadedObj);
                currentLoadedObj = null;
                Debug.Log("�O��̃I�u�W�F�N�g���폜���܂���");
            }

            // OBJ�t�@�C�������[�h
            OBJLoader loader = new OBJLoader();

            // MTL�t�@�C���̃p�X���擾
            string mtlFilePath = Path.ChangeExtension(objFilePath, ".mtl");
            Debug.Log($"�Ή�����MTL�t�@�C���̃p�X: {mtlFilePath}");

            currentLoadedObj = new OBJLoader().Load(objFilePath, mtlFilePath);
            if (currentLoadedObj != null)
            {
                // �����ʒu��ݒ�
                currentLoadedObj.transform.position = initialPosition;
                AdjustScaleToBoundingBox(currentLoadedObj);
                Debug.Log("OBJ�t�@�C�������[�h���܂���");
            }
            else
            {
                Debug.LogError("OBJ�t�@�C���̃��[�h�Ɏ��s���܂���");
            }
        });
    }

    public void LoadNextOBJ()
    {
        if (objFilePaths.Length == 0) return;

        currentObjIndex = (currentObjIndex + 1) % objFilePaths.Length;
        Debug.Log($"Loading next OBJ: {objFilePaths[currentObjIndex]}");
        LoadOBJ(objFilePaths[currentObjIndex]);
    }

    public void LoadPreviousOBJ()
    {
        if (objFilePaths.Length == 0)
        {
            Debug.LogError("OBJ�t�@�C�����X�g����ł��B");
            return;
        }

        currentObjIndex = (currentObjIndex - 1 + objFilePaths.Length) % objFilePaths.Length;
        Debug.Log($"�O��OBJ�����[�h��: {objFilePaths[currentObjIndex]}");
        LoadOBJ(objFilePaths[currentObjIndex]);
    }

    // ���O�Ŏw�肵��OBJ�t�@�C�������[�h
    public void LoadOBJByName(string objFileName)
    {
        if (objFilePaths.Length == 0) return;

        int index = System.Array.FindIndex(objFilePaths, x => Path.GetFileName(x) == objFileName);
        if (index >= 0)
        {
            currentObjIndex = index;
            Debug.Log($"Loading OBJ by name: {objFilePaths[currentObjIndex]}");
            LoadOBJ(objFilePaths[currentObjIndex]);
        }
        else
        {
            Debug.LogError($"�w�肵�����O��OBJ�t�@�C����������܂���: {objFileName}");
        }
    }

    // �o�E���f�B���O�{�b�N�X�ɍ��킹�ăX�P�[���𒲐�
    // �o�E���f�B���O�{�b�N�X�ɍ��킹�ăX�P�[���𒲐�
    // �o�E���f�B���O�{�b�N�X�ɍ��킹�ăX�P�[���𒲐�
    private void AdjustScaleToBoundingBox(GameObject obj)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        // �q�I�u�W�F�N�g��Renderer����Bounds���擾
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (hasBounds)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        // Renderer��������Ȃ��ꍇ�̑Ή�
        if (!hasBounds)
        {
            Debug.LogWarning("Renderer��������܂���B�f�t�H���g�X�P�[�����g�p���܂��B");
            obj.transform.localScale = Vector3.one;
            return;
        }

        // �I�u�W�F�N�g�̃T�C�Y���擾
        Vector3 objectSize = bounds.size;

        // �o�E���f�B���O�{�b�N�X�Ƃ̃X�P�[���{�����v�Z
        float scaleFactor = Mathf.Max(
            boundingBoxSize.x / objectSize.x,
            boundingBoxSize.y / objectSize.y,
            boundingBoxSize.z / objectSize.z
        );

        // �X�P�[���{����K�p
        obj.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

        // �f�o�b�O����ǉ�
        if (scaleFactor > 1)
        {
            Debug.Log($"�I�u�W�F�N�g���g�債�܂���: �X�P�[���{�� = {scaleFactor}");
        }
        else if (scaleFactor < 1)
        {
            Debug.Log($"�I�u�W�F�N�g���k�����܂���: �X�P�[���{�� = {scaleFactor}");
        }
        else
        {
            Debug.Log("�I�u�W�F�N�g�̃X�P�[���͕ύX����܂���ł���");
        }

        Debug.Log($"�I�u�W�F�N�g�̃X�P�[���𒲐����܂���: �V�����X�P�[�� = {obj.transform.localScale}");
    }

}