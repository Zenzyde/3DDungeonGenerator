using System.Collections;
using System.Collections.Generic;
//using System.Linq;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class DelaunayTriangulate3D
{
	private DTPoint3D[] points;
	private DTTetrahedron root;
	private Vector3Int centerPoint;
	private float superTetrahedronRadius;
	private List<DTTetrahedron> tetrahedronList = new List<DTTetrahedron>();

	private const float DebugTimeWindow = 20f;

	private float StartTime;

	private List < (DTPoint3D, DTPoint3D, DTPoint3D, DTPoint3D) > tetrahedronPermutations = new List < (DTPoint3D, DTPoint3D, DTPoint3D, DTPoint3D) > ();

	public DelaunayTriangulate3D(DTPoint3D[] points, Vector3Int centerPoint, float superTetrahedronRadius)
	{
		this.points = points;
		this.centerPoint = centerPoint;
		this.superTetrahedronRadius = superTetrahedronRadius;
	}

	//* Start with inserting one point, triangulate that and then insert another point
	public async Task<DTPoint3D[]> Triangulate()
	{
		StartTime = Time.timeSinceLevelLoad;

		//* Create supertriangle/root
		root = GetSuperPoints();

		//* Store and iteratively insert points later
		Stack<DTPoint3D> pointsToInsert = new Stack<DTPoint3D>();
		for (int i = 0; i < points.Length; i++)
		{
			pointsToInsert.Push(points[i]);
		}

		//* Go through and iteratively triangulate individual points
		while (pointsToInsert.Count > 0)
		{
			Debug.Log($"Points to insert: {pointsToInsert.Count - 1}");
			DTPoint3D currentPoint = pointsToInsert.Pop();
			await UpdateTetrahedronsEncompassingPoint(currentPoint);
		}

		//* Create edges from remaining tetrahedrons
		await CreateNeighbourConnections();

		Debug.Log($"Total time: {Time.timeSinceLevelLoad - StartTime} seconds");

		return points;
	}

	DTTetrahedron GetSuperPoints()
	{
		DTPoint3D[] outerPoints = new DTPoint3D[4];

		Vector3 point = centerPoint + Vector3.up * superTetrahedronRadius;
		outerPoints[0] = new DTPoint3D(point, -1, null);

		// Debug.DrawLine(centerPoint, outerPoints[0].POSITION, Color.red, Time.time + 10f);
		Vector3 forward;

		//* Could've use cosine & sine but oh well this works so whatevs...
		for (int i = 0; i < 3; i++)
		{
			Quaternion lookRotation = Quaternion.Euler(200, 120 * i, 0);
			forward = lookRotation * Vector3.one;

			point = centerPoint + forward * superTetrahedronRadius;
			outerPoints[1 + i] = new DTPoint3D(point, -2 - i, null);

			// Debug.DrawLine(centerPoint, outerPoints[1 + i].POSITION, Color.red, Time.time + DebugTimeWindow);
		}

		for (int i = 0; i < outerPoints.Length; i++)
		{
			DTPoint3D firstPoint = outerPoints[i];
			for (int j = (i + 1) % outerPoints.Length; j != i; j = (j + 1) % outerPoints.Length)
			{
				DTPoint3D otherPoint = outerPoints[j];

				otherPoint.AddNeighbour(firstPoint);
				otherPoint.AddEdge(firstPoint);

				// Debug.DrawLine(firstPoint.POSITION, otherPoint.POSITION, Color.green, Time.time + DebugTimeWindow);
			}
		}

		// return new DTTetrahedron(outerPoints);

		DTTetrahedron rootTetra = new DTTetrahedron(outerPoints);

		tetrahedronList.Add(rootTetra);

		return rootTetra;
	}

	async Task CreateNeighbourConnections()
	{
		await Task.Run(() =>
		{
			// Debug.Log("Creating neighbour connections");
			for (int i = tetrahedronList.Count - 1; i >= 0; i--)
			{
				// Debug.Log($"Tetrahedrons left to search: {i}");

				DTTetrahedron tetrahedron = tetrahedronList[i];
				bool deleteTetrahedron = false;

				// Debug.Log("Searching corners for root match, tetrahedron is not a permutation");

				for (int j = 0; j < tetrahedron.CORNERS.Length; j++)
				{
					if (deleteTetrahedron)
						break;

					DTPoint3D point = tetrahedron.CORNERS[j];
					for (int k = 0; k < root.CORNERS.Length; k++)
					{
						DTPoint3D corner = root.CORNERS[k];
						if (point == corner)
						{
							// Debug.Log("Root match found, exiting and marking for deletion");
							deleteTetrahedron = true;
							break;
						}
					}
				}

				if (deleteTetrahedron)
				{
					// Debug.Log("Root match or permutation found, deleting tetrahedron");
					tetrahedronList.Remove(tetrahedron);
					continue;
				}

				// Debug.Log("No match or permutation found, creating connections for corners of tetrahedron");
				DTPoint3D firstPoint = tetrahedron.CORNERS[0];
				DTPoint3D secondPoint = tetrahedron.CORNERS[1];
				DTPoint3D thirdPoint = tetrahedron.CORNERS[2];
				DTPoint3D fourthPoint = tetrahedron.CORNERS[3];

				firstPoint.AddEdge(secondPoint);
				firstPoint.AddNeighbour(secondPoint);

				firstPoint.AddEdge(thirdPoint);
				firstPoint.AddNeighbour(thirdPoint);

				firstPoint.AddEdge(fourthPoint);
				firstPoint.AddNeighbour(fourthPoint);

				secondPoint.AddEdge(thirdPoint);
				secondPoint.AddNeighbour(thirdPoint);

				thirdPoint.AddEdge(fourthPoint);
				thirdPoint.AddNeighbour(fourthPoint);

				fourthPoint.AddEdge(secondPoint);
				fourthPoint.AddNeighbour(secondPoint);
			}
		});
	}

	async Task UpdateTetrahedronsEncompassingPoint(DTPoint3D point)
	{
		List<DTTetrahedron> tetrahedronsToEdit = new List<DTTetrahedron>();

		for (int i = 0; i < tetrahedronList.Count; i++)
		{
			DTTetrahedron tetrahedron = tetrahedronList[i];
			int index = CheckCircumSphere(tetrahedron, point);

			//* Doesn't contain point -- no need to update anything?
			if (index == -10)
			{
				continue;
			}
			//* Contains point -- save to local tetrahedron list for deleting edges and creating new tetrahedrons below
			else
			{
				tetrahedronsToEdit.Add(tetrahedron);
			}
		}

		if (tetrahedronsToEdit.Count == 0)
		{
			Debug.Log("No tetrahedron containing point -- impossible!!");
			return;
		}

		//* Iterate and edit any tetrahedron that contained the point
		if (tetrahedronsToEdit.Count == 1)
		{
			//* If there's only one tetrahedron containing the point
			//* Assign old tetrahedron for deletion whether or not the resulting tetrahedrons are permutations
			//* -- if the resulting tetrahedrons are permutations then the old tetrahedron was unneeded anyway
			DTTetrahedron tetrahedronToDelete = tetrahedronsToEdit[0];
			// Debug.Log($"Creating 3 tetrahedrons from 1, 1 has {tetrahedronsToEdit[0].CORNERS.Length} corners");
			for (int i = 0; i < tetrahedronsToEdit[0].CORNERS.Length; i++)
			{
				DTPoint3D corner = tetrahedronsToEdit[0].CORNERS[i];

				//* Create new tetrahedron with point as a corner
				DTTetrahedron pointTetrahedron = new DTTetrahedron(new DTPoint3D[]
				{
					point,
					corner,
					tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length],
					tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length]
				});

				bool IsTetrahedronPermutation = await Task.Run(() =>
				{
					return IsTetrahedronAPermutation(pointTetrahedron);
				});

				if (!IsTetrahedronPermutation)
				{
					// Debug.Log("Tetrahedron from super-tetrahedron is NOT permutation, WILL be added to active list");

					//* Add to active tetrahedron list if tetrahedron is not a permutation -- a tetrahedron with these edges has not been created before
					tetrahedronList.Add(pointTetrahedron);
				}
				// else
				// {
				// 	Debug.Log("Tetrahedron from super-tetrahedron is permutation, will not be added to active list");
				// }
			}

			// Debug.Log("Removing 1 tetrahedron");

			//* Remove old tetrahedron
			tetrahedronList.Remove(tetrahedronToDelete);
			return;
		}
		else
		{
			//* Else if there are multiple

			////* Store any edge that borders two tetrahedrons that contains the point
			//* Might not need to do above, doing this part in 3D might just entail creating new, smaller tetrahedrons within a geater tetrahedron and deleting the larger tetrahedron
			//* Would still need to create new, smaller tetrahedrons for every tetrahedron that contains the point

			//* Store any tetrahedrons that contains the point for deletion later
			List<DTTetrahedron> tetrahedronsToDelete = new List<DTTetrahedron>();

			for (int i = 0; i < tetrahedronsToEdit.Count; i++)
			{
				DTTetrahedron tetrahedron = tetrahedronsToEdit[i];

				//* Add old tetrahedron to deletion list whether or not the resulting tetrahedrons are permutations
				//* -- if the resulting tetrahedrons are permutations then the old tetrahedron was unneeded anyway
				tetrahedronsToDelete.Add(tetrahedron);

				// Debug.Log($"Creating multiple tetrahedrons from multiples, current has {tetrahedron.CORNERS.Length} corners");
				for (int j = 0; j < tetrahedron.CORNERS.Length; j++)
				{
					DTPoint3D corner = tetrahedron.CORNERS[j];
					// point.AddEdge(corner);
					// point.AddNeighbour(corner);

					//* Create new tetrahedron with point as a corner
					DTTetrahedron pointTetrahedron = new DTTetrahedron(new DTPoint3D[]
					{
						point,
						corner,
						tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length],
						tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length]
					});

					bool IsTetrahedronPermutation = await Task.Run(() =>
					{
						return IsTetrahedronAPermutation(pointTetrahedron);
					});

					if (!IsTetrahedronPermutation)
					{
						// Debug.Log("Tetrahedron from sub-tetrahedron is NOT permutation, WILL be added to active list");

						//* Add to active tetrahedron list if tetrahedron is not a permutation -- a tetrahedron with these edges has not been created before
						tetrahedronList.Add(pointTetrahedron);
					}
					// else
					// {
					// 	Debug.Log("Tetrahedron from sub-tetrahedron is permutation, will not be added to active list");
					// }
				}
			}

			//* Remove old tetrahedrons
			for (int i = 0; i < tetrahedronsToDelete.Count; i++)
			{
				// Debug.Log("Removing multiple tetrahedrons");

				DTTetrahedron toDelete = tetrahedronsToDelete[i];

				tetrahedronList.Remove(toDelete);
			}
		}
	}

	int CheckCircumSphere(DTTetrahedron points, DTPoint3D room)
	{
		// Source for finding circumcenter of tetrahedron: By Jon -> https://gamedev.stackexchange.com/questions/110223/how-do-i-find-the-circumsphere-of-a-tetrahedron

		// Convert positions of tetrahedron to rows by creating vectors from an arbitrary common corner to each other corner
		Vector3 row1 = points.CORNERS[1].POSITION - points.CORNERS[0].POSITION;
		float length1SQ = row1.sqrMagnitude;
		Vector3 row2 = points.CORNERS[2].POSITION - points.CORNERS[0].POSITION;
		float length2SQ = row2.sqrMagnitude;
		Vector3 row3 = points.CORNERS[3].POSITION - points.CORNERS[0].POSITION;
		float length3SQ = row3.sqrMagnitude;

		//Determinant of matrix:
		//The "solution" of a matrix -- the point at which several lines(in 2D/3D)/planes(3D) meet
		//-- if determinant is == 0 the lines/planes never meet and the matrix/system has no solution
		float determinant = row1.x * (row2.y * row3.z - row3.y * row2.z)
			- row2.x * (row1.y * row3.z - row3.y * row1.z)
			+ row3.x * (row1.y * row2.z - row2.y * row1.z);

		//Volume, and "scalar quantity for re-use in formula"
		float volume = determinant / 6f;
		float iTwelveVolume = 1f / (volume * 12f);

		Vector3Int center = Vector3Int.zero;
		center.x = (int) (points.CORNERS[0].POSITION.x + iTwelveVolume * ((row2.y * row3.z - row3.y * row2.z) * length1SQ - (row1.y * row3.z - row3.y * row1.z) * length2SQ + (row1.y * row2.z - row2.y * row1.z) * length3SQ));
		center.y = (int) (points.CORNERS[0].POSITION.y + iTwelveVolume * (-(row2.x * row3.z - row3.x * row2.z) * length1SQ + (row1.x * row3.z - row3.x * row1.z) * length2SQ - (row1.x * row2.z - row2.x * row1.z) * length3SQ));
		center.z = (int) (points.CORNERS[0].POSITION.z + iTwelveVolume * ((row2.x * row3.y - row3.x * row2.y) * length1SQ - (row1.x * row3.y - row3.x * row1.y) * length2SQ + (row1.x * row2.y - row2.x * row1.y) * length3SQ));

		int radius = (int) (center - points.CORNERS[0].POSITION).sqrMagnitude;

		if (CheckInsideSphere(room.POSITION, center, radius))
		{
			// Debug.Log("Point inside circumsphere");
			return room.INDEX;
		}
		else
		{
			// Debug.Log("Point outside circumsphere");
			return -10;
		}
	}

	bool CheckInsideSphere(Vector3Int position, Vector3Int center, int radius)
	{
		if (position.x <= center.x + radius && position.x >= center.x - radius
			&& position.y <= center.y + radius && position.y >= center.y - radius
			&& position.z <= center.z + radius && position.z >= center.z - radius)
			return true;
		return false;
	}

	bool IsTetrahedronAPermutation(DTTetrahedron tetrahedron, List<DTPoint3D> usedPoints = null)
	{
		if (usedPoints != null && usedPoints.Count == 4)
		{
			if (!tetrahedronPermutations.Contains((usedPoints[0], usedPoints[1], usedPoints[2], usedPoints[3])))
			{
				Debug.Log("Added new tetrahedron permutation");
				tetrahedronPermutations.Add((usedPoints[0], usedPoints[1], usedPoints[2], usedPoints[3]));
			}
			return false;
		}

		if (usedPoints == null)
			usedPoints = new List<DTPoint3D>();

		if (tetrahedronPermutations.Count == 0)
		{
			for (int i = 0; i < tetrahedron.CORNERS.Length; i++)
			{
				DTPoint3D point = tetrahedron.CORNERS[i];
				if (usedPoints.Contains(point))
					continue;
				usedPoints.Add(point);
				return IsTetrahedronAPermutation(tetrahedron, usedPoints);
			}
		}
		else
		{
			for (int i = 0; i < tetrahedron.CORNERS.Length; i++)
			{
				int length = tetrahedron.CORNERS.Length;
				if (tetrahedronPermutations.Contains((tetrahedron.CORNERS[i], tetrahedron.CORNERS[(i + 1) % length],
						tetrahedron.CORNERS[(i + 2) % length], tetrahedron.CORNERS[(i + 3) % length])))
				{
					Debug.Log("Tetrahedron is permutation!");
					return true;
				}
				DTPoint3D point = tetrahedron.CORNERS[i];
				if (usedPoints.Contains(point))
					continue;
				usedPoints.Add(point);
				return IsTetrahedronAPermutation(tetrahedron, usedPoints);
			}
		}
		return false;
	}
}

