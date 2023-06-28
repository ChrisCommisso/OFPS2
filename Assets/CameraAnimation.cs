using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraAnimation : MonoBehaviour
{
    [Header("Position")]
    public float amount;
    public float maxAmount;
    public float smoothAmount;


    [Header("Rotation")]
    public float rotationAmount = 40f;
    public float maxRotationAmount = 100f;
    public float smoothRotation = 4f;

    [Space]
    public bool rotationX = true;
    public bool rotationY = true;
    public bool rotationZ = true;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float InputX;
    private float InputY;

    
    void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        Sway();
        Tilt();
    }
    private void Sway()
    {
        InputX = -Input.GetAxis("Mouse X");
        InputY = -Input.GetAxis("Mouse Y");

        float moveX = Mathf.Clamp(InputX * amount, -maxAmount, maxAmount);
        float moveY = Mathf.Clamp(InputY * amount, -maxAmount, maxAmount);

        Vector3 finalPosition = new Vector3(moveX, moveY, 0);

        transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition + initialPosition, Time.deltaTime * smoothAmount);

    }

    private void Tilt()
    {
        float tiltAroundX = Mathf.Clamp(InputY * rotationAmount, -maxRotationAmount, maxRotationAmount);
        float tiltAroundY = Mathf.Clamp(InputX * rotationAmount, -maxRotationAmount, maxRotationAmount);

        Quaternion finalRotation = Quaternion.Euler(
            rotationX ? -tiltAroundX : 0f,
            rotationY ? tiltAroundY : 0f,
            rotationZ ? tiltAroundY : 0f
        );

        transform.localRotation = Quaternion.Slerp(transform.localRotation, initialRotation * finalRotation, Time.deltaTime * smoothRotation);
    }

}
