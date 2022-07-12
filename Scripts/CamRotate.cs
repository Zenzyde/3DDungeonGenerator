using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamRotate : MonoBehaviour
{
	[SerializeField] private float maxX, rotateSpeed, moveSpeed;

	private Vector3 rotation;

	void Start()
	{
		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;
	}

	// Update is called once per frame
	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
			Application.Quit();
		Move();
		Rotate();
	}

	void Move()
	{
		if (Input.GetKey(KeyCode.W))
		{
			transform.position += transform.forward * moveSpeed * Time.deltaTime;
		}
		if (Input.GetKey(KeyCode.S))
		{
			transform.position -= transform.forward * moveSpeed * Time.deltaTime;
		}
		if (Input.GetKey(KeyCode.D))
		{
			transform.position += transform.right * moveSpeed * Time.deltaTime;
		}
		if (Input.GetKey(KeyCode.A))
		{
			transform.position -= transform.right * moveSpeed * Time.deltaTime;
		}
	}

	void Rotate()
	{
		float X = Input.GetAxisRaw("Mouse X");
		float Y = Input.GetAxisRaw("Mouse Y");

		rotation = transform.localEulerAngles;
		rotation.x -= Y * rotateSpeed;
		rotation.x = HelperMethods.ClampAngle(rotation.x, -maxX, maxX);
		rotation.y += X * rotateSpeed;
		transform.rotation = Quaternion.Euler(rotation);
	}
}
