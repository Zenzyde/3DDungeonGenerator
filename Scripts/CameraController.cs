using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
	[SerializeField] private float rotationSpeed;

	private Transform player;

	private Vector2 mouseInput;

	void Start()
	{
		player = transform.parent;
	}

	// Update is called once per frame
	void Update()
	{
		mouseInput += new Vector2
		{
			y = Input.GetAxis("Mouse X"),
				x = -Input.GetAxis("Mouse Y")
		};

		mouseInput.x = Mathf.Clamp(mouseInput.x, -45, 45);

		transform.localRotation = Quaternion.Euler(mouseInput.x, 0, 0);

		player.rotation = Quaternion.Euler(player.rotation.x, mouseInput.y, player.rotation.z);
	}
}