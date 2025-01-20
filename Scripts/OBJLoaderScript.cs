using UnityEngine;
using System.IO; // �t�@�C������p
using Dummiesman;  // OBJLoader�̂��߂̃C���|�[�g

public class OBJLoaderScript : MonoBehaviour
{
    public string fileListPath = "Assets/Models/obj_file_list.txt"; // �e�L�X�g�t�@�C���̃p�X
    public Vector3 boundingBoxSize = new Vector3(5, 5, 5); // �w�肷��͈͂̃T�C�Y

    private string[] objFilePaths; // �e�L�X�g����ǂݍ���OBJ�t�@�C���̃p�X���X�g
    private GameObject currentLoadedObj; // ���݃��[�h���̃I�u�W�F�N�g
    private int currentObjIndex = 0; // ���ݕ\������OBJ�̃C���f�b�N�X
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
        // OBJ�t�@�C�����X�g�̓ǂݍ���
        LoadFilePaths();

        // �ŏ���OBJ�����[�h�i���݂���ꍇ�̂݁j
        if (objFilePaths.Length > 0)
        {
            LoadOBJ(objFilePaths[currentObjIndex]);
        }
        else
        {
            Debug.LogError("OBJ�t�@�C�����ǂݍ��܂�܂���ł����B");
        }
    }

    /// <summary>
    /// �t�@�C�����X�g��ǂݍ���
    /// </summary>
    private void LoadFilePaths()
    {
        // Models�t�H���_�[�̃p�X���쐬
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");

        // �t�H���_�[�̑��݂��m�F
        if (!Directory.Exists(modelsFolderPath))
        {
            Debug.LogError($"Models�t�H���_�[�����݂��܂���: {modelsFolderPath}");
            objFilePaths = new string[0]; // ��̔z���ݒ�
            return;
        }

        // Models�t�H���_�[����OBJ�t�@�C��������
        objFilePaths = Directory.GetFiles(modelsFolderPath, "*.obj");

        // �t�@�C����������Ȃ��ꍇ�̃G���[�`�F�b�N
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

    /// <summary>
    /// �w�肵���p�X��OBJ�t�@�C�������[�h
    /// </summary>
    /// <param name="objFilePath">OBJ�t�@�C���̃p�X</param>

    public void LoadOBJ(string objFilePath)
    {
        Debug.Log($"Loading OBJ from: {objFilePath}");
        UnityMainThreadDispatcher.Enqueue(() =>
        {
                // �O�񃍁[�h�����I�u�W�F�N�g���폜
                if (currentLoadedObj != null)
            {
                DestroyImmediate(currentLoadedObj); // �Â��I�u�W�F�N�g���폜
                currentLoadedObj = null; // �O�̂��ߖ����I��null�ɂ���
            }

            // OBJLoader�𒼐ڃC���X�^���X�����ă��[�h
            OBJLoader loader = new OBJLoader(); // �C���X�^���X��
            currentLoadedObj = loader.Load(objFilePath); // �I�u�W�F�N�g�����[�h

            // �ǂݍ��񂾃I�u�W�F�N�g���V�[���ɔz�u
            if (currentLoadedObj != null)
            {
                currentLoadedObj.transform.position = Vector3.zero; // �����ʒu��ݒ�
                AdjustScaleToBoundingBox(currentLoadedObj); // �T�C�Y����
                Debug.Log("OBJ�t�@�C�������[�h���܂���");
            }
            else
            {
                Debug.LogError("OBJ�t�@�C���̃��[�h�Ɏ��s���܂���");
            }
        });
    }

    /// <summary>
    /// ����OBJ�t�@�C�������[�h
    /// </summary>
    public void LoadNextOBJ()
    {
        if (objFilePaths.Length == 0) return;

        // ���̃C���f�b�N�X���v�Z
        currentObjIndex = (currentObjIndex + 1) % objFilePaths.Length;

        Debug.Log($"Loading next OBJ: {objFilePaths[currentObjIndex]}");
        LoadOBJ(objFilePaths[currentObjIndex]);
    }

    /// <summary>
    /// �O��OBJ�t�@�C�������[�h
    /// </summary>
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

    /// <summary>
    /// ���O�Ŏw�肵��OBJ�t�@�C�������[�h
    /// </summary>
    public void LoadOBJByName(string objFileName)
    {
        if (objFilePaths.Length == 0) return;
        // �t�@�C�����ŃC���f�b�N�X������
        int index = System.Array.FindIndex(objFilePaths, x => x.Contains(objFileName));
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

    /// <summary>
    /// �I�u�W�F�N�g�̃T�C�Y�𒲐����Ďw�肵���͈͓��Ɏ��߂�
    /// </summary>
    /// <param name="obj">���[�h���ꂽ�I�u�W�F�N�g</param>
    private void AdjustScaleToBoundingBox(GameObject obj)
    {
        Renderer renderer = obj.GetComponentInChildren<Renderer>(); // �q�v�f���܂߂�Renderer���擾

        if (renderer == null)
        {
            Debug.LogError("Renderer��������܂���B�I�u�W�F�N�g��Renderer�R���|�[�l���g��ǉ����Ă��������B");
            return;
        }

        // �I�u�W�F�N�g�̃T�C�Y�iBounds�j
        Vector3 objectSize = renderer.bounds.size;

        // �e���̃X�P�[������v�Z
        float scaleX = boundingBoxSize.x / objectSize.x;
        float scaleY = boundingBoxSize.y / objectSize.y;
        float scaleZ = boundingBoxSize.z / objectSize.z;

        // �䗦���ŏ��X�P�[���ɍ��킹�Ē���
        float scale = Mathf.Min(scaleX, scaleY, scaleZ);

        // �X�P�[���K�p
        obj.transform.localScale = obj.transform.localScale * scale;

        Debug.Log("�I�u�W�F�N�g�̃X�P�[���𒲐����܂����B");
    }
}
