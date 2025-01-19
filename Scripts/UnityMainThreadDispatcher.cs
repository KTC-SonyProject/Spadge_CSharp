using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher instance;

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (instance == null)
            {
                var obj = new GameObject("UnityMainThreadDispatcher");
                instance = obj.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }

    /// <summary>
    /// メインスレッドで実行するアクションをキューに追加
    /// </summary>
    /// <param name="action">実行するアクション</param>
    public static void Enqueue(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    private void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                var action = executionQueue.Dequeue();
                action?.Invoke();
            }
        }
    }
}