public class DTPoint3D : System.Object
{
	public Vector3Int POSITION { get; private set; }

	public List<DTEdge3D> EDGES { get; private set; } = new List<DTEdge3D>();

	public List<DTPoint3D> NEIGHBOURS { get; private set; } = new List<DTPoint3D>();

	public int INDEX { get; private set; } = -10;

	public Room ROOM { get; private set; }

	public DTPoint3D(DTPoint3D copy)
	{
		POSITION = copy.POSITION;
		INDEX = copy.INDEX;
		ROOM = copy.ROOM;
	}

	public DTPoint3D(int X, int Y, int Z, int index, Room room)
	{
		POSITION = new Vector3Int(X, Y, Z);
		INDEX = index;
		ROOM = room;
	}

	public DTPoint3D(Vector3 pos, int index, Room room)
	{
		POSITION = new Vector3Int((int) pos.x, (int) pos.y, (int) pos.z);
		INDEX = index;
		ROOM = room;
	}

	public DTPoint3D(Vector3Int pos, int index, Room room)
	{
		POSITION = pos;
		INDEX = index;
		ROOM = room;
	}

	public void AddEdge(DTPoint3D neighbour)
	{
		if (EDGES.Contains(new DTEdge3D(this, neighbour))) return;
		EDGES.Add(new DTEdge3D(this, neighbour));
	}

