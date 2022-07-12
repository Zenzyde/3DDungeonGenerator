using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Del av examensarbete för kandidatexamen vid Högskolan i Skövde med inriktning dataspelsutveckling, år 2021. Skapat av Emil Birgersson
/// </summary>

public class OverlapFixingAgent : MonoBehaviour
{
	public bool IsOverlapping { get { return isOverlapping; } }
	private bool isOverlapping = true;

	private BoxCollider boxCollider;
	private Transform self;

	void Awake()
	{
		boxCollider = GetComponent<BoxCollider>();
		self = transform;
	}

	void FixedUpdate()
	{
		Collider[] collisions = Physics.OverlapBox(self.position + boxCollider.center, boxCollider.size / 2, Quaternion.identity, LayerMask.GetMask("Room"));
		isOverlapping = false;
		if (collisions.Length > 0 && !SelfColliding(collisions))
		{
			isOverlapping = true;
			int neighbours = 0;
			Vector3 moveAdjustVector = Vector3.zero;
			for (int i = 0; i < collisions.Length; i++)
			{
				Collider collision = collisions[i];
				if (collision.transform == self) continue;
				moveAdjustVector += self.position - collision.transform.position;
				neighbours++;
			}
			if (neighbours > 0)
			{
				moveAdjustVector /= neighbours;
				moveAdjustVector.Normalize();
				self.position += moveAdjustVector * Time.fixedDeltaTime;
				CeilFloorPosition(self);
			}
		}

		//* Switched to doing active ceil-flooring between collision checks in order to make sure ceil-flooring doesn't screw up placement at the end
		//* Re-introduced ceil-flooring at the end to make sure all tile-rooms are ceiled or floored to integer-position
		if (!isOverlapping)
			CeilFloorPosition(self);
	}

	// Checking for self-collision early so that no more extra collision checks are done when not needed
	bool SelfColliding(Collider[] collision)
	{
		return collision[0].transform == self && collision.Length == 1;
	}

	void CeilFloorPosition(Transform t)
	{
		t.position = new Vector3()
		{
			x = t.position.x <= 0.5f ? Mathf.FloorToInt(t.position.x) : Mathf.CeilToInt(t.position.x),
				y = t.position.y <= 0.5f ? Mathf.FloorToInt(t.position.y) : Mathf.CeilToInt(t.position.y),
				z = t.position.z <= 0.5f ? Mathf.FloorToInt(t.position.z) : Mathf.CeilToInt(t.position.z)
		};
	}
}