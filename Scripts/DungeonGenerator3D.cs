using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DungeonGenerator3D : MonoBehaviour
{
	[SerializeField] DungeonTile[] DungeonTiles;
	[SerializeField] float RoomSpawnRange;
	[SerializeField] int RoomAmount;
	[SerializeField] Vector3Int MinRoomSize, MaxRoomSize, RoomAcceptanceSize;
	[SerializeField][Range(0f, 1f)] float DungeonCycleChance = 0.15f;
	[SerializeField][Range(.2f, .8f)] float BigRoomChance = 0.2f;
	[SerializeField] bool AllowDirectPathToExit = false;

	[Space]
	[SerializeField] AStarPathfindingSettings AStarPathfindingSettings;

	[Space]
	[SerializeField] bool Editor_VisualizeCorridorRooms = true;

	public bool DUNGEONDONE { get; private set; }

	public Room START { get { return startRoom; } }

	public Room END { get { return endRoom; } }

	public List<Room> ROOMS { get { return createdRooms; } }

	public List<DTEdge3D> DUNGEONGRAPH { get { return dungeonGraph; } }

	private List<Room> createdRooms, acceptedRooms, corridorRooms;
	private DTPoint3D[] dtGraph;
	private List<DTPoint3D> looseCorridorList;
	private List<DTEdge3D> mstGraph, residualCyclesGraph, dungeonGraph;
	private Room startRoom, endRoom;
	private Dictionary<Vector3Int, AStarNode> aStarGrid;
	private Vector3Int minDungeonBounds, maxDungeonBounds;

	System.Random random = new System.Random();
	// ParkMillerRandom pmRandom;

	// Start is called before the first frame update
	void Start()
	{
		CreateSpreadRooms();
		StartCoroutine(UntangleCreatedRooms(SelectAcceptableRooms));
	}

	void CreateSpreadRooms()
	{
		createdRooms = new List<Room>();

		for (int i = 0; i < RoomAmount; i++)
		{
			//* No need for multiplying with random value, inside unit sphere already puts a point somewhere inside a sphere,
			//* -- hence the name, instead of only at the circumference
			Vector3 point = transform.position + UnityEngine.Random.insideUnitSphere * RoomSpawnRange;
			Room tileRoom = CreateTileRoom(point, i);
			createdRooms.Add(tileRoom);
		}
	}

	//* Creation of tile-rooms shows a bias for creating very oblong room-tiles -- sort of fixed!
	Room CreateTileRoom(Vector3 center, int roomIndex)
	{
		Transform roomTile = new GameObject("RoomTile").transform;
		BoxCollider collider = roomTile.gameObject.AddComponent<BoxCollider>();
		collider.gameObject.layer = LayerMask.NameToLayer("Room");

		//* Very simple emulation of the random distribution used in TinyKeep
		//* -- Added the low chance getting a maximized size, seems to produce much more similar results, only gotta fixa the ratio issue now i think
		//* --- Skipping emulation of TinyKeep random distribution for now, doing my own thing with regular randomness
		int X = random.Next(MinRoomSize.x, MaxRoomSize.x);
		int Y = random.Next(MinRoomSize.y, MaxRoomSize.y);
		int Z = random.Next(MinRoomSize.z, MaxRoomSize.z);

		//* Half and substract in order to center positions
		X -= X / 2;
		Z -= Z / 2;

		List<Transform> childTiles = new List<Transform>();

		List<GameObject> wallTiles = new List<GameObject>();
		List<GameObject> cornerTiles = new List<GameObject>();
		List<GameObject> cornerTopTiles = new List<GameObject>();
		List<GameObject> cornerBottomTiles = new List<GameObject>();
		List<GameObject> roofTiles = new List<GameObject>();
		List<GameObject> floorTiles = new List<GameObject>();
		for (int i = 0; i < DungeonTiles.Length; i++)
		{
			EDungeonTile tileType = DungeonTiles[i].TileType;
			for (int j = 0; j < DungeonTiles[i].TileObjects.Length; j++)
			{
				GameObject tile = DungeonTiles[i].TileObjects[j];
				switch (tileType)
				{
				case EDungeonTile.corner:
					cornerTiles.Add(tile);
					break;
				case EDungeonTile.cornerTop:
					cornerTopTiles.Add(tile);
					break;
				case EDungeonTile.cornerBottom:
					cornerBottomTiles.Add(tile);
					break;
				case EDungeonTile.floor:
					floorTiles.Add(tile);
					break;
				case EDungeonTile.roof:
					roofTiles.Add(tile);
					break;
				case EDungeonTile.wall:
					wallTiles.Add(tile);
					break;
				}
			}
		}

		GameObject chosenWallTile = wallTiles[random.Next(0, wallTiles.Count)];
		GameObject chosenCornerTile = cornerTiles[random.Next(0, cornerTiles.Count)];
		GameObject chosenCornerTopTile = cornerTopTiles[random.Next(0, cornerTopTiles.Count)];
		GameObject chosenCornerBottomTile = cornerBottomTiles[random.Next(0, cornerBottomTiles.Count)];
		GameObject chosenFloorTile = floorTiles[random.Next(0, floorTiles.Count)];
		GameObject chosenRoofTile = roofTiles[random.Next(0, roofTiles.Count)];

		bool IsWall(Vector3 pos, Vector3 min, Vector3 max)
		{
			return (pos.x == min.x && pos.z > min.z && pos.z < max.z
				|| pos.x == max.x && pos.z > min.z && pos.z < max.z
				|| pos.z == min.z && pos.x > min.x && pos.x < max.x
				|| pos.z == max.z && pos.x > min.x && pos.x < max.x) && pos.y >= 0 && pos.y <= max.y - 1;
		}

		bool IsFloor(Vector3 pos, Vector3 min, Vector3 max)
		{
			return pos.x >= min.x && pos.x <= max.x && pos.z >= min.z && pos.z <= max.z && pos.y == 0;
		}

		bool IsRoof(Vector3 pos, Vector3 min, Vector3 max)
		{
			return pos.x >= min.x && pos.x <= max.x && pos.z >= min.z && pos.z <= max.z && pos.y == max.y - 1;
		}

		bool IsCorner(Vector3 pos, Vector3 min, Vector3 max)
		{
			return pos.x == min.x && pos.z == max.z || pos.x == min.x && pos.z == min.z || pos.x == max.x && pos.z == max.z || pos.x == max.x && pos.z == min.z;
		}

		bool IsCornerTop(Vector3 pos, Vector3 min, Vector3 max)
		{
			return IsCorner(pos, min, max) && pos.y == max.y - 1;
		}

		bool IsCornerBottom(Vector3 pos, Vector3 min, Vector3 max)
		{
			return IsCorner(pos, min, max) && pos.y == 0;
		}

		Quaternion GetFacing(EDungeonTile dungeonTileType, Vector3 pos, Vector3 min, Vector3 max)
		{
			switch (dungeonTileType)
			{
			case EDungeonTile.corner:
				if (pos.x == min.x && pos.z == max.z)
					return Quaternion.Euler(0, 90, 0);
				else if (pos.x == min.x && pos.z == min.z)
					return Quaternion.identity;
				else if (pos.x == max.x && pos.z == max.z)
					return Quaternion.Euler(0, 180, 0);
				else if (pos.x == max.x && pos.z == min.z)
					return Quaternion.Euler(0, 270, 0);
				break;
			case EDungeonTile.cornerBottom:
				if (pos.x == min.x && pos.z == max.z)
					return Quaternion.Euler(0, 90, 0);
				else if (pos.x == min.x && pos.z == min.z)
					return Quaternion.identity;
				else if (pos.x == max.x && pos.z == max.z)
					return Quaternion.Euler(0, 180, 0);
				else if (pos.x == max.x && pos.z == min.z)
					return Quaternion.Euler(0, 270, 0);
				break;
			case EDungeonTile.cornerTop:
				if (pos.x == min.x && pos.z == max.z)
					return Quaternion.Euler(0, 90, 0);
				else if (pos.x == min.x && pos.z == min.z)
					return Quaternion.identity;
				else if (pos.x == max.x && pos.z == max.z)
					return Quaternion.Euler(0, 180, 0);
				else if (pos.x == max.x && pos.z == min.z)
					return Quaternion.Euler(0, 270, 0);
				break;
			case EDungeonTile.wall:
				if (pos.x == min.x && pos.z > min.z && pos.z < max.z) //* Facing left
					return Quaternion.Euler(0, 270, 0);
				if (pos.z == max.z && pos.x > min.x && pos.x < max.x) //* Facing forward
					return Quaternion.identity;
				if (pos.x == max.x && pos.z > min.z && pos.z < max.z) //* Facing right
					return Quaternion.Euler(0, 90, 0);
				if (pos.z == min.z && pos.x > min.x && pos.x < max.x) //* Facing back
					return Quaternion.Euler(0, 180, 0);
				break;
			}
			return Quaternion.identity;
		}

		Vector3 min = new Vector3(-X, 0, -Z);
		Vector3 max = new Vector3(X, Y, Z);

		for (float x = -X; x <= X; x++)
		{
			for (float z = -Z; z <= Z; z++)
			{
				for (float y = 0; y < Y; y++)
				{
					Vector3 pos = new Vector3(x, y, z);
					//* Corners
					if (cornerTopTiles.Count > 0 && IsCornerTop(pos, min, max) && !IsWall(pos, min, max))
					{
						GameObject childWallTile = Instantiate(chosenCornerTopTile, pos, GetFacing(EDungeonTile.cornerTop, pos, min, max), roomTile);
						childWallTile.gameObject.layer = LayerMask.NameToLayer("Corner");
						childTiles.Add(childWallTile.transform);
						continue;
					}
					if (cornerBottomTiles.Count > 0 && IsCornerBottom(pos, min, max) && !IsWall(pos, min, max))
					{
						GameObject childWallTile = Instantiate(chosenCornerBottomTile, pos, GetFacing(EDungeonTile.cornerBottom, pos, min, max), roomTile);
						childWallTile.gameObject.layer = LayerMask.NameToLayer("Corner");
						childTiles.Add(childWallTile.transform);
						continue;
					}
					if (cornerTiles.Count > 0 && IsCorner(pos, min, max) && !IsWall(pos, min, max))
					{
						GameObject childWallTile = Instantiate(chosenCornerTile, pos, GetFacing(EDungeonTile.corner, pos, min, max), roomTile);
						childWallTile.gameObject.layer = LayerMask.NameToLayer("Corner");
						childTiles.Add(childWallTile.transform);
						continue;
					}
					//* Walls
					if (wallTiles.Count > 0 && IsWall(pos, min, max) && !IsCorner(pos, min, max))
					{
						// Debug.Log("Wall");
						GameObject childWallTile = Instantiate(chosenWallTile, pos, GetFacing(EDungeonTile.wall, pos, min, max), roomTile);
						childWallTile.gameObject.layer = LayerMask.NameToLayer("Wall");
						childTiles.Add(childWallTile.transform);
					}
					//* Floors
					if (floorTiles.Count > 0 && IsFloor(pos, min, max))
					{
						GameObject childWallTile = Instantiate(chosenFloorTile, pos, Quaternion.identity, roomTile);
						childWallTile.gameObject.layer = LayerMask.NameToLayer("Floor");
						childTiles.Add(childWallTile.transform);
					}
					//* Roofs
					if (roofTiles.Count > 0 && IsRoof(pos, min, max))
					{
						GameObject childWallTile = Instantiate(chosenRoofTile, pos, Quaternion.identity, roomTile);
						childWallTile.gameObject.layer = LayerMask.NameToLayer("Roof");
						childTiles.Add(childWallTile.transform);
					}
				}
			}
		}

		//* Re-center the collider
		float height = Y == 1 ? 0 : Y % 2 == 0 ? (Y / 2f) - 0.5f : Mathf.CeilToInt(Y / 3);
		Vector3 newCenter = collider.center;
		//newCenter -= new Vector3(.5f, 0, .5f);
		newCenter.y = height;
		collider.center = newCenter;
		//* Adjust the size to encompass all child-tiles & properly represent the volume of the room
		collider.size = new Vector3Int()
		{
			x = (X * 2) + 1, y = Y, z = (Z * 2) + 1
		};

		roomTile.position = center;

		return new Room(roomTile, childTiles, X, Z, collider, roomIndex);
	}

	IEnumerator UntangleCreatedRooms(System.Action callBack = null)
	{
		OverlapFixingAgent[] overlapFixAgents = new OverlapFixingAgent[createdRooms.Count];
		for (int i = 0; i < createdRooms.Count; i++)
		{
			OverlapFixingAgent overlapFixAgent = createdRooms[i].RoomObject.gameObject.AddComponent<OverlapFixingAgent>();
			overlapFixAgents[i] = overlapFixAgent;
		}

		bool[] agentsDoneFlags = new bool[overlapFixAgents.Length];
		WaitForFixedUpdate fixedWait = new WaitForFixedUpdate();

		bool AllAgentsDone()
		{
			for (int i = 0; i < overlapFixAgents.Length; i++)
			{
				if (overlapFixAgents[i].IsOverlapping)
					return false;
			}
			return true;
		}

		while (!AllAgentsDone())
		{
			for (int i = 0; i < overlapFixAgents.Length; i++)
			{
				OverlapFixingAgent currentAgent = overlapFixAgents[i];
				agentsDoneFlags[i] = currentAgent.IsOverlapping;
				yield return fixedWait;
			}
		}

		for (int i = 0; i < overlapFixAgents.Length; i++)
		{
			Destroy(overlapFixAgents[i]);
		}

		callBack?.Invoke();
	}

	async void SelectAcceptableRooms()
	{
		acceptedRooms = new List<Room>();
		corridorRooms = new List<Room>();

		foreach (Room room in createdRooms)
		{
			int X = (int) room.RoomCollider.size.x;
			int Z = (int) room.RoomCollider.size.z;
			int Y = (int) room.RoomCollider.size.y;
			if (X >= RoomAcceptanceSize.x && Z >= RoomAcceptanceSize.z && Y >= RoomAcceptanceSize.y)
			{
				acceptedRooms.Add(room);
			}
			else
			{
				corridorRooms.Add(room);
			}
		}

		if (acceptedRooms.Count < 4)
		{
			Debug.LogWarning("Less than 4 rooms accepted, aborting");
			return;
		}

		await CreateDelaunayTriangulation(FindPrimsMST);
	}

	async Task CreateDelaunayTriangulation(System.Action callBack = null)
	{
		DTPoint3D[] dTPoints = new DTPoint3D[acceptedRooms.Count];
		for (int i = 0; i < acceptedRooms.Count; i++)
		{
			dTPoints[i] = new DTPoint3D(Vector3Int.RoundToInt(acceptedRooms[i].RoomObject.position), acceptedRooms[i].RoomIndex, acceptedRooms[i]);
		}

		looseCorridorList = new List<DTPoint3D>();
		for (int i = 0; i < corridorRooms.Count; i++)
		{
			looseCorridorList.Add(new DTPoint3D(Vector3Int.RoundToInt(corridorRooms[i].RoomObject.position), corridorRooms[i].RoomIndex, corridorRooms[i]));
		}

		DelaunayTriangulate3D delaunayTriangulator = new DelaunayTriangulate3D(dTPoints, Vector3Int.RoundToInt(transform.position), RoomSpawnRange * RoomSpawnRange);
		dtGraph = await delaunayTriangulator.Triangulate();

		callBack?.Invoke();
	}

	//* Based on video: https://www.youtube.com/watch?v=cplfcGZmX7I
	void FindPrimsMST()
	{
		List<DTPoint3D> visitedPoints = new List<DTPoint3D>();
		List<DTEdge3D> usedEdges = new List<DTEdge3D>();

		DTPoint3D current = dtGraph[0];
		visitedPoints.Add(current);
		while (visitedPoints.Count < dtGraph.Length)
		{
			float length = float.MaxValue;
			DTPoint3D next = null;
			for (int i = 0; i < visitedPoints.Count; i++)
			{
				current = visitedPoints[i];

				if (current == null)
				{
					Debug.Log($"Finding Prim's MST stage: DTPoint current state: {current}");
					continue;
				}

				for (int j = 0; j < current.NEIGHBOURS.Count; j++)
				{
					DTPoint3D neighbour = current.NEIGHBOURS[j];
					float newLength = (neighbour.POSITION - current.POSITION).magnitude;
					if (!visitedPoints.Contains(neighbour) && newLength <= length)
					{
						length = newLength;
						next = neighbour;
					}
				}
			}

			if (next == null)
			{
				Debug.Log($"Finding Prim's MST stage: DTPoint next state: {next}");
				continue;
			}

			visitedPoints.Add(next);
			usedEdges.Add(new DTEdge3D(current, next));
			current = next;
		}

		mstGraph = new List<DTEdge3D>();
		mstGraph.AddRange(usedEdges);

		residualCyclesGraph = new List<DTEdge3D>();
		for (int i = 0; i < dtGraph.Length; i++)
		{
			DTPoint3D point = dtGraph[i];
			for (int j = 0; j < point.NEIGHBOURS.Count; j++)
			{
				DTPoint3D neighbour = point.NEIGHBOURS[j];
				if (usedEdges.Contains(new DTEdge3D(point, neighbour)) || usedEdges.Contains(new DTEdge3D(neighbour, point)))
				{
					continue;
				}
				residualCyclesGraph.Add(new DTEdge3D(point, neighbour));
			}
		}

		InsertCycles();
		InsertOverlappingRooms();
		ChooseStartEndRooms();
		CreateGrid();
		StartCoroutine(BuildCorridors());
	}

	//* Think this works now, original design based on the instructions by Phi Dinh on reddit
	void InsertCycles()
	{
		//* Changed from Floor to Ceil to increase the chance of at least one cycle being added
		int cyclesToAdd = Mathf.CeilToInt(residualCyclesGraph.Count * DungeonCycleChance);

		dungeonGraph = new List<DTEdge3D>();

		for (int i = 0; i < mstGraph.Count; i++)
		{
			dungeonGraph.Add(mstGraph[i]);
		}

		int cyclesAdded = 0;
		while (cyclesAdded < cyclesToAdd)
		{
			for (int i = residualCyclesGraph.Count - 1; i >= 0; i--)
			{
				DTEdge3D edge = residualCyclesGraph[i];
				if (random.NextDouble() < 0.125)
				{
					dungeonGraph.Add(edge);
					residualCyclesGraph.Remove(edge);
					cyclesAdded++;
				}
			}
		}
	}

	//* Iterate the dungeon graph containging the edges -- ensures i only look at the edges once
	//* Perform physics-overlapbox-check to find rooms that are in-between two edge-rooms
	//* Save rooms in-between, remove the connecting edge between the original point-pairs
	//* -- then iterate the saved rooms, find the corresponding room in the "loose corridor list"
	//* -- create new edge between previous room and current room, continue to iterate all "loose" rooms
	//* -- create new edge between last "loose" room and original edge-end -- room-chain is done!
	void InsertOverlappingRooms()
	{
		for (int i = dungeonGraph.Count - 1; i >= 0; i--)
		{
			DTEdge3D edge = dungeonGraph[i];

			//* Move a Physics.OverlapBox from start to end & activate all rooms that aren't activated
			//* Add the new rooms as neighbours to the current room in dungeonGraph

			DTPoint3D room = edge.START;
			DTPoint3D neighbour = edge.END;

			Vector3 start = room.POSITION;
			Vector3 end = neighbour.POSITION;

			Vector3 dummyPos = start;
			Vector3 overlapBoxSize = new Vector3(
				Mathf.Max(room.ROOM.RoomCollider.size.x, neighbour.ROOM.RoomCollider.size.x),
				Mathf.Max(room.ROOM.RoomCollider.size.y, neighbour.ROOM.RoomCollider.size.y),
				1);

			List<Collider> colliderSet = new List<Collider>();

			//* Add colliders faster by doing a boxcast & jumping from hit to hit to end
			while (true)
			{
				Vector3 boxVector = end - dummyPos;
				//* Boxcast from current to end to detect rooms in the way
				if (Physics.BoxCast(dummyPos, overlapBoxSize / 2, boxVector.normalized, out RaycastHit hit, Quaternion.LookRotation(boxVector).normalized,
						boxVector.magnitude, LayerMask.GetMask("Room")))
				{
					//* If length between start & end is less than current & start it means current has gone past end, abort!
					if ((end - start).sqrMagnitude < (dummyPos - start).sqrMagnitude)
						break;

					//* Only add colliders of rooms that aren't the start or end or room tiles
					if (hit.collider != room.ROOM.RoomCollider && hit.collider != neighbour.ROOM.RoomCollider && hit.transform.parent == null)
						colliderSet.Add(hit.collider);

					//* If we hit the neighbour it means we reached the end, abort!
					if (hit.collider == neighbour.ROOM.RoomCollider)
						break;

					//* Move dummypos to next position & boxcast!
					dummyPos = hit.point - hit.normal;
				}
				//* If nothing was hit there's no overlapping rooms to add, abort!
				else
					break;
			}

			//* Remove the original connection between start room and neighbour room
			//* -- i'll be creating a chain-link of rooms between the two either way
			room.RemoveEdge(neighbour);
			room.RemoveNeighbour(neighbour);

			//* Remove the DTEdge from dungeonGraph
			dungeonGraph.Remove(edge);

			DTPoint3D pointA = room;
			DTPoint3D pointB = null;

			for (int j = 0; j < colliderSet.Count; j++)
			{
				Collider colliderRoom = colliderSet[j];

				pointB = null;

				//* Find the DTPoint3D corresponding to the collider-room
				for (int k = 0; k < looseCorridorList.Count; k++)
				{
					if (looseCorridorList[k].ROOM.RoomCollider == colliderRoom)
						pointB = looseCorridorList[k];
				}

				if (pointB == null)
					continue;

				//* Connect start-room to new chain-room
				pointA.AddEdge(pointB);
				pointA.AddNeighbour(pointB);

				//* Add new DTEdge to dungeonGraph
				dungeonGraph.Add(new DTEdge3D(pointA, pointB));

				//* Update pointA and assign it to pointB -- "moving the pointer to the next variable"
				pointA = pointB;
			}

			//* Connect last chain-room to neighbour -- chain-link is done! (insertion of overlapping room should be done?)
			pointA.AddEdge(neighbour);
			pointA.AddNeighbour(neighbour);

			//* Add last new DTEdge to dungeonGraph
			dungeonGraph.Add(new DTEdge3D(pointA, neighbour));
		}

		//* De-activate/destroy all rooms which aren't overlapping
		for (int i = createdRooms.Count - 1; i >= 0; i--)
		{
			if (corridorRooms.Contains(createdRooms[i]) || acceptedRooms.Contains(createdRooms[i]))
			{
				if (Editor_VisualizeCorridorRooms && corridorRooms.Contains(createdRooms[i]))
				{
					foreach (Transform child in createdRooms[i].ChildTiles)
					{
						Debug.DrawLine(child.position, child.position + Vector3.down * 150, Color.magenta, 10f);
					}
				}
			}
		}

		for (int j = looseCorridorList.Count - 1; j >= 0; j--)
		{
			DTPoint3D point = looseCorridorList[j];
			bool looseCorridorIsConnected = false;
			for (int i = dungeonGraph.Count - 1; i >= 0; i--)
			{
				DTEdge3D edge = dungeonGraph[i];
				if (edge.START == point || edge.END == point)
				{
					looseCorridorIsConnected = true;
					break;
				}
			}

			if (!looseCorridorIsConnected)
			{
				createdRooms.Remove(looseCorridorList[j].ROOM);
				acceptedRooms.Remove(looseCorridorList[j].ROOM);
				corridorRooms.Remove(looseCorridorList[j].ROOM);
				Destroy(looseCorridorList[j].ROOM.RoomObject.gameObject);
				looseCorridorList.RemoveAt(j);
			}
		}
	}

	//* Using SetPropertyBlock for performance reasons, this method means i directly edit the properties of the actual material instance --
	//* I'm not creating a new material instance at runtime or editing the colour of the material asset itself
	void ChooseStartEndRooms()
	{
		int roomIndex = random.Next(0, acceptedRooms.Count);
		startRoom = acceptedRooms[roomIndex];
		while (acceptedRooms[roomIndex] == startRoom)
		{
			roomIndex = random.Next(0, acceptedRooms.Count);
		}
		endRoom = acceptedRooms[roomIndex];

		//* Check if start & end rooms should not be directly connected (also check and create new connections to other rooms just in case, all rooms gotta be connected somehow)
		if (!AllowDirectPathToExit)
		{
			List<DTEdge3D> startEndConnections = new List<DTEdge3D>();

			DTPoint3D startPoint = null;
			DTPoint3D endPoint = null;

			bool startHasOtherConnection = false, endHasOtherConnection = false;

			for (int i = 0; i < dungeonGraph.Count; i++)
			{
				DTEdge3D current = dungeonGraph[i];

				if (startPoint == null)
				{
					if (current.START.ROOM == startRoom)
					{
						startPoint = current.START;
					}
					else if (current.END.ROOM == startRoom)
					{
						startPoint = current.END;
					}
				}

				if (endPoint == null)
				{
					if (current.START.ROOM == endRoom)
					{
						endPoint = current.START;
					}
					else if (current.END.ROOM == endRoom)
					{
						endPoint = current.END;
					}
				}

				if (current.START.ROOM == startRoom && current.END.ROOM == endRoom || current.START.ROOM == endRoom && current.END.ROOM == startRoom)
				{
					startEndConnections.Add(current);
				}

				if (current.START.ROOM == startRoom && current.END.ROOM != endRoom || current.END.ROOM == startRoom && current.START.ROOM != endRoom)
				{
					startHasOtherConnection = true;
				}

				if (current.START.ROOM == endRoom && current.END.ROOM != startRoom || current.END.ROOM == endRoom && current.START.ROOM != startRoom)
				{
					endHasOtherConnection = true;
				}
			}

			Debug.Log($"Num direct connections between start-end: {startEndConnections.Count}");

			if (startEndConnections.Count > 0)
			{
				for (int i = 0; i < startEndConnections.Count; i++)
				{
					DTEdge3D current = startEndConnections[i];
					Debug.Log("Drawing removed start-end connection");
					dungeonGraph.Remove(current);
				}

				//* If start and/or end rooms have no other connections, create new ones now that the primary start-end connection has been removed
				if (!startHasOtherConnection)
				{
					int randomNeighbourIndex = random.Next(0, dungeonGraph.Count);
					dungeonGraph.Add(new DTEdge3D(startPoint, dungeonGraph[randomNeighbourIndex].START));
				}
				if (!endHasOtherConnection)
				{
					int randomNeighbourIndex = random.Next(0, dungeonGraph.Count);
					dungeonGraph.Add(new DTEdge3D(endPoint, dungeonGraph[randomNeighbourIndex].START));
				}
			}
		}
	}

	//* Flooring the position makes it easier to construct actual corridors & rooms later on, it'll be a tile-like grid
	//* Flooring or Ceiling based on nearest rounding, to make up for collision check sometimes missing
	Vector3Int CeilFloorPosition(Vector3 position)
	{
		return new Vector3Int()
		{
			x = position.x <= 0.5f ? Mathf.FloorToInt(position.x) : Mathf.CeilToInt(position.x),
				y = position.y <= 0.5f ? Mathf.FloorToInt(position.y) : Mathf.CeilToInt(position.y),
				z = position.z <= 0.5f ? Mathf.FloorToInt(position.z) : Mathf.CeilToInt(position.z)
		};
	}

	void CreateGrid()
	{
		//* Build simulated grid
		int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
		for (int i = 0; i < acceptedRooms.Count; i++)
		{
			minX = Mathf.Min(CeilFloorPosition(acceptedRooms[i].RoomCollider.bounds.min).x, minX);

			maxX = Mathf.Max(CeilFloorPosition(acceptedRooms[i].RoomCollider.bounds.max).x, maxX);

			minZ = Mathf.Min(CeilFloorPosition(acceptedRooms[i].RoomCollider.bounds.min).z, minZ);

			maxZ = Mathf.Max(CeilFloorPosition(acceptedRooms[i].RoomCollider.bounds.max).z, maxZ);

			minY = Mathf.Min(CeilFloorPosition(acceptedRooms[i].RoomCollider.bounds.min).y, minY);

			maxY = Mathf.Max(CeilFloorPosition(acceptedRooms[i].RoomCollider.bounds.max).y, maxY);
		}

		for (int i = 0; i < corridorRooms.Count; i++)
		{
			minX = Mathf.Min(CeilFloorPosition(corridorRooms[i].RoomCollider.bounds.min).x, minX);

			maxX = Mathf.Max(CeilFloorPosition(corridorRooms[i].RoomCollider.bounds.max).x, maxX);

			minZ = Mathf.Min(CeilFloorPosition(corridorRooms[i].RoomCollider.bounds.min).z, minZ);

			maxZ = Mathf.Max(CeilFloorPosition(corridorRooms[i].RoomCollider.bounds.max).z, maxZ);

			minY = Mathf.Min(CeilFloorPosition(corridorRooms[i].RoomCollider.bounds.min).y, minY);

			maxY = Mathf.Max(CeilFloorPosition(corridorRooms[i].RoomCollider.bounds.max).y, maxY);
		}

		minDungeonBounds = new Vector3Int(minX, minY, minZ);
		maxDungeonBounds = new Vector3Int(maxX, maxY, maxZ);

		// Debug.DrawRay(minDungeonBounds, new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(maxDungeonBounds, new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(new Vector3(maxDungeonBounds.x, minDungeonBounds.y, minDungeonBounds.z), new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(new Vector3(maxDungeonBounds.x, minDungeonBounds.y, minDungeonBounds.z), new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(new Vector3(maxDungeonBounds.x, minDungeonBounds.y, maxDungeonBounds.z), new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(new Vector3(maxDungeonBounds.x, minDungeonBounds.y, maxDungeonBounds.z), new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(new Vector3(minDungeonBounds.x, minDungeonBounds.y, maxDungeonBounds.z), new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(new Vector3(minDungeonBounds.x, minDungeonBounds.y, maxDungeonBounds.z), new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(new Vector3(minDungeonBounds.x, maxDungeonBounds.y, maxDungeonBounds.z), new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(new Vector3(minDungeonBounds.x, maxDungeonBounds.y, maxDungeonBounds.z), new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(new Vector3(minDungeonBounds.x, maxDungeonBounds.y, minDungeonBounds.z), new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(new Vector3(minDungeonBounds.x, maxDungeonBounds.y, minDungeonBounds.z), new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(new Vector3(maxDungeonBounds.x, maxDungeonBounds.y, minDungeonBounds.z), new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(new Vector3(maxDungeonBounds.x, maxDungeonBounds.y, minDungeonBounds.z), new Vector3(-1, -1, -1), Color.blue, 100f);

		// Debug.DrawRay(maxDungeonBounds, new Vector3(1, 1, 1), Color.blue, 100f);
		// Debug.DrawRay(maxDungeonBounds, new Vector3(-1, -1, -1), Color.blue, 100f);

		aStarGrid = new Dictionary<Vector3Int, AStarNode>();

		for (int i = minX; i < maxX; i++)
		{
			for (int j = minY; j < maxY; j++)
			{
				for (int k = minZ; k < maxZ; k++)
				{
					Vector3Int position = new Vector3Int(i, j, k);
					aStarGrid.Add(position, new AStarNode(position));
					if (Physics.OverlapBox(aStarGrid[position].position, Vector3.one / 3f, Quaternion.identity, LayerMask.GetMask("Room")).Length > 0)
					{
						// aStarGrid[position].isRoom = true;
						aStarGrid[position].dungeonTileType = EDungeonTile.room;
						// Debug.DrawRay(position, new Vector3(.5f, .5f, .5f), Color.blue, 100f);
					}
					else
					{
						// aStarGrid[position].isEmpty = true;
						aStarGrid[position].dungeonTileType = EDungeonTile.none;
						// Debug.DrawRay(position, new Vector3(.5f, .5f, .5f), Color.red, 100f);
					}

					if (Physics.OverlapBox(aStarGrid[position].position, Vector3.one / 3f, Quaternion.identity, LayerMask.GetMask("Corner")).Length > 0)
					{
						// aStarGrid[position].isCorner = true;
						aStarGrid[position].dungeonTileType = EDungeonTile.corner;
						// Debug.DrawRay(position, new Vector3(.5f, .5f, .5f), Color.red, 100f);
					}
					else if (Physics.OverlapBox(aStarGrid[position].position, Vector3.one / 3f, Quaternion.identity, LayerMask.GetMask("Floor")).Length > 0)
					{
						// aStarGrid[position].isFloor = true;
						aStarGrid[position].dungeonTileType = EDungeonTile.floor;
						continue;
					}
					else if (Physics.OverlapBox(aStarGrid[position].position, Vector3.one / 3f, Quaternion.identity, LayerMask.GetMask("Roof")).Length > 0)
					{
						aStarGrid[position].dungeonTileType = EDungeonTile.roof;
						continue;
					}
					else if (Physics.OverlapBox(aStarGrid[position].position, Vector3.one / 3f, Quaternion.identity, LayerMask.GetMask("Wall")).Length > 0)
					{
						aStarGrid[position].dungeonTileType = EDungeonTile.wall;
					}
				}
			}
		}
	}

	IEnumerator BuildCorridors()
	{
		//* Build corridors
		int builderResult = 0;

		WaitUntil waitUntil = new WaitUntil(() => builderResult != 0);

		List<GameObject> stairTiles = new List<GameObject>();
		List<GameObject> stairRoofTiles = new List<GameObject>();
		List<GameObject> stairLeftTiles = new List<GameObject>();
		List<GameObject> stairLeftRoofTiles = new List<GameObject>();
		List<GameObject> stairRightTiles = new List<GameObject>();
		List<GameObject> stairRightRoofTiles = new List<GameObject>();
		List<GameObject> stairMiddleTiles = new List<GameObject>();
		List<GameObject> stairMiddleRoofTiles = new List<GameObject>();
		List<GameObject> stairCornerTiles = new List<GameObject>();
		List<GameObject> corridorTiles = new List<GameObject>();
		List<GameObject> corridorCornerTiles = new List<GameObject>();
		List<GameObject> corridorTWayTiles = new List<GameObject>();
		List<GameObject> corridorFourSectionTiles = new List<GameObject>();
		List<GameObject> doorTiles = new List<GameObject>();

		for (int j = 0; j < DungeonTiles.Length; j++)
		{
			EDungeonTile tileType = DungeonTiles[j].TileType;

			for (int k = 0; k < DungeonTiles[j].TileObjects.Length; k++)
			{
				GameObject tile = DungeonTiles[j].TileObjects[k];

				if (tileType == EDungeonTile.stair)
					stairTiles.Add(tile);
				else if (tileType == EDungeonTile.stairRoof)
					stairRoofTiles.Add(tile);
				else if (tileType == EDungeonTile.stairLeft)
					stairLeftTiles.Add(tile);
				else if (tileType == EDungeonTile.stairLeftRoof)
					stairLeftRoofTiles.Add(tile);
				else if (tileType == EDungeonTile.stairRight)
					stairRightTiles.Add(tile);
				else if (tileType == EDungeonTile.stairRightRoof)
					stairRightRoofTiles.Add(tile);
				else if (tileType == EDungeonTile.stairMiddle)
					stairMiddleTiles.Add(tile);
				else if (tileType == EDungeonTile.stairMiddleRoof)
					stairMiddleRoofTiles.Add(tile);
				else if (tileType == EDungeonTile.stairCorner)
					stairCornerTiles.Add(tile);
				else if (tileType == EDungeonTile.corridor)
					corridorTiles.Add(tile);
				else if (tileType == EDungeonTile.corridorCorner)
					corridorCornerTiles.Add(tile);
				else if (tileType == EDungeonTile.corridorTWay)
					corridorTWayTiles.Add(tile);
				else if (tileType == EDungeonTile.corridorFourSection)
					corridorFourSectionTiles.Add(tile);
				else if (tileType == EDungeonTile.door)
					doorTiles.Add(tile);
			}
		}

		GameObject chosenStairTile = stairTiles[random.Next(0, stairTiles.Count)];
		GameObject chosenStairRoofTile = stairRoofTiles[random.Next(0, stairRoofTiles.Count)];
		GameObject chosenStairLeftTile = stairLeftTiles[random.Next(0, stairLeftTiles.Count)];
		GameObject chosenStairLeftRoofTile = stairLeftRoofTiles[random.Next(0, stairLeftRoofTiles.Count)];
		GameObject chosenStairRightTile = stairRightTiles[random.Next(0, stairRightTiles.Count)];
		GameObject chosenStairRightRoofTile = stairRightRoofTiles[random.Next(0, stairRightRoofTiles.Count)];
		GameObject chosenStairMiddleTile = stairMiddleTiles[random.Next(0, stairMiddleTiles.Count)];
		GameObject chosenStairMiddleRoofTile = stairMiddleRoofTiles[random.Next(0, stairMiddleRoofTiles.Count)];
		GameObject chosenStairCornerTile = stairCornerTiles[random.Next(0, stairCornerTiles.Count)];
		GameObject chosenCorridorTile = corridorTiles[random.Next(0, corridorTiles.Count)];
		GameObject chosenCorridorCornerTile = corridorCornerTiles[random.Next(0, corridorCornerTiles.Count)];
		GameObject chosenCorridorTWayTile = corridorTWayTiles[random.Next(0, corridorTWayTiles.Count)];
		GameObject chosenCorridorFourSectionTile = corridorFourSectionTiles[random.Next(0, corridorFourSectionTiles.Count)];
		GameObject chosenDoorTile = doorTiles[random.Next(0, doorTiles.Count)];

		GameObject GetDungeonTileTypeObject(EDungeonTile tileType)
		{
			switch (tileType)
			{
			case EDungeonTile.corridor:
				return chosenCorridorTile;
			case EDungeonTile.corridorCorner:
				return chosenCorridorCornerTile;
			case EDungeonTile.corridorTWay:
				return chosenCorridorTWayTile;
			case EDungeonTile.corridorFourSection:
				return chosenCorridorFourSectionTile;
			default:
				return null;
			}
		}

		void SwitchPos(Vector3Int pos, Vector3 direction, EDungeonTile newTileType)
		{
			Collider[] hallways = Physics.OverlapBox(pos, Vector3.one * .25f, Quaternion.identity, LayerMask.GetMask("Corridor"));

			for (int i = hallways.Length - 1; i >= 0; i--)
			{
				Destroy(hallways[i].gameObject);
			}

			GameObject newHallway = Instantiate(GetDungeonTileTypeObject(newTileType), pos, Quaternion.LookRotation(direction));
			aStarGrid[pos].dungeonTileType = newTileType;
			newHallway.layer = LayerMask.NameToLayer("Corridor");
		}

		(Transform, EDungeonTile) GetHallwayConfiguration(Vector3Int position)
		{
			Collider[] hallways = Physics.OverlapBox(position, Vector3.one * .25f, Quaternion.identity, LayerMask.GetMask("Corridor"));

			Transform transform = null;
			EDungeonTile tileType = EDungeonTile.none;

			for (int i = 0; i < hallways.Length; i++)
			{
				Transform hallway = hallways[i].transform;

				transform = hallway;
				tileType = aStarGrid[position].dungeonTileType;
			}

			return (transform, tileType);
		}

		bool GridPosIsRoom(Vector3 pos, Vector3 extents) => Physics.CheckBox(pos, extents, Quaternion.identity, LayerMask.GetMask("Room"));
		bool GridPosIsWall(Vector3 pos, Vector3 extents) => Physics.CheckBox(pos, extents, Quaternion.identity, LayerMask.GetMask("Wall"));
		bool CanPlaceDoor(Vector3 currentPos, Vector3 anticipatedEmptyPos, Vector3 extents)
		{
			Collider[] wall = Physics.OverlapBox(currentPos, Vector3.one * .25f, Quaternion.identity, LayerMask.GetMask("Wall", "Floor"));
			Collider[] room = Physics.OverlapBox(anticipatedEmptyPos, Vector3.one * .25f, Quaternion.identity, LayerMask.GetMask("Room"));

			if (wall.Length < 2)
				return false;
			if (wall.Length == 2 && room.Length > 0)
				return false;
			return true;
		}
		bool DoorAlreadyPlaced(Vector3 currentPos, Vector3 anticipatedEmptyPos, Vector3 extents)
		{
			bool doorAtPos = Physics.CheckBox(currentPos, extents, Quaternion.identity, LayerMask.GetMask("Door"));
			bool anticipatedIsEmpty = !Physics.CheckBox(anticipatedEmptyPos, extents, Quaternion.identity, LayerMask.GetMask("Room"));

			return doorAtPos && anticipatedIsEmpty;
		}

		void PlaceDoor(Vector3 pos, Vector3 corridorFacing)
		{
			Vector3 halfSize = Vector3.one * .25f;
			if (!GridPosIsRoom(pos, halfSize) && !GridPosIsWall(pos, halfSize))
			{
				Debug.DrawRay(pos, Vector3.up * 25, Color.red, 5f);
				return;
			}

			Collider[] wall = Physics.OverlapBox(pos, Vector3.one * .25f, Quaternion.identity, LayerMask.GetMask("Wall"));

			Quaternion facing = Quaternion.identity;
			for (int i = wall.Length - 1; i >= 0; i--)
			{
				Quaternion current = wall[i].transform.rotation;

				facing = wall[i].transform.rotation;

				Destroy(wall[i].gameObject);
			}

			GameObject door = Instantiate(chosenDoorTile, pos, facing);
			aStarGrid[new Vector3Int((int) pos.x, (int) pos.y, (int) pos.z)].dungeonTileType = EDungeonTile.door;
			door.layer = LayerMask.NameToLayer("Door");
		}

		int count = dungeonGraph.Count;

		for (int i = 0; i < dungeonGraph.Count; i++)
		{
			DTEdge3D edge = dungeonGraph[i];

			AStarPathBuilder builder = new AStarPathBuilder();
			builderResult = builder.BuildAStarPath(edge.START.ROOM, edge.END.ROOM,
				AStarPathfindingSettings.pathfindingStairCost,
				AStarPathfindingSettings.pathfindingRoomCost,
				AStarPathfindingSettings.pathfindingCorridorCost,
				AStarPathfindingSettings.pathfindingBaseCost,
				aStarGrid, minDungeonBounds, maxDungeonBounds);
			yield return waitUntil;

			if (builderResult == -1)
			{
				Debug.Log($"Pathfinding failed for builder no. {i} of {count - 1}, continuing to next.");
				continue;
			}

			aStarGrid = builder.GRID;

			List<AStarNode> builderPath = builder.FINISHEDPATH;

			Vector3 preDirection = Vector3.zero;

			int doorsPlaced = 0;

			for (int j = 0; j < builderPath.Count; j++)
			{
				//* Reset doorsPlaced counter each time we begin another pathway
				if (j == 0)
					doorsPlaced = 0;

				bool bPosIsRoom = GridPosIsRoom(builderPath[j].position, Vector3.one * .25f);
				bool bPosIsWall = GridPosIsWall(builderPath[j].position, Vector3.one * .25f);

				//* If we're placing the first door, check for empty space in front of door, otherwise check for empty space behind door
				bool bCanPlaceDoor = false;

				//* If there's already a door placed, we don't need to create one, increase the doorsPlaced counter anyway!
				bool bDoorAlreadyPlaced = false;

				if (doorsPlaced == 0 && j < builderPath.Count - 1)
				{
					bCanPlaceDoor = CanPlaceDoor(builderPath[j].position, builderPath[j + 1].position, Vector3.one * .25f);
					bDoorAlreadyPlaced = DoorAlreadyPlaced(builderPath[j].position, builderPath[j + 1].position, Vector3.one * .25f);
				}
				else if (doorsPlaced == 1)
				{
					bCanPlaceDoor = CanPlaceDoor(builderPath[j].position, builderPath[j - 1].position, Vector3.one * .25f);
					bDoorAlreadyPlaced = DoorAlreadyPlaced(builderPath[j].position, builderPath[j - 1].position, Vector3.one * .25f);
				}

				AStarNode current = builderPath[j];
				Vector3 direction = current.facing;

				//* Get the configuration of the current position we're trying to place a new hallway section at, if there is any
				(Transform, EDungeonTile) hallwayConfig = GetHallwayConfiguration(current.position);

				#region For Debugging
				// if (j > 0)
				// {
				// 	if (hallwayConfig.Item1 != null && hallwayConfig.Item2 != EDungeonTile.none)
				// 	{
				// 		Debug.DrawRay(current.position, direction * 5, Color.green, 2f);
				// 		Debug.DrawRay(builderPath[j - 1].position, preDirection * 5, Color.white, 2f);
				// 		Debug.Log($"Dungeon tile type: {(EDungeonTile)hallwayConfig.Item2}");
				// 		Debug.DrawRay(hallwayConfig.Item1.position, hallwayConfig.Item1.forward * 5, Color.red, 2f);
				// 		yield return new WaitForSeconds(2.5f);
				// 	}
				// }
				#endregion

				if (current.delta.y == 0)
				{
					float angle = Vector3.SignedAngle(preDirection, direction, Vector3.up);
					if (preDirection != direction)
					{
						//* If to the left, angle is negative, if to the right, angle is positive
						if (angle < 0) // Corner turning left
						{
							//* Previous points "forward", current is to the left, corridorCorner forward needs to point back towards preDirection
							//* since corners forward and right directions are open, this rotation makes the corners "forward" point into preDirections "forward" (to preDiretions back)
							//* and corners "right" point into currents "forward" which is to the left of previous

							if (!bPosIsRoom)
							{
								if (hallwayConfig.Item1 == null && hallwayConfig.Item2 == EDungeonTile.none)
								{
									GameObject corridor = Instantiate(chosenCorridorCornerTile, current.position, Quaternion.LookRotation(-preDirection));
									aStarGrid[current.position].dungeonTileType = EDungeonTile.corridorCorner;
									corridor.layer = LayerMask.NameToLayer("Corridor");
								}
								else
								{
									if (hallwayConfig.Item2 == EDungeonTile.corridor)
									{
										//* Make sure corridor forward is different from preDirection, or rather make sure corridor forward is orthogonal to preDirection
										int dotResult = (int) Vector3.Dot(hallwayConfig.Item1.forward, preDirection);

										if (dotResult == 0)
										{
											//* Create T-Way, orientation towards current back should solve things
											SwitchPos(current.position, -preDirection, EDungeonTile.corridorTWay);
										}
										else
										{
											// We have entered a straight corridor and are about to turn left trying to exit it's closed side, create T-Way pointing
											// to currents direction
											SwitchPos(current.position, direction, EDungeonTile.corridorTWay);
										}
									}
									else if (hallwayConfig.Item2 == EDungeonTile.corridorCorner)
									{
										//* Create T-Way, finding proper orientation would be trickier

										//* Get corridorcorner forward
										Vector3 forward = hallwayConfig.Item1.forward;

										//* Find corridorcorner right
										Vector3 right = hallwayConfig.Item1.right;

										int preDotResult = (int) Vector3.Dot(hallwayConfig.Item1.forward, preDirection);
										int dotResultTurn = (int) Vector3.Dot(hallwayConfig.Item1.right, preDirection);

										// If coming in from corners back, create Four-Way since we're coming out it's closed left
										if (preDotResult == 1)
										{
											SwitchPos(current.position, direction, EDungeonTile.corridorFourSection);
										}
										else if (preDotResult == 0)
										{
											// If coming in from corners left, create T-Way pointing to corners forward since we're coming out it's open forward
											if (dotResultTurn == 1)
											{
												SwitchPos(current.position, forward, EDungeonTile.corridorTWay);
											}
											// If coming in from corners right, create T-Way pointing to corners right since we're coming out it's back
											else if (dotResultTurn == -1)
											{
												SwitchPos(current.position, right, EDungeonTile.corridorTWay);
											}
										}
									}
									else if (hallwayConfig.Item2 == EDungeonTile.corridorTWay)
									{
										//* Create Four-Way, make sure we're coming in from the back of it, otherwise we can just go past it
										// but, if we're coming in from the right of it and are continuing left (through T-Ways back), also place a Four-Way
										Vector3 forward = hallwayConfig.Item1.forward;

										//* Use Dot-product to check for same direction
										int dotResult = (int) Vector3.Dot(forward, direction);
										int preDotResult = (int) Vector3.Dot(forward, preDirection);
										if (preDotResult == 1) // coming in from the back
										{
											SwitchPos(current.position, preDirection, EDungeonTile.corridorFourSection);
										}
										else if (preDotResult == 0 && dotResult == -1)
										// coming in from the left and making a right turn out it's back, create Four-Section
										{
											SwitchPos(current.position, direction, EDungeonTile.corridorFourSection);
										}
									}
									//* If Four-Way, skip. Would maybe need to skip stairs but that's gonna require more model-setup
								}
							}

							if (bCanPlaceDoor)
							{
								PlaceDoor(current.position, -preDirection);
								doorsPlaced++;
							}
							else if (bDoorAlreadyPlaced)
							{
								doorsPlaced++;
							}
						}
						else // Corner turning right
						{
							//* Previous points "forward", current is to the right, make corners "forward" face current "forward" which is to the right, 
							//* aligns the corners open "forward" and "right" directions to currents forward and previous forward
							if (!bPosIsRoom)
							{
								if (hallwayConfig.Item1 == null && hallwayConfig.Item2 == EDungeonTile.none)
								{
									GameObject corridor = Instantiate(chosenCorridorCornerTile, current.position, Quaternion.LookRotation(direction));
									aStarGrid[current.position].dungeonTileType = EDungeonTile.corridorCorner;
									corridor.layer = LayerMask.NameToLayer("Corridor");
								}
								else
								{
									if (hallwayConfig.Item2 == EDungeonTile.corridor)
									{
										//* Make sure corridor forward is different from preDirection, or rather make sure corridor forward is orthogonal to preDirection
										int dotResult = (int) Vector3.Dot(hallwayConfig.Item1.forward, preDirection);

										if (dotResult == 0)
										{
											//* Create T-Way, orientation towards current back should solve things
											SwitchPos(current.position, -preDirection, EDungeonTile.corridorTWay);
										}
										else
										{
											// We're about to exit through a straight corridors closed side, create T-Way pointing in currents direction
											SwitchPos(current.position, direction, EDungeonTile.corridorTWay);
										}
									}
									else if (hallwayConfig.Item2 == EDungeonTile.corridorCorner)
									{
										//* Create T-Way, finding proper orientation would be trickier

										//* Get corridorcorner forward
										Vector3 forward = hallwayConfig.Item1.forward;

										//* Find corridorcorner right
										Vector3 right = hallwayConfig.Item1.right;

										int preDotResult = (int) Vector3.Dot(hallwayConfig.Item1.forward, preDirection);
										int dotResultTurn = (int) Vector3.Dot(hallwayConfig.Item1.right, preDirection);

										// If coming in from corners back, create T-Way since we're coming out it's open right
										if (preDotResult == 1)
										{
											SwitchPos(current.position, direction, EDungeonTile.corridorTWay);
										}
										else if (preDotResult == 0)
										{
											// If coming in from corners left, create Four-Section since we're coming out it's closed back
											if (dotResultTurn == 1)
											{
												SwitchPos(current.position, forward, EDungeonTile.corridorFourSection);
											}
										}
										else if (preDotResult == -1)
										{
											// If coming in from corners front, create T-Way pointing to corners forward since we're coming out it's closed left
											SwitchPos(current.position, forward, EDungeonTile.corridorTWay);
										}
									}
									else if (hallwayConfig.Item2 == EDungeonTile.corridorTWay)
									{
										//* Create Four-Way, make sure we're coming in from the back of it, otherwise we can just go past it
										// but, if we're coming in from the left of it and are continuing right (through T-Ways back), also place a Four-Way
										Vector3 forward = hallwayConfig.Item1.forward;

										//* Use Dot-product to check for same direction
										int dotResult = (int) Vector3.Dot(forward, direction);
										int preDotResult = (int) Vector3.Dot(forward, preDirection);
										if (preDotResult == 1) // coming in from the back
										{
											SwitchPos(current.position, preDirection, EDungeonTile.corridorFourSection);
										}
										else if (preDotResult == 0 && dotResult == -1)
										// coming in from the left and making a right turn out it's back, create Four-Section
										{
											SwitchPos(current.position, direction, EDungeonTile.corridorFourSection);
										}
									}
									//* If Four-Way, skip. Would maybe need to skip stairs but that's gonna require more model-setup
								}
							}

							if (bCanPlaceDoor)
							{
								PlaceDoor(current.position, direction);
								doorsPlaced++;
							}
							else if (bDoorAlreadyPlaced)
							{
								doorsPlaced++;
							}
						}
					}
					else // Going straight
					{
						if (!bPosIsRoom)
						{
							if (hallwayConfig.Item1 == null && hallwayConfig.Item2 == EDungeonTile.none)
							{
								GameObject corridor = Instantiate(chosenCorridorTile, current.position, Quaternion.LookRotation(direction));
								aStarGrid[current.position].dungeonTileType = EDungeonTile.corridor;
								corridor.layer = LayerMask.NameToLayer("Corridor");
							}
							else
							{
								if (hallwayConfig.Item2 == EDungeonTile.corridor)
								{
									//* Make sure corridor forward is different from preDirection, or rather make sure corridor forward is orthogonal to preDirection
									int dotResult = (int) Vector3.Dot(hallwayConfig.Item1.forward, preDirection);

									if (dotResult == 0)
									{
										// Create Four-Section, if we're coming in from the side of a corridor and are gonna go staight with another corridor,
										// then we're gonna go past it and should create a Four-Section
										SwitchPos(current.position, direction, EDungeonTile.corridorFourSection);
									}
								}
								else if (hallwayConfig.Item2 == EDungeonTile.corridorCorner) // (T-Way galore!)
								{
									//* Create T-Way, finding proper orientation would be trickier

									//* Get corridorcorner forward
									Vector3 forward = hallwayConfig.Item1.forward;

									//* Find corridorcorner right
									Vector3 right = hallwayConfig.Item1.right;

									int preDotResult = (int) Vector3.Dot(hallwayConfig.Item1.forward, preDirection);
									int dotResultTurn = (int) Vector3.Dot(hallwayConfig.Item1.right, preDirection);

									// If coming in from corners back or front, create T-Way pointing to corners right since we're continuing
									// either straight through it's open front or straight through it's closed back
									if (preDotResult != 0)
									{
										SwitchPos(current.position, right, EDungeonTile.corridorTWay);
									}
									else if (preDotResult == 0) // If coming in from a side
									{
										// If coming in from corners left, create T-Way pointing to corners forward since we're continuing straight out it's open right
										if (dotResultTurn == 1)
										{
											SwitchPos(current.position, forward, EDungeonTile.corridorTWay);
										}
										// If coming in from corners right, create T-Way pointing to corners forward since we're contining straight out it's closed left
										else if (dotResultTurn == -1)
										{
											SwitchPos(current.position, forward, EDungeonTile.corridorTWay);
										}
									}
								}
								else if (hallwayConfig.Item2 == EDungeonTile.corridorTWay)
								{
									//* Create Four-Way, make sure we're coming in from the back of it, otherwise we can just go past it
									// but, if we're coming in from the front of it and are continuing forwards (through T-Ways back), also place a Four-Way
									Vector3 forward = hallwayConfig.Item1.forward;

									//* Use Dot-product to check for same direction
									int dotResult = (int) Vector3.Dot(forward, preDirection);
									if (dotResult == 1) // coming in from the back
									{
										SwitchPos(current.position, direction, EDungeonTile.corridorFourSection);
									}
									else if (dotResult == -1) // coming in from the front, switch because we're continuing straight forwards
									{
										SwitchPos(current.position, direction, EDungeonTile.corridorFourSection);
									}
								}
								//* If Four-Way, skip. Would maybe need to skip stairs but that's gonna require more model-setup
							}
						}

						if (bCanPlaceDoor)
						{
							PlaceDoor(current.position, direction);
							doorsPlaced++;
						}
						else if (bDoorAlreadyPlaced)
						{
							doorsPlaced++;
						}
					}
				}

				if (j > 0 && current.delta.y != 0)
				{
					Vector3 delta = current.delta;

					bool bIsGoingUp = delta.y > 0;

					direction = bIsGoingUp ? direction : -direction;

					if (bIsGoingUp)
					{
						if (!bPosIsRoom)
						{
							GameObject stair = Instantiate(chosenStairTile, current.position, Quaternion.LookRotation(direction));
							aStarGrid[current.position].dungeonTileType = EDungeonTile.stair;
							stair.layer = LayerMask.NameToLayer("Stair");
							GameObject stairRoof = Instantiate(chosenStairRoofTile, current.position + Vector3Int.up, Quaternion.LookRotation(direction));
							aStarGrid[current.position + Vector3Int.up].dungeonTileType = EDungeonTile.stair;
							stairRoof.layer = LayerMask.NameToLayer("Stair");
						}

						if (bCanPlaceDoor)
						{
							PlaceDoor(current.position, direction);
							doorsPlaced++;
						}
						else if (bDoorAlreadyPlaced)
						{
							doorsPlaced++;
						}
					}
					else
					{
						if (!bPosIsRoom)
						{
							GameObject stair = Instantiate(chosenStairRoofTile, current.position - direction, Quaternion.LookRotation(direction));
							aStarGrid[current.position - new Vector3Int((int) direction.x, (int) direction.y, (int) direction.z)].dungeonTileType = EDungeonTile.stair;
							stair.layer = LayerMask.NameToLayer("Stair");
							GameObject stairRoof = Instantiate(chosenStairTile, current.position - direction - Vector3Int.up, Quaternion.LookRotation(direction));
							aStarGrid[current.position - new Vector3Int((int) direction.x, (int) direction.y, (int) direction.z) - Vector3Int.up].dungeonTileType = EDungeonTile.stair;
							stairRoof.layer = LayerMask.NameToLayer("Stair");
						}

						//* Invert direction again in case it needed to be reversed for down-stairs -- ensures setting preDirection doesn't get screwed up for corridors/corners
						direction = -direction;

						if (bCanPlaceDoor)
						{
							PlaceDoor(current.position, direction);
							doorsPlaced++;
						}
						else if (bDoorAlreadyPlaced)
						{
							doorsPlaced++;
						}
					}
				}

				preDirection = direction;
			}

			if (doorsPlaced < 2)
			{
				Debug.Log($"Builder {i} of {count} placed {doorsPlaced} doors");
				for (int j = 0; j < builderPath.Count - 1; j++)
				{
					AStarNode first = builderPath[j];

					AStarNode next = builderPath[j + 1];

					if (j == 0)
						Debug.DrawLine(first.position, next.position, Color.green, 2.5f);
					else
						Debug.DrawLine(first.position, next.position, Color.red, 2.5f);
				}
				yield return new WaitForSeconds(3f);
			}
		}

		//* Disable containing colliders for each room, not needed anymore because the algorithm is done!!
		foreach (Room room in createdRooms)
		{
			room.RoomCollider.enabled = false;
		}

		Debug.Log("All done!");

		DUNGEONDONE = true;
	}
}

[System.Serializable]
public class DungeonTile
{
	public EDungeonTile TileType;
	public GameObject[] TileObjects;
}

public enum EDungeonTile
{
	floor,
	wall,
	corner,
	cornerBottom,
	cornerTop,
	roof,
	stair,
	stairRoof,
	stairMiddle,
	stairLeft,
	stairRight,
	stairMiddleRoof,
	stairLeftRoof,
	stairRightRoof,
	stairCorner,
	corridor,
	corridorCorner,
	corridorTWay,
	corridorFourSection,
	room,
	door,
	none
}

[System.Serializable]
public class AStarPathfindingSettings
{
	public int pathfindingStairCost = 2;
	public int pathfindingCorridorCost = 1;
	public int pathfindingRoomCost = 3;
	public int pathfindingBaseCost = 100;
	public int pathfindStepSize = 1;
}