	public void RemoveEdge(Vector3Int start, Vector3Int end)
	{
		DTEdge3D edge = EDGES.Find(x => x.START.POSITION == start && x.END.POSITION == end);
		EDGES.Remove(edge);
	}

	public void RemoveEdge(DTPoint3D neighbour)
	{
		DTEdge3D edge = EDGES.Find(x => x.END == neighbour);
		EDGES.Remove(edge);
	}

	public void AddNeighbour(DTPoint3D point)
	{
		if (NEIGHBOURS.Contains(point)) return;
		NEIGHBOURS.Add(point);
	}

	public void RemoveNeighbour(DTPoint3D point) => NEIGHBOURS.Remove(point);
}

public class DTEdge3D : System.Object, IEquatable<DTEdge3D>
	{
		public DTPoint3D START { get; private set; }
		public DTPoint3D END { get; private set; }
		public Vector3 DIRECTION { get; private set; }
		public int MAGNITUDE { get; private set; }

		public DTEdge3D(DTEdge3D copy)
		{
			START = copy.START;
			END = copy.END;
			DIRECTION = ((Vector3) copy.END.POSITION - (Vector3) copy.START.POSITION).normalized;
			MAGNITUDE = (int) (copy.END.POSITION - copy.START.POSITION).magnitude;
		}

		public DTEdge3D(DTPoint3D start, DTPoint3D end)
		{
			if (start == null || end == null)
			{
				Debug.Log($"Tetrahedrization stage: DTPoint start state: {start} : DTPoint end state: {end}");
				return;
			}

			START = start;
			END = end;
			DIRECTION = ((Vector3) end.POSITION - (Vector3) start.POSITION).normalized;
			MAGNITUDE = (int) (end.POSITION - start.POSITION).magnitude;
		}

		public bool Equals(DTEdge3D other)
		{
			return other == this || other.START.POSITION == START.POSITION && other.END.POSITION == END.POSITION || other.START.POSITION == END.POSITION && other.END.POSITION == START.POSITION;
		}
	}

public class DTTetrahedron : System.Object
{
	public DTPoint3D[] CORNERS { get; private set; }

	public Vector3Int CENTER { get; private set; }

	public DTTetrahedron(DTPoint3D[] corners)
	{
		CORNERS = corners;
		CENTER = new Vector3Int();
		for (int i = 0; i < CORNERS.Length; i++)
		{
			CENTER += CORNERS[i].POSITION;
			for (int j = (i + 1) % CORNERS.Length; j != i; j = (j + 1) % CORNERS.Length)
			{
				CORNERS[i].AddEdge(CORNERS[j]);
			}
		}
		CENTER /= CORNERS.Length;
	}
}