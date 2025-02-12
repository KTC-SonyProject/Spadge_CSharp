using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;
using Dummiesman;
using System.Linq;

public class ObjectController : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 8765;

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;

    private string persistentDataPath;

    public OBJLoaderScript OBJLoader;
    private string objFileName;
    private string[] objFilePaths;


    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    private class ReceivedFileData
    {
        public string fileName;
        public long fileSize;
        public byte[] fileData;
    }
    private ConcurrentQueue<ReceivedFileData> fileQueue = new ConcurrentQueue<ReceivedFileData>();
    private readonly string currentLoadedObj;

    void Awake()
    {
        persistentDataPath = Application.persistentDataPath;
    }

    void Start()
    {
        ConnectToServer();
    }

    void ConnectToServer()
    {
        try
        {
            Debug.Log("サーバーへの接続を試みます...");
            client = new TcpClient();
            client.Connect(serverIP, serverPort);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("サーバーに接続しました");

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("サーバー接続中にエラーが発生しました: " + e.Message);
            isConnected = false;
        }
    }

    public void SendMessageToServer(string message)
    {
        if (isConnected && stream != null)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
                Debug.Log("サーバーへメッセージを送信しました: " + message);
            }
            catch (Exception e)
            {
                Debug.LogError("メッセージ送信中にエラーが発生しました: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("サーバーへ接続されていないためメッセージを送信できません");
        }
    }

    private void ReceiveData()
    {
        try
        {
            byte[] buffer = new byte[1024];
            StringBuilder messageBuffer = new StringBuilder();

            while (isConnected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) throw new Exception("サーバーとの接続が切断されました");

                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                while (true)
                {
                    string message = messageBuffer.ToString();
                    int newlineIndex = message.IndexOf('\n');
                    if (newlineIndex < 0) break;

                    string header = message.Substring(0, newlineIndex).Trim();
                    messageBuffer.Remove(0, newlineIndex + 1);

                    Debug.Log($"受信したヘッダー: {header}");

                    string[] parts = header.Split(' ');
                    if (parts.Length != 2)
                    {
                        Debug.LogError("ヘッダー形式が不正");
                        break;
                    }

                    string commandType = parts[0];
                    int bodySize = int.Parse(parts[1]);

                    if (messageBuffer.Length < bodySize) break;

                    string body = messageBuffer.ToString(0, bodySize);
                    messageBuffer.Remove(0, bodySize);

                    Debug.Log($"受信したBODY: {body}");

                    // メインスレッドでHandleCommandを呼び出す
                    UnityMainThreadDispatcher.Enqueue(() => HandleCommand(commandType, body));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"受信処理中にエラーが発生: {e.Message}");
            Disconnect();
        }
    }


    private void HandleCommand(string commandType, string body)
    {
        Debug.Log($"HandleCommand呼び出し - CommandType: {commandType}, Body: {body}");
        switch (commandType)
        {
            case "CONTROL":
                HandleControlCommand(body);
                break;
            case "TRANSFER":
                HandleTransferCommand(body);
                break;
            //case "SCENE":
            //    HandleSceneCommand(body);
            //    break;
            case "UPDATE":
                HandleUpdateCommand(body);
                break;
            case "NEXT":
                OBJLoader.LoadNextOBJ();
                SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"next command sccess\"}");
                break;
            case "PREVIOUS":
                OBJLoader.LoadPreviousOBJ();
                SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"previous command sccess\"}");
                break;
            case "LIST":
                GetList();
                break;
            case "PING":
                SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"message\": \"pong\"}");
                break;
            case "GET_MODEL":
                GetModel();
                break;
            case "QUIT":
                Quit();
                break;
            default:
                Debug.LogWarning($"不明なコマンドタイプ: {commandType}");
                SendResponse("{\"status_code\": 400, \"status_message\": \"Bad Request\", \"error\": \"Unknown command\"}");
                break;
        }
    }

    private void HandleControlCommand(string body)
    {
        try
        {
            Debug.Log($"CONTROLコマンド受信: {body}");

            if (OBJLoader == null)
            {
                Debug.LogError("OBJLoaderがnullです。");
                SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"OBJLoader is null\"}");
                return;
            }

            ControlCommandData commandData;
            try
            {
                commandData = JsonUtility.FromJson<ControlCommandData>(body);
            }
            catch (Exception e)
            {
                Debug.LogError($"JSONの解析中にエラー: {e.Message}");
                SendResponse("{\"status_code\": 400, \"status_message\": \"Bad Request\", \"error\": \"Invalid JSON format\"}");
                return;
            }

            Debug.Log($"オブジェクト {commandData.object_id} に対する操作: {commandData.action}");

            switch (commandData.action)
            {
                case "next":
                    OBJLoader.LoadNextOBJ();
                    Debug.Log("次のOBJファイルをロードしました");
                    break;

                case "previous":
                    OBJLoader.LoadPreviousOBJ();
                    Debug.Log("前のOBJファイルをロードしました");
                    break;
                case "ON":
                    OBJLoader.RotatorManage(true);
                    Debug.Log("回転を開始しました");
                    break;
                case "OFF":
                    OBJLoader.RotatorManage(false);
                    Debug.Log("回転を停止しました");
                    break;
                default:
                    Debug.LogWarning($"不明なアクション: {commandData.action}");
                    SendResponse("{\"status_code\": 400, \"status_message\": \"Bad Request\", \"error\": \"Unknown action\"}");
                    return;
            }

            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"success\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"CONTROLコマンド処理中にエラー: {e.Message}");
            SendResponse($"{{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"{e.Message}\"}}");
        }
    }

    private void HandleSceneCommand(string body)
    {
        try
        {
            var sceneData = JsonUtility.FromJson<SceneCommandData>(body);
            Debug.Log($"シーンを {sceneData.scene_name} に切り替えます");
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"scene changed\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"SCENEコマンド処理中にエラー: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to change scene\"}");
        }
    }

    private void HandleUpdateCommand(string body)
    {
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");
        try
        {
            var objFileName = JsonUtility.FromJson<UpdateCommandData>(body);
            Debug.Log($"UPDATEコマンド受信: {objFileName}に変更");
            OBJLoader.LoadOBJByName(objFileName.file_name);
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"object updated\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"UPDATEコマンド処理中にエラー: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to update object\"}");
        }
    }

    private void HandleTransferCommand(string body)
    {
        Debug.Log($"TRANSFERコマンド受信: {body}");
        try
        {
            var transferData = JsonUtility.FromJson<TransferCommandData>(body);
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"object updated\"}");
            ReceiveFileData(transferData.file_name, transferData.file_size);
        }
        catch (Exception e)
        {
            Debug.LogError($"TRANSFERコマンド処理中にエラー: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to process TRANSFER command\"}");
        }
    }

    private void ReceiveFileData(string fileName, long fileSize)
    {
        try
        {
            // ファイルデータを受信
            byte[] fileBuffer = new byte[fileSize];
            int totalRead = 0;

            while (totalRead < fileSize)
            {
                int bytesRead = stream.Read(fileBuffer, totalRead, fileBuffer.Length - totalRead);
                if (bytesRead == 0)
                {
                    throw new Exception("サーバーから切断されました");
                }
                totalRead += bytesRead;
            }

            // 保存先のパスを作成 (Modelsフォルダー)
            string modelsFolderPath = Path.Combine(persistentDataPath, "Models");
            if (!Directory.Exists(modelsFolderPath))
            {
                Directory.CreateDirectory(modelsFolderPath); // フォルダーを作成
            }

            string savePath = Path.Combine(modelsFolderPath, fileName);
            File.WriteAllBytes(savePath, fileBuffer);
            Debug.Log($"ファイル受信・保存完了: {savePath} (サイズ: {fileSize}バイト)");

            objFileName = fileName;
            Debug.Log($"オブジェクトを読み取り中 : {objFileName}");
            OBJLoader.LoadFilePaths();
            OBJLoader.LoadOBJByName(objFileName);

            // レスポンス送信
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"file received\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ファイル受信処理中にエラー: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to receive file\"}");
        }
    }

    public void GetList()
    {
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");

        // Modelsフォルダーが存在するか確認
        if (!Directory.Exists(modelsFolderPath))
        {
            Debug.LogError($"Modelsフォルダーが存在しません: {modelsFolderPath}");
            objFilePaths = new string[0];  // ファイルパスの配列を空に設定
            return;
        }

        // Modelsフォルダー内の*.objファイルを取得
        objFilePaths = Directory.GetFiles(modelsFolderPath, "*.obj");

        // OBJファイルが見つからない場合
        if (objFilePaths.Length == 0)
        {
            Debug.LogError("Modelsフォルダー内にOBJファイルが見つかりませんでした");
        }
        else
        {
            Debug.Log("OBJファイルのリストを読み込みました:");

            // ファイル名のリストを作成
            var fileNames = objFilePaths.Select(path => Path.GetFileName(path)).ToList();

            // JSON形式のレスポンスを直接作成
            string jsonResponse = "{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"" + string.Join(", ", fileNames) + "\"}";

            // デバッグログに出力
            Debug.Log(jsonResponse);

            // JSONを1回で送信
            SendResponse(jsonResponse);
        }
    }

    private void GetModel()
    {
        UnityMainThreadDispatcher.Enqueue(() =>
        {
            if (OBJLoaderScript.Instance != null)
            {
                GameObject model = OBJLoaderScript.Instance.CurrentLoadedObj;
                if (model != null)
                {
                    string modelName = model.name;
                    Debug.Log(modelName);
                    SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"" + modelName + "\"}");
                }
                else
                {
                    Debug.LogError("現在ロードされているオブジェクトがありません。");
                    SendResponse("{\"status_code\": 404, \"status_message\": \"Not Found\", \"error\": \"No object currently loaded\"}");
                }
            }
            else
            {
                Debug.LogError("OBJLoaderScriptのインスタンスが見つかりません。");
                SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"OBJLoaderScript instance not found\"}");
            }
        });
    }









    private void SendResponse(string responseBody)
    {
        responseBody += "\n";

        int bodySize = Encoding.UTF8.GetBytes(responseBody).Length;
        string header = $"RESPONSE {bodySize}\n";
        string message = header + responseBody;
        byte[] data = Encoding.UTF8.GetBytes(message);
        //byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        //byte[] bodyBytes = Encoding.UTF8.GetBytes(responseBody);
        //byte[] message = new byte[headerBytes.Length + bodyBytes.Length];
        //headerBytes.CopyTo(message, 0);
        //bodyBytes.CopyTo(message, headerBytes.Length);

        try
        {
            stream.Write(data, 0, data.Length);
            Debug.Log($"レスポンス送信: {message}");
            //Debug.Log($"レスポンス送信: {header.Trim()} {responseBody}");
        }
        catch (Exception e)
        {
            Debug.LogError($"レスポンス送信中にエラーが発生しました: {e.Message}");
        }
    }

    private void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (client != null)
            {
                client.Close();
                client = null;
            }
            Debug.Log("サーバーとの接続を終了しました");
        }
    }
    private void Quit()
    {
        Debug.Log("アプリケーションを終了します。");
        UnityMainThreadDispatcher.Enqueue(() =>
        {
            Application.Quit();
        });
    }

    [Serializable]
    private class ControlCommandData
    {
        public string object_id;
        public string action;
    }

    [Serializable]
    private class SceneCommandData
    {
        public string scene_name;
    }

    [Serializable]
    private class UpdateCommandData
    {
        public string object_id;
        public string file_name;
    }

    [Serializable]
    private class TransferCommandData
    {
        public string file_name;
        public long file_size;
    }

    void OnApplicationQuit()
    {
        Disconnect();
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join();
        }
    }
}
