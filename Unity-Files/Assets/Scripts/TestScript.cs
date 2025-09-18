using UnityEngine;

public class TestScript : MonoBehaviour
{

    [SerializeField] private Transform target;
    
    // Update is called once per frame
    void FixedUpdate()
    {
        transform.position = target.position;
    }
}
