using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;


public class ObjectsControll : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 8765;

    private TcpClient client;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;

    private string persistentDataPath;

    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    public OBJLoaderScript OBJLoader;
    private GameObject currentLoadedObj; // 現在のロード中のオブジェクト

    private class ReceivedFileData
    {
        public string fileName;
        public long fileSize;
        public byte[] fileData;
    }
    private ConcurrentQueue<ReceivedFileData> fileQueue = new ConcurrentQueue<ReceivedFileData>();

    void Awake()
    {
        persistentDataPath = Application.persistentDataPath;
    }

    void Start()
    {
        // Singletonパターンを使ってOBJLoaderを取得
        OBJLoader = OBJLoaderScript.Instance;

        if (OBJLoader == null)
        {
            Debug.LogError("OBJLoaderのインスタンス取得に失敗しました！");
            return;
        }

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

                    HandleCommand(commandType, body);
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
            case "SCENE":
                HandleSceneCommand(body);
                break;
            case "UPDATE":
                HandleUpdateCommand(body);
                break;
            case "TRANSFER":
                HandleTransferCommand(body);
                break;
            case "PING":
                SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"message\": \"pong\"}");
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
        try
        {
            var updateData = JsonUtility.FromJson<UpdateCommandData>(body);
            Debug.Log($"オブジェクト {updateData.object_id} を {updateData.file_name} に更新します");
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
        try
        {
            var transferData = JsonUtility.FromJson<TransferCommandData>(body);
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

            string savePath = Path.Combine(persistentDataPath, fileName);
            File.WriteAllBytes(savePath, fileBuffer);
            Debug.Log($"ファイル受信・保存完了: {savePath} (サイズ: {fileSize}バイト)");

            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"file received\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ファイル受信処理中にエラー: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to receive file\"}");
        }
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
