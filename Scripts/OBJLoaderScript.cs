using UnityEngine;
using System.IO; // ファイル操作用
using Dummiesman;  // OBJLoaderのためのインポート

public class OBJLoaderScript : MonoBehaviour
{
    public string fileListPath = "Assets/Models/obj_file_list.txt"; // テキストファイルのパス
    public Vector3 boundingBoxSize = new Vector3(5, 5, 5); // 指定する範囲のサイズ

    private string[] objFilePaths; // テキストから読み込んだOBJファイルのパスリスト
    private GameObject currentLoadedObj; // 現在ロード中のオブジェクト
    private int currentObjIndex = 0; // 現在表示中のOBJのインデックス
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
        // OBJファイルリストの読み込み
        LoadFilePaths();

        // 最初のOBJをロード（存在する場合のみ）
        if (objFilePaths.Length > 0)
        {
            LoadOBJ(objFilePaths[currentObjIndex]);
        }
        else
        {
            Debug.LogError("OBJファイルが読み込まれませんでした。");
        }
    }

    /// <summary>
    /// ファイルリストを読み込む
    /// </summary>
    private void LoadFilePaths()
    {
        // Modelsフォルダーのパスを作成
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");

        // フォルダーの存在を確認
        if (!Directory.Exists(modelsFolderPath))
        {
            Debug.LogError($"Modelsフォルダーが存在しません: {modelsFolderPath}");
            objFilePaths = new string[0]; // 空の配列を設定
            return;
        }

        // Modelsフォルダー内のOBJファイルを検索
        objFilePaths = Directory.GetFiles(modelsFolderPath, "*.obj");

        // ファイルが見つからない場合のエラーチェック
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

    /// <summary>
    /// 指定したパスのOBJファイルをロード
    /// </summary>
    /// <param name="objFilePath">OBJファイルのパス</param>

    public void LoadOBJ(string objFilePath)
    {
        Debug.Log($"Loading OBJ from: {objFilePath}");
        UnityMainThreadDispatcher.Enqueue(() =>
        {
                // 前回ロードしたオブジェクトを削除
                if (currentLoadedObj != null)
            {
                DestroyImmediate(currentLoadedObj); // 古いオブジェクトを削除
                currentLoadedObj = null; // 念のため明示的にnullにする
            }

            // OBJLoaderを直接インスタンス化してロード
            OBJLoader loader = new OBJLoader(); // インスタンス化
            currentLoadedObj = loader.Load(objFilePath); // オブジェクトをロード

            // 読み込んだオブジェクトをシーンに配置
            if (currentLoadedObj != null)
            {
                currentLoadedObj.transform.position = Vector3.zero; // 初期位置を設定
                AdjustScaleToBoundingBox(currentLoadedObj); // サイズ調整
                Debug.Log("OBJファイルをロードしました");
            }
            else
            {
                Debug.LogError("OBJファイルのロードに失敗しました");
            }
        });
    }

    /// <summary>
    /// 次のOBJファイルをロード
    /// </summary>
    public void LoadNextOBJ()
    {
        if (objFilePaths.Length == 0) return;

        // 次のインデックスを計算
        currentObjIndex = (currentObjIndex + 1) % objFilePaths.Length;

        Debug.Log($"Loading next OBJ: {objFilePaths[currentObjIndex]}");
        LoadOBJ(objFilePaths[currentObjIndex]);
    }

    /// <summary>
    /// 前のOBJファイルをロード
    /// </summary>
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

    /// <summary>
    /// 名前で指定したOBJファイルをロード
    /// </summary>
    public void LoadOBJByName(string objFileName)
    {
        if (objFilePaths.Length == 0) return;
        // ファイル名でインデックスを検索
        int index = System.Array.FindIndex(objFilePaths, x => x.Contains(objFileName));
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

    /// <summary>
    /// オブジェクトのサイズを調整して指定した範囲内に収める
    /// </summary>
    /// <param name="obj">ロードされたオブジェクト</param>
    private void AdjustScaleToBoundingBox(GameObject obj)
    {
        Renderer renderer = obj.GetComponentInChildren<Renderer>(); // 子要素も含めてRendererを取得

        if (renderer == null)
        {
            Debug.LogError("Rendererが見つかりません。オブジェクトにRendererコンポーネントを追加してください。");
            return;
        }

        // オブジェクトのサイズ（Bounds）
        Vector3 objectSize = renderer.bounds.size;

        // 各軸のスケール比を計算
        float scaleX = boundingBoxSize.x / objectSize.x;
        float scaleY = boundingBoxSize.y / objectSize.y;
        float scaleZ = boundingBoxSize.z / objectSize.z;

        // 比率を最小スケールに合わせて調整
        float scale = Mathf.Min(scaleX, scaleY, scaleZ);

        // スケール適用
        obj.transform.localScale = obj.transform.localScale * scale;

        Debug.Log("オブジェクトのスケールを調整しました。");
    }
}
