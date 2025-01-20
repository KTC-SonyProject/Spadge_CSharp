using UnityEngine;
using System.IO;
using Dummiesman; // OBJLoaderのためのインポート

public class OBJLoaderScript : MonoBehaviour
{
    public Vector3 boundingBoxSize = new Vector3(5, 5, 5);
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


        // 前回ロードしたオブジェクトを削除
        if (currentLoadedObj != null)
        {
            DestroyImmediate(currentLoadedObj);
            currentLoadedObj = null;
            Debug.Log("前回のオブジェクトを削除しました");
        }

        // OBJファイルをロード
        OBJLoader loader = new OBJLoader(); 
        Debug.Log(0);
        // currentLoadedObj = loader.Load(objFilePath);  // ここでエラーが発生
        currentLoadedObj = new OBJLoader().Load(objFilePath);   //試してみたが特に変わらず
        Debug.Log(1);
        if (currentLoadedObj != null)
        {
            MaterialAndShaderHandler.ApplyMaterialAndShader(currentLoadedObj, objFilePath);
            Debug.Log(2);
            currentLoadedObj.transform.position = Vector3.zero;
            Debug.Log(3);
            AdjustScaleToBoundingBox(currentLoadedObj);
            Debug.Log("OBJファイルをロードしました");
        }
        else
        {
            Debug.LogError("OBJファイルのロードに失敗しました");
        }
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
    private void AdjustScaleToBoundingBox(GameObject obj)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

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

        if (!hasBounds)
        {
            Debug.LogWarning("Rendererが見つかりません。デフォルトスケールを使用します。");
            obj.transform.localScale = Vector3.one;
            return;
        }

        Vector3 objectSize = bounds.size;
        float scaleFactor = Mathf.Min(
            boundingBoxSize.x / objectSize.x,
            boundingBoxSize.y / objectSize.y,
            boundingBoxSize.z / objectSize.z
        );

        obj.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
        Debug.Log("オブジェクトのスケールを調整しました。");
    }
}


// ここからシェーダーについて
public static class MaterialAndShaderHandler
{
    public static void ApplyMaterialAndShader(GameObject obj, string objFilePath)
    {
        Debug.Log("Material and Shader適用プロセスを開始...");
        Material material = LoadOrCreateMaterial(objFilePath);

        if (material == null)
        {
            Debug.LogError("マテリアルの生成またはロードに失敗しました。");
            return;
        }

        Debug.Log("マテリアルを全てのメッシュに適用します...");
        ApplyMaterialToAllMeshes(obj, material);

        Debug.Log("シェーダーを全てのメッシュに適用します...");
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("Standardシェーダーが見つかりません。処理を中断します。");
            return;
        }

        ApplyShaderToAllMeshes(obj, shader);
        Debug.Log("Material and Shader適用プロセスが完了しました。");
    }

    private static Material LoadOrCreateMaterial(string objFilePath)
    {
        Debug.Log("マテリアルをロードまたは作成中...");

        string mtlFilePath = Path.ChangeExtension(objFilePath, ".mtl");
        Debug.Log($"対応するMTLファイルのパス: {mtlFilePath}");

        if (File.Exists(mtlFilePath))
        {
            Debug.Log($"MTLファイルをロード中: {mtlFilePath}");
            return LoadMaterialFromMTL(mtlFilePath);
        }
        else
        {
            Debug.LogWarning("MTLファイルが見つかりません。デフォルトの赤色マテリアルを使用します。");
            return CreateRedMaterial();
        }
    }

    private static Material LoadMaterialFromMTL(string mtlFilePath)
    {
        try
        {
            Debug.Log("MTLファイルを読み込んで内容を解析します...");
            string mtlContent = File.ReadAllText(mtlFilePath);
            Debug.Log("MTLファイルの内容: " + mtlContent);

            // 仮実装のため、ここで新しいMaterialを返す
            Shader shader = Shader.Find("Standard");
            return new Material(shader);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"MTLファイルの読み込み中にエラーが発生しました: {ex.Message}");
            return null;
        }
    }

    private static Material CreateRedMaterial()
    {
        Debug.Log("赤色マテリアルを作成中...");
        Material redMaterial = new Material(Shader.Find("Standard"));

        if (redMaterial == null)
        {
            Debug.LogError("赤色マテリアルの作成に失敗しました。");
        }
        else
        {
            redMaterial.color = Color.red;
            Debug.Log("赤色マテリアルが正常に作成されました。");
        }

        return redMaterial;
    }

    private static void ApplyMaterialToAllMeshes(GameObject obj, Material material)
    {
        Debug.Log("全てのMeshRendererを取得してマテリアルを適用します...");
        MeshRenderer[] meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();

        if (meshRenderers.Length == 0)
        {
            Debug.LogWarning("MeshRendererが見つかりませんでした。");
            return;
        }

        foreach (var renderer in meshRenderers)
        {
            renderer.material = material;
        }

        Debug.Log("マテリアルの適用が完了しました。");
    }

    private static void ApplyShaderToAllMeshes(GameObject obj, Shader shader)
    {
        Debug.Log("全てのMeshRendererを取得してシェーダーを適用します...");
        MeshRenderer[] meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();

        if (meshRenderers.Length == 0)
        {
            Debug.LogWarning("MeshRendererが見つかりませんでした。");
            return;
        }

        foreach (var renderer in meshRenderers)
        {
            if (renderer.material != null)
            {
                renderer.material.shader = shader;
                Debug.Log($"シェーダーを適用: {renderer.name}");
            }
            else
            {
                Debug.LogWarning($"MaterialがnullのRendererがあります: {renderer.name}");
            }
        }

        Debug.Log("シェーダーの適用が完了しました。");
    }
}
