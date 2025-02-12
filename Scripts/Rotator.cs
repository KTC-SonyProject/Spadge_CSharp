using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("x軸の回転角度")]
    private float rotateX = 0f;  // 回転速度を遅くするため小さな値に設定

    [SerializeField]
    [Tooltip("y軸の回転角度")]
    private float rotateY = 5f;

    [SerializeField]
    [Tooltip("z軸の回転角度")]
    private float rotateZ = 0f;

    // 回転を制御するフラグ
    private bool isRotating = false;

    // Update is called once per frame
    void Update()
    {
        if (isRotating)
        {
            // X,Y,Z軸に対してそれぞれ、指定した角度ずつ回転させている。
            // deltaTimeをかけることで、フレームごとではなく、1秒ごとに回転するようにしている。
            transform.Rotate(new Vector3(rotateX, rotateY, rotateZ) * Time.deltaTime);
        }
    }

    // 回転をオンにするメソッド
    public void StartRotation(GameObject currentLoadedObj)
    {
        // currentLoadedObjを指定したオブジェクトに対して回転を開始
        currentLoadedObj.GetComponent<Rotator>().isRotating = true;
    }

    // 回転をオフにするメソッド
    public void StopRotation(GameObject currentLoadedObj)
    {
        // currentLoadedObjを指定したオブジェクトに対して回転を停止
        currentLoadedObj.GetComponent<Rotator>().isRotating = false;
    }
}
