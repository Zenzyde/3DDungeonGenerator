using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Del av examensarbete för kandidatexamen vid Högskolan i Skövde med inriktning dataspelsutveckling, år 2021. Skapat av Emil Birgersson
/// </summary>

[System.Serializable]
public class Room
{
	public Transform RoomObject { get; private set; }

	public Transform[] ChildTiles { get; private set; }

	public BoxCollider[] ChildColliders { get; private set; }

	public MeshRenderer[] ChildRenderers { get; private set; }

	public int SizeX { get; private set; }
	public int SizeZ { get; private set; }

	public BoxCollider RoomCollider { get; private set; }

	public int RoomIndex { get; private set; }

	public Room(Transform roomObject, List<Transform> childTiles, int X, int Z, BoxCollider roomCollider, int roomIndex)
	{
		RoomObject = roomObject;
		ChildTiles = new Transform[childTiles.Count];
		ChildColliders = new BoxCollider[childTiles.Count];
		ChildRenderers = new MeshRenderer[childTiles.Count];
		for (int i = 0; i < childTiles.Count; i++)
		{
			ChildTiles[i] = childTiles[i];
			ChildColliders[i] = childTiles[i].GetComponent<BoxCollider>();
			ChildRenderers[i] = childTiles[i].GetComponent<MeshRenderer>();
		}

		SizeX = X;
		SizeZ = Z;
		RoomCollider = roomCollider;
		RoomIndex = roomIndex;
	}
}