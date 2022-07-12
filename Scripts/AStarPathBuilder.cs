using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AStarPathBuilder
{
	public Dictionary<Vector3Int, AStarNode> GRID { get { return grid; } }

	public List<AStarNode> FINISHEDPATH { get; private set; }

	public Vector3Int[] OFFSETS { get; private set; }

	private int stairCost, roomCost, corridorCost, baseCost;

	private Vector3Int min, max;

	private Dictionary<Vector3Int, AStarNode> grid;
	private List<AStarNode> openSet = new List<AStarNode>();
	private HashSet<AStarNode> closedSet = new HashSet<AStarNode>();

	public int BuildAStarPath(Room startRoom, Room endRoom, int stairCost, int roomCost, int corridorCost, int baseCost,
		Dictionary<Vector3Int, AStarNode> grid, Vector3Int min, Vector3Int max)
	{
		this.stairCost = stairCost;
		this.roomCost = roomCost;
		this.corridorCost = corridorCost;
		this.baseCost = baseCost;
		this.grid = grid;
		this.min = min;
		this.max = max;
		OFFSETS = new Vector3Int[]
		{
			new Vector3Int(-1, 0, 0),
				new Vector3Int(1, 0, 0),

				new Vector3Int(0, 0, -1),
				new Vector3Int(0, 0, 1),

				new Vector3Int(-3, 1, 0),
				new Vector3Int(-3, -1, 0),
				new Vector3Int(3, 1, 0),
				new Vector3Int(3, -1, 0),

				new Vector3Int(0, 1, -3),
				new Vector3Int(0, -1, -3),
				new Vector3Int(0, 1, 3),
				new Vector3Int(0, -1, -3)
		};

		grid.TryGetValue(Vector3Int.RoundToInt(startRoom.RoomObject.position), out AStarNode startNode);
		AStarNode start = startNode;
		grid.TryGetValue(Vector3Int.RoundToInt(endRoom.RoomObject.position), out AStarNode endNode);
		AStarNode end = endNode;
		List<AStarNode> path = FindPath(start, end);
		if (path != null && path.Count > 0)
		{
			BuildPath(path);
			return 1;
		}
		return -1;
	}

	List<AStarNode> FindPath(AStarNode start, AStarNode goal)
	{
		openSet.Add(start);

		while (openSet.Count > 0)
		{
			AStarNode current = openSet[0];
			for (int i = 0; i < openSet.Count; i++)
			{
				if (openSet[i].totalDistFCost < current.totalDistFCost
					|| openSet[i].totalDistFCost == current.totalDistFCost && openSet[i].distFromGoalHCost < current.distFromGoalHCost)
				{
					current = openSet[i];
				}
			}

			openSet.Remove(current);
			closedSet.Add(current);

			if (current.position == goal.position)
			{
				return ReconstructPath(start, current);
			}

			foreach (Vector3Int offset in OFFSETS)
			{
				//* Check if position is inside bounds, continue if not
				if (!InsideBoundary(current.position + offset))
				{
					continue;
				}

				if (grid.TryGetValue(current.position + offset, out AStarNode neighbour))
				{
					if (closedSet.Contains(neighbour) || current.previousSet.Contains(neighbour.position))
					{
						continue;
					}

					PathTraverseCost traverseCost = GetHeuristic(current, neighbour, goal);

					if (!traverseCost.isTraversable)
					{
						continue;
					}

					if (traverseCost.isStairs) //* If pathCost is stairs
					{
						int xDir = Mathf.Clamp(offset.x, -1, 1);
						int zDir = Mathf.Clamp(offset.z, -1, 1);

						Vector3Int verticalOffset = new Vector3Int(0, offset.y, 0);
						Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

						if (current.previousSet.Contains(current.position + horizontalOffset)
							|| current.previousSet.Contains(current.position + horizontalOffset * 2)
							|| current.previousSet.Contains(current.position + verticalOffset + horizontalOffset)
							|| current.previousSet.Contains(current.position + verticalOffset + horizontalOffset * 2))
						{
							continue;
						}
					}

					int newCost = (int) (current.distFromStartGCost + traverseCost.cost);
					if (newCost < neighbour.distFromStartGCost || !openSet.Contains(neighbour))
					{
						neighbour.distFromStartGCost = newCost;
						neighbour.distFromGoalHCost = (int) traverseCost.cost;
						neighbour.previous = current;

						openSet.Add(neighbour);

						neighbour.previousSet.Clear();
						neighbour.previousSet.UnionWith(current.previousSet);
						neighbour.previousSet.Add(current.position);

						if (traverseCost.isStairs) //* If pathCost is stairs
						{
							int xDir = Mathf.Clamp(offset.x, -1, 1);
							int zDir = Mathf.Clamp(offset.z, -1, 1);

							Vector3Int verticalOffset = new Vector3Int(0, offset.y, 0);
							Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

							neighbour.previousSet.Add(current.position + horizontalOffset);
							neighbour.previousSet.Add(current.position + horizontalOffset * 2);
							neighbour.previousSet.Add(current.position + verticalOffset + horizontalOffset);
							neighbour.previousSet.Add(current.position + verticalOffset + horizontalOffset * 2);
						}
					}
				}
			}
		}

		return null;
	}

	List<AStarNode> ReconstructPath(AStarNode start, AStarNode current)
	{
		List<AStarNode> path = new List<AStarNode>();
		AStarNode node = current;

		while (node != start)
		{
			// Debug.DrawLine(node.position, node.position + Vector3Int.up * 50, Color.yellow, .1f);
			path.Add(node);
			node = node.previous;
		}

		return path;
	}

	struct PathTraverseCost
	{
		public PathTraverseCost(bool traversable, bool stairs, float cost)
		{
			isTraversable = traversable;
			isStairs = stairs;
			this.cost = cost;
		}

		public bool isTraversable, isStairs;
		public float cost;
	}

	PathTraverseCost GetHeuristic(AStarNode current, AStarNode neighbour, AStarNode goal)
	{
		Vector3Int delta = neighbour.position - current.position;

		float cost = 0;
		bool isTraversable = false;
		bool isStairs = false;

		// Check if current is a room-corner or neighbour is a room-corner
		if (current.dungeonTileType == EDungeonTile.corner || neighbour.dungeonTileType == EDungeonTile.corner)
			return new PathTraverseCost(isTraversable, isStairs, Mathf.Infinity);

		//* Flat hallway
		if (delta.y == 0)
		{
			cost = Vector3Int.Distance(neighbour.position, goal.position); //* Heuristic

			switch (neighbour.dungeonTileType)
			{
			case EDungeonTile.stair:
				cost += stairCost;
				isStairs = true;
				return new PathTraverseCost(isTraversable, isStairs, cost);
				// case EDungeonTile.corner:
				// 	cost = Mathf.Infinity;
				// 	return new PathTraverseCost(isTraversable, isStairs, cost);
			case EDungeonTile.floor:
				cost += roomCost;
				break;
			case EDungeonTile.wall:
				cost += roomCost;
				break;
			case EDungeonTile.room:
				cost += roomCost;
				break;
			case EDungeonTile.roof:
				cost = Mathf.Infinity;
				return new PathTraverseCost(isTraversable, isStairs, cost);
			case EDungeonTile.corridor:
				cost += corridorCost;
				break;
			case EDungeonTile.corridorCorner:
				cost += corridorCost;
				break;
			case EDungeonTile.corridorTWay:
				cost += corridorCost;
				break;
			case EDungeonTile.corridorFourSection:
				cost += corridorCost;
				break;
			}

			isTraversable = true;
		}
		//* Staircase
		else
		{
			// bool stair = !current.isCorridor && !current.isEmpty && !current.isRoom || !neighbour.isCorridor && !neighbour.isEmpty && !neighbour.isRoom;

			#region Alternative Exhaustive Stair Check
			// bool stair = current.dungeonTileType != EDungeonTile.corridor && current.dungeonTileType != EDungeonTile.corridorCorner
			// 	&& current.dungeonTileType != EDungeonTile.corridorTWay && current.dungeonTileType != EDungeonTile.corridorFourSection
			// 	&& current.dungeonTileType != EDungeonTile.none && current.dungeonTileType != EDungeonTile.room
			// 	&& current.dungeonTileType != EDungeonTile.floor && current.dungeonTileType != EDungeonTile.roof
			// 	&& current.dungeonTileType != EDungeonTile.corner && current.dungeonTileType != EDungeonTile.cornerBottom
			// 	&& current.dungeonTileType != EDungeonTile.cornerTop && current.dungeonTileType != EDungeonTile.wall
			// 	&& current.dungeonTileType != EDungeonTile.door
			// 	|| neighbour.dungeonTileType != EDungeonTile.corridor && neighbour.dungeonTileType != EDungeonTile.corridorCorner
			// 	&& neighbour.dungeonTileType != EDungeonTile.corridorTWay && neighbour.dungeonTileType != EDungeonTile.corridorFourSection
			// 	&& neighbour.dungeonTileType != EDungeonTile.none && neighbour.dungeonTileType != EDungeonTile.room
			// 	&& neighbour.dungeonTileType != EDungeonTile.floor && neighbour.dungeonTileType != EDungeonTile.roof
			// 	&& neighbour.dungeonTileType != EDungeonTile.corner && neighbour.dungeonTileType != EDungeonTile.cornerBottom
			// 	&& neighbour.dungeonTileType != EDungeonTile.cornerTop && neighbour.dungeonTileType != EDungeonTile.wall
			// 	&& neighbour.dungeonTileType != EDungeonTile.door;
			#endregion

			bool stair = current.dungeonTileType == EDungeonTile.stair || neighbour.dungeonTileType == EDungeonTile.stair;

			// If either current or neighbour already is a stair, don't create a new stair cross-sectioning this current stair
			if (stair)
			{
				cost += stairCost;
				return new PathTraverseCost(isTraversable, isStairs, cost);
			}

			// There's no stair at current or neighbour currently, we can possibly create a new stair
			cost = baseCost + Vector3Int.Distance(neighbour.position, goal.position); //* Base cost + heuristic

			int xDir = Mathf.Clamp(delta.x, -1, 1);
			int zDir = Mathf.Clamp(delta.z, -1, 1);

			Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
			Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

			//* Check if position isn't in bounds & return current cost if it isn't
			if (!InsideBoundary(current.position + verticalOffset)
				|| !InsideBoundary(current.position + horizontalOffset)
				|| !InsideBoundary(current.position + verticalOffset + horizontalOffset))
			{
				return new PathTraverseCost(isTraversable, isStairs, cost);
			}

			grid.TryGetValue(current.position + horizontalOffset, out AStarNode one);
			grid.TryGetValue(current.position + horizontalOffset * 2, out AStarNode two);
			grid.TryGetValue(current.position + verticalOffset + horizontalOffset, out AStarNode three);
			grid.TryGetValue(current.position + verticalOffset + horizontalOffset * 2, out AStarNode four);

			// // If one of these positions between current and neighbour is a room-corner, don't create stair
			// if (one.dungeonTileType == EDungeonTile.corner || two.dungeonTileType == EDungeonTile.corner || three.dungeonTileType == EDungeonTile.corner
			// 	|| four.dungeonTileType == EDungeonTile.corner)
			// {
			// 	cost = Mathf.Infinity;
			// 	return new PathTraverseCost(isTraversable, isStairs, cost);
			// }

			// If there's already something other then a corner in the way between current and neighbour position, don't create a new stair
			if (one.dungeonTileType != EDungeonTile.none || two.dungeonTileType != EDungeonTile.none || three.dungeonTileType != EDungeonTile.none
				|| four.dungeonTileType != EDungeonTile.none)
			{
				return new PathTraverseCost(isTraversable, isStairs, cost);
			}

			isTraversable = true;
			isStairs = true;
		}

		return new PathTraverseCost(isTraversable, isStairs, cost);
	}

	void BuildPath(List<AStarNode> path)
	{
		if (path == null)
		{
			Debug.LogWarning("Path is null!");
			return;
		}

		FINISHEDPATH = new List<AStarNode>();

		for (int i = 0; i < path.Count; i++)
		{
			AStarNode current = path[i];

			if (current.dungeonTileType == EDungeonTile.none)
			{
				//* Place hallway
				current.dungeonTileType = EDungeonTile.corridor;
			}

			if (i > 0)
			{
				AStarNode previous = path[i - 1];
				Vector3Int delta = current.position - previous.position;

				previous.facing = Vector3.ProjectOnPlane(delta / (int) delta.magnitude, Vector3.up);
				if (delta.y != 0)
				{
					int xDir = Mathf.Clamp(delta.x, -1, 1);
					int zDir = Mathf.Clamp(delta.z, -1, 1);
					Vector3Int verticalOffset = new Vector3Int(0, delta.y, 0);
					Vector3Int horizontalOffset = new Vector3Int(xDir, 0, zDir);

					grid.TryGetValue(previous.position + horizontalOffset, out AStarNode one);
					grid.TryGetValue(previous.position + horizontalOffset * 2, out AStarNode two);
					grid.TryGetValue(previous.position + verticalOffset + horizontalOffset, out AStarNode three);
					grid.TryGetValue(previous.position + verticalOffset + horizontalOffset * 2, out AStarNode four);

					one.delta = delta;
					one.dungeonTileType = EDungeonTile.stair;
					one.facing = previous.facing;
					one.delta = delta;
					two.dungeonTileType = EDungeonTile.stair;
					two.facing = previous.facing;
					two.delta = delta;
					three.dungeonTileType = EDungeonTile.stair;
					three.facing = previous.facing;
					three.delta = delta;
					four.dungeonTileType = EDungeonTile.stair;
					four.facing = previous.facing;
					four.delta = delta;

					FINISHEDPATH.Add(one);
					FINISHEDPATH.Add(current);
				}
				else
				{
					FINISHEDPATH.Add(current);
				}
			}
			else
			{
				FINISHEDPATH.Add(current);
			}
		}

		foreach (var pair in grid.Values)
		{
			pair.previous = null;
			pair.previousSet.Clear();
			pair.cost = 0;
			pair.distFromGoalHCost = 0;
			pair.distFromStartGCost = 0;
		}
	}

	bool InsideBoundary(Vector3Int position)
	{
		return position.x < max.x && position.x > min.x
			&& position.y < max.y && position.y > min.y
			&& position.z < max.z && position.z > min.z;
	}
}

public class AStarNode
{
	public AStarNode previous;
	public Vector3Int position;
	public float cost;
	public int distFromStartGCost, distFromGoalHCost;
	public HashSet<Vector3Int> previousSet = new HashSet<Vector3Int>();
	public bool isStair, isRoom, isCorridor, isEmpty, isTraversable, isFloor, isCorner;
	public EDungeonTile dungeonTileType;
	public Vector3 facing, delta;

	public int totalDistFCost { get { return distFromStartGCost + distFromGoalHCost; } }

	public AStarNode(Vector3Int position)
	{
		this.position = position;
	}
}