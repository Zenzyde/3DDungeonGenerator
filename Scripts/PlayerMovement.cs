using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
	[SerializeField] private float moveSpeed;

	private CharacterController controller;

	private Vector3 movement;

	private bool bSetupComplete = false;

	void Awake()
	{
		controller = GetComponent<CharacterController>();
	}

	// Start is called before the first frame update
	IEnumerator Start()
	{
		DungeonGenerator3D generator3D = FindObjectOfType<DungeonGenerator3D>();
		yield return new WaitUntil(() => generator3D.DUNGEONDONE);
		transform.position = generator3D.START.RoomObject.position;
		controller.center = Vector3.zero;
		bSetupComplete = true;
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
			Application.Quit();

		if (!bSetupComplete)
			return;

		movement = transform.right * Input.GetAxisRaw("Horizontal") + transform.forward * Input.GetAxisRaw("Vertical");

		movement.Normalize();

		movement *= moveSpeed * Time.deltaTime;
	}

	void FixedUpdate()
	{
		if (!bSetupComplete)
			return;

		movement += Physics.gravity * Time.fixedDeltaTime;

		controller.Move(movement);
	}
}