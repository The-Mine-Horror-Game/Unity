using UnityEngine;

public class TestScript : MonoBehaviour
{

    public Transform initialTransform;
    public float timeElapsed;

    // Update is called once per frame
    void Update()
    {
        var xRot = transform.rotation.eulerAngles.x;
        var yRot = transform.rotation.eulerAngles.y;
        var zRot = transform.rotation.eulerAngles.z;
        timeElapsed += Time.deltaTime;
        transform.rotation = Quaternion.Euler(xRot, yRot, zRot + (10f * Time.deltaTime));
        var newRot = Quaternion.Euler(xRot, yRot, 30);
        transform.rotation = Quaternion.Slerp(initialTransform.rotation, newRot, timeElapsed);
        //if(timeElapsed > 1)
            
    }
}
