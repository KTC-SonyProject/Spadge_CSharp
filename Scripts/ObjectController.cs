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
            Debug.Log("�T�[�o�[�ւ̐ڑ������݂܂�...");
            client = new TcpClient();
            client.Connect(serverIP, serverPort);
            stream = client.GetStream();
            isConnected = true;
            Debug.Log("�T�[�o�[�ɐڑ����܂���");

            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("�T�[�o�[�ڑ����ɃG���[���������܂���: " + e.Message);
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
                Debug.Log("�T�[�o�[�փ��b�Z�[�W�𑗐M���܂���: " + message);
            }
            catch (Exception e)
            {
                Debug.LogError("���b�Z�[�W���M���ɃG���[���������܂���: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("�T�[�o�[�֐ڑ�����Ă��Ȃ����߃��b�Z�[�W�𑗐M�ł��܂���");
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
                if (bytesRead == 0) throw new Exception("�T�[�o�[�Ƃ̐ڑ����ؒf����܂���");

                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                while (true)
                {
                    string message = messageBuffer.ToString();
                    int newlineIndex = message.IndexOf('\n');
                    if (newlineIndex < 0) break;

                    string header = message.Substring(0, newlineIndex).Trim();
                    messageBuffer.Remove(0, newlineIndex + 1);

                    Debug.Log($"��M�����w�b�_�[: {header}");

                    string[] parts = header.Split(' ');
                    if (parts.Length != 2)
                    {
                        Debug.LogError("�w�b�_�[�`�����s��");
                        break;
                    }

                    string commandType = parts[0];
                    int bodySize = int.Parse(parts[1]);

                    if (messageBuffer.Length < bodySize) break;

                    string body = messageBuffer.ToString(0, bodySize);
                    messageBuffer.Remove(0, bodySize);

                    Debug.Log($"��M����BODY: {body}");

                    // ���C���X���b�h��HandleCommand���Ăяo��
                    UnityMainThreadDispatcher.Enqueue(() => HandleCommand(commandType, body));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"��M�������ɃG���[������: {e.Message}");
            Disconnect();
        }
    }


    private void HandleCommand(string commandType, string body)
    {
        Debug.Log($"HandleCommand�Ăяo�� - CommandType: {commandType}, Body: {body}");
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
                Debug.LogWarning($"�s���ȃR�}���h�^�C�v: {commandType}");
                SendResponse("{\"status_code\": 400, \"status_message\": \"Bad Request\", \"error\": \"Unknown command\"}");
                break;
        }
    }

    private void HandleControlCommand(string body)
    {
        try
        {
            Debug.Log($"CONTROL�R�}���h��M: {body}");

            if (OBJLoader == null)
            {
                Debug.LogError("OBJLoader��null�ł��B");
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
                Debug.LogError($"JSON�̉�͒��ɃG���[: {e.Message}");
                SendResponse("{\"status_code\": 400, \"status_message\": \"Bad Request\", \"error\": \"Invalid JSON format\"}");
                return;
            }

            Debug.Log($"�I�u�W�F�N�g {commandData.object_id} �ɑ΂��鑀��: {commandData.action}");

            switch (commandData.action)
            {
                case "next":
                    OBJLoader.LoadNextOBJ();
                    Debug.Log("����OBJ�t�@�C�������[�h���܂���");
                    break;

                case "previous":
                    OBJLoader.LoadPreviousOBJ();
                    Debug.Log("�O��OBJ�t�@�C�������[�h���܂���");
                    break;
                case "ON":
                    OBJLoader.RotatorManage(true);
                    Debug.Log("��]���J�n���܂���");
                    break;
                case "OFF":
                    OBJLoader.RotatorManage(false);
                    Debug.Log("��]���~���܂���");
                    break;
                default:
                    Debug.LogWarning($"�s���ȃA�N�V����: {commandData.action}");
                    SendResponse("{\"status_code\": 400, \"status_message\": \"Bad Request\", \"error\": \"Unknown action\"}");
                    return;
            }

            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"success\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"CONTROL�R�}���h�������ɃG���[: {e.Message}");
            SendResponse($"{{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"{e.Message}\"}}");
        }
    }

    private void HandleSceneCommand(string body)
    {
        try
        {
            var sceneData = JsonUtility.FromJson<SceneCommandData>(body);
            Debug.Log($"�V�[���� {sceneData.scene_name} �ɐ؂�ւ��܂�");
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"scene changed\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"SCENE�R�}���h�������ɃG���[: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to change scene\"}");
        }
    }

    private void HandleUpdateCommand(string body)
    {
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");
        try
        {
            var objFileName = JsonUtility.FromJson<UpdateCommandData>(body);
            Debug.Log($"UPDATE�R�}���h��M: {objFileName}�ɕύX");
            OBJLoader.LoadOBJByName(objFileName.file_name);
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"object updated\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"UPDATE�R�}���h�������ɃG���[: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to update object\"}");
        }
    }

    private void HandleTransferCommand(string body)
    {
        Debug.Log($"TRANSFER�R�}���h��M: {body}");
        try
        {
            var transferData = JsonUtility.FromJson<TransferCommandData>(body);
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"object updated\"}");
            ReceiveFileData(transferData.file_name, transferData.file_size);
        }
        catch (Exception e)
        {
            Debug.LogError($"TRANSFER�R�}���h�������ɃG���[: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to process TRANSFER command\"}");
        }
    }

    private void ReceiveFileData(string fileName, long fileSize)
    {
        try
        {
            // �t�@�C���f�[�^����M
            byte[] fileBuffer = new byte[fileSize];
            int totalRead = 0;

            while (totalRead < fileSize)
            {
                int bytesRead = stream.Read(fileBuffer, totalRead, fileBuffer.Length - totalRead);
                if (bytesRead == 0)
                {
                    throw new Exception("�T�[�o�[����ؒf����܂���");
                }
                totalRead += bytesRead;
            }

            // �ۑ���̃p�X���쐬 (Models�t�H���_�[)
            string modelsFolderPath = Path.Combine(persistentDataPath, "Models");
            if (!Directory.Exists(modelsFolderPath))
            {
                Directory.CreateDirectory(modelsFolderPath); // �t�H���_�[���쐬
            }

            string savePath = Path.Combine(modelsFolderPath, fileName);
            File.WriteAllBytes(savePath, fileBuffer);
            Debug.Log($"�t�@�C����M�E�ۑ�����: {savePath} (�T�C�Y: {fileSize}�o�C�g)");

            objFileName = fileName;
            Debug.Log($"�I�u�W�F�N�g��ǂݎ�蒆 : {objFileName}");
            OBJLoader.LoadFilePaths();
            OBJLoader.LoadOBJByName(objFileName);

            // ���X�|���X���M
            SendResponse("{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"file received\"}");
        }
        catch (Exception e)
        {
            Debug.LogError($"�t�@�C����M�������ɃG���[: {e.Message}");
            SendResponse("{\"status_code\": 500, \"status_message\": \"Internal Server Error\", \"error\": \"Failed to receive file\"}");
        }
    }

    public void GetList()
    {
        string modelsFolderPath = Path.Combine(persistentDataPath, "Models");

        // Models�t�H���_�[�����݂��邩�m�F
        if (!Directory.Exists(modelsFolderPath))
        {
            Debug.LogError($"Models�t�H���_�[�����݂��܂���: {modelsFolderPath}");
            objFilePaths = new string[0];  // �t�@�C���p�X�̔z�����ɐݒ�
            return;
        }

        // Models�t�H���_�[����*.obj�t�@�C�����擾
        objFilePaths = Directory.GetFiles(modelsFolderPath, "*.obj");

        // OBJ�t�@�C����������Ȃ��ꍇ
        if (objFilePaths.Length == 0)
        {
            Debug.LogError("Models�t�H���_�[����OBJ�t�@�C����������܂���ł���");
        }
        else
        {
            Debug.Log("OBJ�t�@�C���̃��X�g��ǂݍ��݂܂���:");

            // �t�@�C�����̃��X�g���쐬
            var fileNames = objFilePaths.Select(path => Path.GetFileName(path)).ToList();

            // JSON�`���̃��X�|���X�𒼐ڍ쐬
            string jsonResponse = "{\"status_code\": 200, \"status_message\": \"OK\", \"result\": \"" + string.Join(", ", fileNames) + "\"}";

            // �f�o�b�O���O�ɏo��
            Debug.Log(jsonResponse);

            // JSON��1��ő��M
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
                    Debug.LogError("���݃��[�h����Ă���I�u�W�F�N�g������܂���B");
                    SendResponse("{\"status_code\": 404, \"status_message\": \"Not Found\", \"error\": \"No object currently loaded\"}");
                }
            }
            else
            {
                Debug.LogError("OBJLoaderScript�̃C���X�^���X��������܂���B");
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
            Debug.Log($"���X�|���X���M: {message}");
            //Debug.Log($"���X�|���X���M: {header.Trim()} {responseBody}");
        }
        catch (Exception e)
        {
            Debug.LogError($"���X�|���X���M���ɃG���[���������܂���: {e.Message}");
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
            Debug.Log("�T�[�o�[�Ƃ̐ڑ����I�����܂���");
        }
    }
    private void Quit()
    {
        Debug.Log("�A�v���P�[�V�������I�����܂��B");
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
