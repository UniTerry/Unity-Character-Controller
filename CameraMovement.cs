using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public Rigidbody player; //player, used for horizontal movement of the camera
    public Transform head; //player's head position, this will be where the camera will stay relative to the player

    //mouse sensitivity
    public float horizontalSensitivity = 100f;
    public float verticalSensitivity = 100f;

    //maximum vertical angle the camera can look up or down to
    public float maxAngle = 90f; 

    //rotation of the camera
    float yRotation;
    float xRotation;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        //camera follows the head's position and rotation
        transform.position = head.position;
        transform.rotation = head.rotation; 

        //get the mouse input from the player
        float xMouse = Input.GetAxis("Mouse X") * horizontalSensitivity * Time.deltaTime;
        float yMouse = Input.GetAxis("Mouse Y") * verticalSensitivity * Time.deltaTime;

        //rotate the head for up and down movement
        yRotation -= yMouse;
        yRotation = Mathf.Clamp(yRotation, -maxAngle, maxAngle);
        head.localRotation = Quaternion.Euler(yRotation, 0, 0);

        //rotate the player for left and right movement
        xRotation += xMouse;
        player.transform.rotation = Quaternion.Euler(0, xRotation, 0); 
    }
}
