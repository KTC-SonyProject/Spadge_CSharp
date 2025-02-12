using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("x���̉�]�p�x")]
    private float rotateX = 0f;  // ��]���x��x�����邽�ߏ����Ȓl�ɐݒ�

    [SerializeField]
    [Tooltip("y���̉�]�p�x")]
    private float rotateY = 5f;

    [SerializeField]
    [Tooltip("z���̉�]�p�x")]
    private float rotateZ = 0f;

    // ��]�𐧌䂷��t���O
    private bool isRotating = false;

    // Update is called once per frame
    void Update()
    {
        if (isRotating)
        {
            // X,Y,Z���ɑ΂��Ă��ꂼ��A�w�肵���p�x����]�����Ă���B
            // deltaTime�������邱�ƂŁA�t���[�����Ƃł͂Ȃ��A1�b���Ƃɉ�]����悤�ɂ��Ă���B
            transform.Rotate(new Vector3(rotateX, rotateY, rotateZ) * Time.deltaTime);
        }
    }

    // ��]���I���ɂ��郁�\�b�h
    public void StartRotation(GameObject currentLoadedObj)
    {
        // currentLoadedObj���w�肵���I�u�W�F�N�g�ɑ΂��ĉ�]���J�n
        currentLoadedObj.GetComponent<Rotator>().isRotating = true;
    }

    // ��]���I�t�ɂ��郁�\�b�h
    public void StopRotation(GameObject currentLoadedObj)
    {
        // currentLoadedObj���w�肵���I�u�W�F�N�g�ɑ΂��ĉ�]���~
        currentLoadedObj.GetComponent<Rotator>().isRotating = false;
    }
}
