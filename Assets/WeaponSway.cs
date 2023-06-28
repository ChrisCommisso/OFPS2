using UnityEngine;

public class WeaponSway : MonoBehaviour
{
    public float swayAmount;       // The amount of sway rotation
    public float maxSwayAmount;    // The maximum sway amount
    public float smoothFactor;     // The smooth factor for interpolation

    private Quaternion initialRotation; // The initial rotation of the weapon

    private void Start()
    {
        initialRotation = transform.localRotation;
    }

    private void Update()
    {
        // Get the mouse movement axis values
        float moveX = -Input.GetAxis("Mouse X") * swayAmount;
        float moveY = -Input.GetAxis("Mouse Y") * swayAmount;


        // Limit the sway amount
        moveX = Mathf.Clamp(-moveX, -maxSwayAmount, maxSwayAmount);
        moveY = Mathf.Clamp(-moveY, -maxSwayAmount, maxSwayAmount);

        // Calculate the target rotation based on the mouse movement
        Quaternion targetRotation = Quaternion.Euler(moveY, moveX, 0f) * initialRotation;

        // Smoothly interpolate between the current rotation and the target rotation
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * smoothFactor);
    }
}
