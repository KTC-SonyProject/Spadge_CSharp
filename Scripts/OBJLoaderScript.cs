using UnityEngine;
using System.IO;
using Dummiesman; // OBJLoaderのためのインポート

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
                    Debug.LogError("OBJLoaderScriptのインスタンスがシーンにありません！");
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
            Debug.LogError($"Modelsフォルダーが存在しません: {modelsFolderPath}");
            objFilePaths = new string[0];
            return;
        }

        objFilePaths = Directory.GetFiles(modelsFolderPath, "*.obj");

        if (objFilePaths.Length == 0)
        {
            Debug.LogError("Modelsフォルダー内にOBJファイルが見つかりませんでした");
        }
        else
        {
            Debug.Log("OBJファイルのリストを読み込みました:");
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
            Debug.LogError("OBJファイルが読み込まれませんでした。");
        }
    }
    public void LoadOBJ(string objFilePath)
    {
        Debug.Log($"Loading OBJ from: {objFilePath}");
        UnityMainThreadDispatcher.Enqueue(() =>
        { 
            // 前回ロードしたオブジェクトを削除
            if (currentLoadedObj != null)
            {
                DestroyImmediate(currentLoadedObj);
                currentLoadedObj = null;
                Debug.Log("前回のオブジェクトを削除しました");
            }

            // OBJファイルをロード
            OBJLoader loader = new OBJLoader();

            // MTLファイルのパスを取得
            string mtlFilePath = Path.ChangeExtension(objFilePath, ".mtl");
            Debug.Log($"対応するMTLファイルのパス: {mtlFilePath}");

            currentLoadedObj = new OBJLoader().Load(objFilePath, mtlFilePath);
            if (currentLoadedObj != null)
            {
                // 初期位置を設定
                currentLoadedObj.transform.position = initialPosition;
                AdjustScaleToBoundingBox(currentLoadedObj);
                Debug.Log("OBJファイルをロードしました");
            }
            else
            {
                Debug.LogError("OBJファイルのロードに失敗しました");
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
            Debug.LogError("OBJファイルリストが空です。");
            return;
        }

        currentObjIndex = (currentObjIndex - 1 + objFilePaths.Length) % objFilePaths.Length;
        Debug.Log($"前のOBJをロード中: {objFilePaths[currentObjIndex]}");
        LoadOBJ(objFilePaths[currentObjIndex]);
    }

    // 名前で指定したOBJファイルをロード
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
            Debug.LogError($"指定した名前のOBJファイルが見つかりません: {objFileName}");
        }
    }

    // バウンディングボックスに合わせてスケールを調整
    // バウンディングボックスに合わせてスケールを調整
    // バウンディングボックスに合わせてスケールを調整
    private void AdjustScaleToBoundingBox(GameObject obj)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        // 子オブジェクトのRendererからBoundsを取得
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

        // Rendererが見つからない場合の対応
        if (!hasBounds)
        {
            Debug.LogWarning("Rendererが見つかりません。デフォルトスケールを使用します。");
            obj.transform.localScale = Vector3.one;
            return;
        }

        // オブジェクトのサイズを取得
        Vector3 objectSize = bounds.size;

        // バウンディングボックスとのスケール倍率を計算
        float scaleFactor = Mathf.Max(
            boundingBoxSize.x / objectSize.x,
            boundingBoxSize.y / objectSize.y,
            boundingBoxSize.z / objectSize.z
        );

        // スケール倍率を適用
        obj.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

        // デバッグ情報を追加
        if (scaleFactor > 1)
        {
            Debug.Log($"オブジェクトを拡大しました: スケール倍率 = {scaleFactor}");
        }
        else if (scaleFactor < 1)
        {
            Debug.Log($"オブジェクトを縮小しました: スケール倍率 = {scaleFactor}");
        }
        else
        {
            Debug.Log("オブジェクトのスケールは変更されませんでした");
        }

        Debug.Log($"オブジェクトのスケールを調整しました: 新しいスケール = {obj.transform.localScale}");
    }

}