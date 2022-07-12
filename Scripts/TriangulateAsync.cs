using System;
using System.Collections;
using System.Collections.Generic;
// using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Delaunay triangulation steps:
//1: find super-triangle, store in list of triangles
//2: check random point in point-set
//3: if point is within circumcircle of a lone triangle in triangle list:
//3 A: connect edges from triangle-corners to the point
//3: else if point is within circumcirle of multiple triangles in triangle list
//3 B: find common/shared edge(s) of triangles, delete those edges, connect new edges from triangle corners to the point
//4: delete any old triangle(s) from the triangle list whose edges were deleted, and store the newly created ones that became a result of newly created edges
//5: if there's no point in point-set left to check -> PROFIT!

public class TriangulateAsync
{
	private DTPoint3DAsync[] points;
	private DTTetrahedronAsync root;
	private Vector3Int centerPoint;
	private float superTetrahedronRadius;
	private List<DTTetrahedronAsync> tetrahedronList = new List<DTTetrahedronAsync>();

	private const float DebugTimeWindow = 20f;

	private List < (Vector3Int, Vector3Int) > activeConnections = new List < (Vector3Int, Vector3Int) > ();

	private List < (DTPoint3DAsync, DTPoint3DAsync, DTPoint3DAsync, DTPoint3DAsync) > tetrahedronPermutations = new List < (DTPoint3DAsync, DTPoint3DAsync, DTPoint3DAsync, DTPoint3DAsync) > ();

	public TriangulateAsync(DTPoint3DAsync[] points, Vector3Int centerPoint, float superTetrahedronRadius)
	{
		this.points = points;
		this.centerPoint = centerPoint;
		this.superTetrahedronRadius = superTetrahedronRadius;
	}

	//* Start with inserting one point, triangulate that and then insert another point
	public async Task<DTPoint3DAsync[]> Triangulate()
	{
		//* Create supertriangle/root
		root = GetSuperPoints();

		//* Store and iteratively insert points later
		Stack<DTPoint3DAsync> pointsToInsert = new Stack<DTPoint3DAsync>();
		for (int i = 0; i < points.Length; i++)
		{
			pointsToInsert.Push(points[i]);
		}

		//* Go through and iteratively triangulate individual points
		while (pointsToInsert.Count > 0)
		{
			Debug.Log($"Points to insert: {pointsToInsert.Count - 1}");
			DTPoint3DAsync currentPoint = pointsToInsert.Pop();
			await UpdateTetrahedronsEncompassingPoint(currentPoint);
			// await UpdateTetrahedronsEncompassingPoint(currentPoint);
			// await DeleteEdges(currentPoint);
			// await UpdateTetrahedronsEncompassingPoint(currentPoint);
			// await CreateNewEdges(currentPoint);
		}

		//* Delete super-tetrahedron edges
		// await RemoveOuterEdges();
		// await RemoveConnectionsToSuperTetrahedron();

		//* Create connections resulting from tetrahedron building
		await CreateNeighbourConnections();

		return points;
	}

	DTTetrahedronAsync GetSuperPoints()
	{
		DTPoint3DAsync[] outerPoints = new DTPoint3DAsync[4];

		Vector3 point = centerPoint + Vector3.up * superTetrahedronRadius;
		outerPoints[0] = new DTPoint3DAsync(point, -1, null);

		// Debug.DrawLine(centerPoint, outerPoints[0].POSITION, Color.red, Time.time + 10f);
		Vector3 forward;

		//* Could've use cosine & sine but oh well this works so whatevs...
		for (int i = 0; i < 3; i++)
		{
			Quaternion lookRotation = Quaternion.Euler(200, 120 * i, 0);
			forward = lookRotation * Vector3.one;

			point = centerPoint + forward * superTetrahedronRadius;
			outerPoints[1 + i] = new DTPoint3DAsync(point, -2 - i, null);

			// Debug.DrawLine(centerPoint, outerPoints[1 + i].POSITION, Color.red, Time.time + DebugTimeWindow);
		}

		for (int i = 0; i < outerPoints.Length; i++)
		{
			DTPoint3DAsync firstPoint = outerPoints[i];
			for (int j = (i + 1) % outerPoints.Length; j != i; j = (j + 1) % outerPoints.Length)
			{
				DTPoint3DAsync otherPoint = outerPoints[j];
				// firstPoint.AddNeighbour(otherPoint);
				// firstPoint.AddEdge(otherPoint);

				otherPoint.AddNeighbour(firstPoint);
				otherPoint.AddEdge(firstPoint);
				// Debug.DrawLine(firstPoint.POSITION, otherPoint.POSITION, Color.green, Time.time + DebugTimeWindow);
			}
		}

		DTTetrahedronAsync rootTetra = new DTTetrahedronAsync(outerPoints);

		tetrahedronList.Add(rootTetra);

		return rootTetra;
	}

	async Task CreateNewEdges(DTPoint3DAsync point)
	{
		//* Check for tetrahedrons that contain the point, if found i need to update and remove edges
		// DTTetrahedronAsync[] tetrahedrons = await GetTetrahedronsEncompassingPoint(point);

		//* Iterate each found tetrahedron
		foreach (DTTetrahedronAsync tetrahedron in tetrahedronList)
		{
			//* Iterate each corner of each tetrahedron and create a new edge from the corner to the newly added point
			foreach (DTPoint3DAsync corner in tetrahedron.CORNERS)
			{
				// point.AddNeighbour(corner);
				// point.AddEdge(corner);

				corner.AddNeighbour(point);
				corner.AddEdge(point);

				Debug.Log("Adding edges");
				Vector3 middlePoint = corner.POSITION + point.POSITION / 2;
				Debug.DrawLine(corner.POSITION, middlePoint, Color.blue, 5f);
				Debug.DrawLine(middlePoint, point.POSITION, Color.green, 5f);
				await Task.Delay(5000);
				activeConnections.Add((corner.POSITION, point.POSITION));
				// Debug.DrawLine(point.POSITION, corner.POSITION, Color.magenta, Time.time + DebugTimeWindow);
			}
		}
		for (int i = 0; i < activeConnections.Count; i++)
		{
			Debug.DrawLine(activeConnections[i].Item1, activeConnections[i].Item2, Color.green, 5f);
		}
	}

	async Task DeleteEdges(DTPoint3DAsync point)
	{
		//* Check for tetrahedrons that contain the point, if found i need to update and remove edges
		// DTTetrahedronAsync[] tetrahedrons = await GetTetrahedronsEncompassingPoint(point);

		//* Iterate each found tetrahedron
		foreach (DTTetrahedronAsync tetrahedron in tetrahedronList)
		{
			//* Iterate each corner of each tetrahedron and delete the edge connecting/intersecting the corner and the newly added point
			foreach (DTPoint3DAsync corner in tetrahedron.CORNERS)
			{
				corner.RemoveNeighbour(point);
				corner.RemoveEdge(point);

				Debug.Log("Removing edges");
				Vector3 middlePoint = corner.POSITION + point.POSITION / 2;
				Debug.DrawLine(corner.POSITION, middlePoint, Color.blue, 5f);
				Debug.DrawLine(middlePoint, point.POSITION, Color.red, 5f);
				await Task.Delay(5000);
				if (activeConnections.Contains((corner.POSITION, point.POSITION)))
					activeConnections.Remove((corner.POSITION, point.POSITION));

				// point.RemoveNeighbour(corner);
				// point.RemoveEdge(corner);
				// Debug.DrawLine(point.POSITION, corner.POSITION, Color.black, DebugTimeWindow);
			}
		}
		for (int i = 0; i < activeConnections.Count; i++)
		{
			Debug.DrawLine(activeConnections[i].Item1, activeConnections[i].Item2, Color.green, 5f);
		}
	}

	async Task CreateNeighbourConnections()
	{
		Debug.Log("Creating neighbour connections");
		for (int i = tetrahedronList.Count - 1; i >= 0; i--)
		{
			Debug.Log($"Tetrahedrons left to search: {i}");

			DTTetrahedronAsync tetrahedron = tetrahedronList[i];
			bool deleteTetrahedron = false;

			// deleteTetrahedron = IsTetrahedronAPermutation(tetrahedron);

			if (deleteTetrahedron)
			{
				Debug.Log("Tetrahedron is a permutation, marked for early deletion");

				DTPoint3DAsync point = tetrahedron.CORNERS[0];

				Debug.DrawLine(point.POSITION, tetrahedron.CORNERS[1].POSITION, Color.red, .001f);
				Debug.DrawLine(point.POSITION, tetrahedron.CORNERS[2].POSITION, Color.red, .001f);
				Debug.DrawLine(point.POSITION, tetrahedron.CORNERS[3].POSITION, Color.red, .001f);

				Debug.DrawLine(tetrahedron.CORNERS[1].POSITION, tetrahedron.CORNERS[2].POSITION, Color.red, .001f);
				Debug.DrawLine(tetrahedron.CORNERS[2].POSITION, tetrahedron.CORNERS[3].POSITION, Color.red, .001f);
				Debug.DrawLine(tetrahedron.CORNERS[3].POSITION, tetrahedron.CORNERS[1].POSITION, Color.red, .001f);

				await Task.Delay(1);
			}

			Debug.Log("Searching corners for root match, tetrahedron is not a permutation");

			for (int j = 0; j < tetrahedron.CORNERS.Length; j++)
			{
				if (deleteTetrahedron)
					break;

				DTPoint3DAsync point = tetrahedron.CORNERS[j];
				for (int k = 0; k < root.CORNERS.Length; k++)
				{
					DTPoint3DAsync corner = root.CORNERS[k];
					if (point == corner)
					{
						Debug.Log("Root match found, exiting and marking for deletion");
						deleteTetrahedron = true;

						Debug.Log("Drawing faulty tetrahedron connections");
						Debug.DrawLine(point.POSITION, tetrahedron.CORNERS[(j + 1) % tetrahedron.CORNERS.Length].POSITION, Color.red, .001f);
						Debug.DrawLine(point.POSITION, tetrahedron.CORNERS[(j + 2) % tetrahedron.CORNERS.Length].POSITION, Color.red, .001f);
						Debug.DrawLine(point.POSITION, tetrahedron.CORNERS[(j + 3) % tetrahedron.CORNERS.Length].POSITION, Color.red, .001f);

						Debug.DrawLine(tetrahedron.CORNERS[(j + 1) % tetrahedron.CORNERS.Length].POSITION,
							tetrahedron.CORNERS[(j + 2) % tetrahedron.CORNERS.Length].POSITION, Color.red, .001f);
						Debug.DrawLine(tetrahedron.CORNERS[(j + 2) % tetrahedron.CORNERS.Length].POSITION,
							tetrahedron.CORNERS[(j + 3) % tetrahedron.CORNERS.Length].POSITION, Color.red, .001f);
						Debug.DrawLine(tetrahedron.CORNERS[(j + 3) % tetrahedron.CORNERS.Length].POSITION,
							tetrahedron.CORNERS[(j + 1) % tetrahedron.CORNERS.Length].POSITION, Color.red, .001f);

						await Task.Delay(1);

						break;
					}
				}
			}

			if (deleteTetrahedron)
			{
				Debug.Log("Root match or permutation found, deleting tetrahedron");
				tetrahedronList.Remove(tetrahedron);
				continue;
			}

			Debug.Log("No match or permutation found, creating connections for corners of tetrahedron");
			DTPoint3DAsync firstPoint = tetrahedron.CORNERS[0];
			DTPoint3DAsync secondPoint = tetrahedron.CORNERS[1];
			DTPoint3DAsync thirdPoint = tetrahedron.CORNERS[2];
			DTPoint3DAsync fourthPoint = tetrahedron.CORNERS[3];

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

			Debug.Log("Drawing valid tetrahedron connections");
			Debug.DrawLine(firstPoint.POSITION, secondPoint.POSITION, Color.blue, .01f);
			Debug.DrawLine(firstPoint.POSITION, thirdPoint.POSITION, Color.blue, .01f);
			Debug.DrawLine(firstPoint.POSITION, fourthPoint.POSITION, Color.blue, .01f);

			Debug.DrawLine(secondPoint.POSITION, thirdPoint.POSITION, Color.blue, .01f);
			Debug.DrawLine(thirdPoint.POSITION, fourthPoint.POSITION, Color.blue, .01f);
			Debug.DrawLine(fourthPoint.POSITION, secondPoint.POSITION, Color.blue, .01f);

			await Task.Delay(10);
		}

		// for (int i = tetrahedronList.Count - 1; i >= 0; i--)
		// {
		// 	DTTetrahedronAsync tetrahedron = tetrahedronList[i];

		// 	DTPoint3DAsync firstPoint = tetrahedron.CORNERS[0];
		// 	DTPoint3DAsync secondPoint = tetrahedron.CORNERS[1];
		// 	DTPoint3DAsync thirdPoint = tetrahedron.CORNERS[2];
		// 	DTPoint3DAsync fourthPoint = tetrahedron.CORNERS[3];

		// 	Debug.Log("Drawing valid tetrahedron connections after list deletion");
		// 	Debug.DrawLine(firstPoint.POSITION, secondPoint.POSITION, Color.blue, 60f);
		// 	Debug.DrawLine(firstPoint.POSITION, thirdPoint.POSITION, Color.blue, 60f);
		// 	Debug.DrawLine(firstPoint.POSITION, fourthPoint.POSITION, Color.blue, 60f);

		// 	Debug.DrawLine(secondPoint.POSITION, thirdPoint.POSITION, Color.blue, 60f);
		// 	Debug.DrawLine(thirdPoint.POSITION, fourthPoint.POSITION, Color.blue, 60f);
		// 	Debug.DrawLine(fourthPoint.POSITION, secondPoint.POSITION, Color.blue, 60f);
		// }
	}

	async Task RemoveConnectionsToSuperTetrahedron()
	{
		Debug.Log("Removing super connections");
		for (int i = 0; i < tetrahedronList.Count; i++)
		{
			DTTetrahedronAsync current = tetrahedronList[i];
			for (int j = 0; j < current.CORNERS.Length; j++)
			{
				DTPoint3DAsync point = current.CORNERS[j];
				for (int k = 0; k < root.CORNERS.Length; k++)
				{
					DTPoint3DAsync corner = root.CORNERS[k];
					if (point.NEIGHBOURS.Contains(corner))
					{
						Debug.Log("Found connection to super-point, removing");
						point.NEIGHBOURS.Remove(corner);
						Debug.DrawLine(point.POSITION, corner.POSITION, Color.red, .1f);
						await Task.Delay(100);
					}
				}
				for (int k = 0; k < point.NEIGHBOURS.Count; k++)
				{
					Debug.Log("Drawing connection to neighbouring point");
					DTPoint3DAsync neighbour = point.NEIGHBOURS[k];
					Debug.DrawLine(point.POSITION, neighbour.POSITION, Color.green, 20f);
					await Task.Delay(100);
				}
			}
		}
	}

	async Task RemoveOuterEdges()
	{
		for (int j = root.CORNERS.Length - 1; j >= 0; j--)
		{
			DTPoint3DAsync point = root.CORNERS[j];
			if (point.NEIGHBOURS.Count == 0) continue;
			for (int i = point.NEIGHBOURS.Count - 1; i >= 0; i--)
			{
				DTPoint3DAsync otherPoint = point.NEIGHBOURS[i];
				if (point == otherPoint) continue;
				// point.RemoveNeighbour(otherPoint);
				// point.RemoveEdge(otherPoint);

				otherPoint.RemoveNeighbour(point);
				otherPoint.RemoveEdge(point);

				Debug.Log("Removing super edges");
				Vector3 middlePoint = otherPoint.POSITION + point.POSITION / 2;
				Debug.DrawLine(otherPoint.POSITION, middlePoint, Color.black, 5f);
				Debug.DrawLine(middlePoint, point.POSITION, Color.red, 5f);
				await Task.Delay(5000);
				// Debug.DrawLine(point.POSITION, otherPoint.POSITION, Color.black, DebugTimeWindow);
			}
		}
		for (int i = 0; i < activeConnections.Count; i++)
		{
			Debug.DrawLine(activeConnections[i].Item1, activeConnections[i].Item2, Color.green, 5f);
		}
	}

	async Task UpdateTetrahedronsEncompassingPoint(DTPoint3DAsync point)
	{
		await Task.Delay(0000001);

		List<DTTetrahedronAsync> tetrahedronsToEdit = new List<DTTetrahedronAsync>();

		for (int i = 0; i < tetrahedronList.Count; i++)
		{
			DTTetrahedronAsync tetrahedron = tetrahedronList[i];
			int index = await CheckCircumSphere(tetrahedron, point);

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
			DTTetrahedronAsync tetrahedronToDelete = tetrahedronsToEdit[0];
			Debug.Log($"Creating 3 tetrahedrons from 1, 1 has {tetrahedronsToEdit[0].CORNERS.Length} corners");
			for (int i = 0; i < tetrahedronsToEdit[0].CORNERS.Length; i++)
			{
				DTPoint3DAsync corner = tetrahedronsToEdit[0].CORNERS[i];
				// point.AddEdge(corner);
				// point.AddNeighbour(corner);

				//* Create new tetrahedron with point as a corner
				DTTetrahedronAsync pointTetrahedron = new DTTetrahedronAsync(new DTPoint3DAsync[]
				{
					point,
					corner,
					tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length],
					tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length]
				});

				if (!IsTetrahedronAPermutation(pointTetrahedron))
				{
					Debug.Log("Tetrahedron from super-tetrahedron is NOT permutation, WILL be added to active list");

					//* Add to active tetrahedron list if tetrahedron is not a permutation -- a tetrahedron with these edges has not been created before
					tetrahedronList.Add(pointTetrahedron);

					Debug.DrawLine(point.POSITION, corner.POSITION, Color.blue, .01f);
					Debug.DrawLine(point.POSITION, tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.blue, .01f);
					Debug.DrawLine(point.POSITION, tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.blue, .01f);

					Debug.DrawLine(corner.POSITION, tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.blue, .01f);
					Debug.DrawLine(tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length].POSITION,
						tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.blue, .01f);
					Debug.DrawLine(tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, corner.POSITION, Color.blue, .01f);

					await Task.Delay(10);
				}
				else
				{
					Debug.Log("Tetrahedron from super-tetrahedron is permutation, will not be added to active list");

					Debug.DrawLine(point.POSITION, corner.POSITION, Color.red, 01f);
					Debug.DrawLine(point.POSITION, tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.red, .01f);
					Debug.DrawLine(point.POSITION, tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.red, .01f);

					Debug.DrawLine(corner.POSITION, tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.red, .01f);
					Debug.DrawLine(tetrahedronsToEdit[0].CORNERS[(i + 1) % tetrahedronsToEdit[0].CORNERS.Length].POSITION,
						tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, Color.red, .01f);
					Debug.DrawLine(tetrahedronsToEdit[0].CORNERS[(i + 2) % tetrahedronsToEdit[0].CORNERS.Length].POSITION, corner.POSITION, Color.red, .01f);

					await Task.Delay(10);
				}
			}

			Debug.Log("Removing 1 tetrahedron");
			// Debug.DrawLine(tetrahedronToDelete.CORNERS[0].POSITION, tetrahedronToDelete.CORNERS[1].POSITION, Color.red, .5f);
			// Debug.DrawLine(tetrahedronToDelete.CORNERS[0].POSITION, tetrahedronToDelete.CORNERS[2].POSITION, Color.red, .5f);
			// Debug.DrawLine(tetrahedronToDelete.CORNERS[0].POSITION, tetrahedronToDelete.CORNERS[3].POSITION, Color.red, .5f);

			// Debug.DrawLine(tetrahedronToDelete.CORNERS[1].POSITION, tetrahedronToDelete.CORNERS[2].POSITION, Color.red, .5f);
			// Debug.DrawLine(tetrahedronToDelete.CORNERS[2].POSITION, tetrahedronToDelete.CORNERS[3].POSITION, Color.red, .5f);
			// Debug.DrawLine(tetrahedronToDelete.CORNERS[3].POSITION, tetrahedronToDelete.CORNERS[1].POSITION, Color.red, .5f);

			// await Task.Delay(500);

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
			List<DTTetrahedronAsync> tetrahedronsToDelete = new List<DTTetrahedronAsync>();

			for (int i = 0; i < tetrahedronsToEdit.Count; i++)
			{
				DTTetrahedronAsync tetrahedron = tetrahedronsToEdit[i];

				//* Add old tetrahedron to deletion list whether or not the resulting tetrahedrons are permutations
				//* -- if the resulting tetrahedrons are permutations then the old tetrahedron was unneeded anyway
				tetrahedronsToDelete.Add(tetrahedron);

				Debug.Log($"Creating multiple tetrahedrons from multiples, current has {tetrahedron.CORNERS.Length} corners");
				for (int j = 0; j < tetrahedron.CORNERS.Length; j++)
				{
					DTPoint3DAsync corner = tetrahedron.CORNERS[j];
					// point.AddEdge(corner);
					// point.AddNeighbour(corner);

					//* Create new tetrahedron with point as a corner
					DTTetrahedronAsync pointTetrahedron = new DTTetrahedronAsync(new DTPoint3DAsync[]
					{
						point,
						corner,
						tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length],
						tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length]
					});

					if (!IsTetrahedronAPermutation(pointTetrahedron))
					{
						Debug.Log("Tetrahedron from sub-tetrahedron is NOT permutation, WILL be added to active list");

						//* Add to active tetrahedron list if tetrahedron is not a permutation -- a tetrahedron with these edges has not been created before
						tetrahedronList.Add(pointTetrahedron);

						Debug.DrawLine(point.POSITION, corner.POSITION, Color.blue, .01f);
						Debug.DrawLine(point.POSITION, tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.blue, .01f);
						Debug.DrawLine(point.POSITION, tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.blue, .01f);

						Debug.DrawLine(corner.POSITION, tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.blue, .01f);
						Debug.DrawLine(tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length].POSITION,
							tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.blue, .01f);
						Debug.DrawLine(tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, corner.POSITION, Color.blue, .01f);

						await Task.Delay(10);
					}
					else
					{
						Debug.Log("Tetrahedron from sub-tetrahedron is permutation, will not be added to active list");

						Debug.DrawLine(point.POSITION, corner.POSITION, Color.red, .01f);
						Debug.DrawLine(point.POSITION, tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.red, .01f);
						Debug.DrawLine(point.POSITION, tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.red, .01f);

						Debug.DrawLine(corner.POSITION, tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.red, .01f);
						Debug.DrawLine(tetrahedronsToEdit[i].CORNERS[(j + 1) % tetrahedronsToEdit[i].CORNERS.Length].POSITION,
							tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, Color.red, .01f);
						Debug.DrawLine(tetrahedronsToEdit[i].CORNERS[(j + 2) % tetrahedronsToEdit[i].CORNERS.Length].POSITION, corner.POSITION, Color.red, .01f);

						await Task.Delay(10);
					}
				}
			}

			//* Remove old tetrahedrons
			for (int i = 0; i < tetrahedronsToDelete.Count; i++)
			{
				Debug.Log("Removing multiple tetrahedrons");

				DTTetrahedronAsync toDelete = tetrahedronsToDelete[i];

				// Debug.DrawLine(toDelete.CORNERS[0].POSITION, toDelete.CORNERS[1].POSITION, Color.red, .5f);
				// Debug.DrawLine(toDelete.CORNERS[0].POSITION, toDelete.CORNERS[2].POSITION, Color.red, .5f);
				// Debug.DrawLine(toDelete.CORNERS[0].POSITION, toDelete.CORNERS[3].POSITION, Color.red, .5f);

				// Debug.DrawLine(toDelete.CORNERS[1].POSITION, toDelete.CORNERS[2].POSITION, Color.red, .5f);
				// Debug.DrawLine(toDelete.CORNERS[2].POSITION, toDelete.CORNERS[3].POSITION, Color.red, .5f);
				// Debug.DrawLine(toDelete.CORNERS[3].POSITION, toDelete.CORNERS[1].POSITION, Color.red, .5f);

				// await Task.Delay(500);

				tetrahedronList.Remove(toDelete);
			}
		}
	}

	async Task CreateTetrahedronsEncompassingPoint(DTPoint3DAsync point)
	{
		//* Create the sets of 4 points and verify if the point is within any of the sets
		Dictionary<int, DTTetrahedronAsync> tetrahedrons = new Dictionary<int, DTTetrahedronAsync>();

		//* Add the super-tetrahedron corners to check the primary outer connection connecting to the point
		Queue<DTPoint3DAsync> pointsQueue = new Queue<DTPoint3DAsync>();
		foreach (DTPoint3DAsync rootCorner in root.CORNERS)
		{
			pointsQueue.Enqueue(rootCorner);
		}

		//* Iterate while there's still a corner-point to check
		while (pointsQueue.Count > 0)
		{
			//* Go through all the connected corners to the current corner-point
			DTPoint3DAsync currentRootCorner = pointsQueue.Dequeue();
			int count = currentRootCorner.NEIGHBOURS.Count;

			//* Attempt for counter of special case -- only three neighbours -> most likely the super-tetrahedron at the start
			if (count == 3)
			{
				//* Check three corner-points of tetrahedron
				DTPoint3DAsync first = currentRootCorner.NEIGHBOURS[0];
				DTPoint3DAsync second = currentRootCorner.NEIGHBOURS[1];
				DTPoint3DAsync third = currentRootCorner.NEIGHBOURS[2];

				//* Calculate what i'm HOPING is a unique set sum
				Vector3Int setVectorSum = (currentRootCorner.POSITION + first.POSITION + second.POSITION + third.POSITION) / 4;
				int currentSetSum = (int) setVectorSum.magnitude / 3; //first.INDEX + second.INDEX + third.INDEX + fourth.INDEX;

				//* Check that the set sum key is unique and hasn't been checked before
				if (!tetrahedrons.ContainsKey(currentSetSum))
				{
					Debug.Log("Unique tetrahedron: 3 neighbours");

					Debug.DrawLine(currentRootCorner.POSITION, first.POSITION, Color.blue, 5f);
					Debug.DrawLine(currentRootCorner.POSITION, second.POSITION, Color.blue, 5f);
					Debug.DrawLine(currentRootCorner.POSITION, third.POSITION, Color.blue, 5f);

					Debug.DrawLine(first.POSITION, second.POSITION, Color.blue, 5f);
					Debug.DrawLine(second.POSITION, third.POSITION, Color.blue, 5f);
					Debug.DrawLine(third.POSITION, first.POSITION, Color.blue, 5f);

					await Task.Delay(5000);

					//* Create and save new entry for checked tetrahedron
					tetrahedrons.Add(currentSetSum, new DTTetrahedronAsync(new DTPoint3DAsync[]
					{
						currentRootCorner,
						first,
						second,
						third
					}));

					//* Enqueue any point that hasn't been checked yet
					pointsQueue.Enqueue(first);
					pointsQueue.Enqueue(second);
					pointsQueue.Enqueue(third);
				}
				else
				{
					Debug.Log("Non-Unique tetrahedron: 3 neighbours");

					Debug.DrawLine(currentRootCorner.POSITION, first.POSITION, Color.red, 5f);
					Debug.DrawLine(currentRootCorner.POSITION, second.POSITION, Color.red, 5f);
					Debug.DrawLine(currentRootCorner.POSITION, third.POSITION, Color.red, 5f);

					Debug.DrawLine(first.POSITION, second.POSITION, Color.red, 5f);
					Debug.DrawLine(second.POSITION, third.POSITION, Color.red, 5f);
					Debug.DrawLine(third.POSITION, first.POSITION, Color.red, 5f);

					await Task.Delay(5000);
				}
			}
			//* More than 3 neighbours -- proceed to check all permutations of neighbours
			else
			{
				for (int i = 0; i < count; i++)
				{
					//* Check permutation of corner-points of tetrahedron
					DTPoint3DAsync first = currentRootCorner.NEIGHBOURS[(i % count)];
					DTPoint3DAsync second = currentRootCorner.NEIGHBOURS[(i + 1) % count];
					DTPoint3DAsync third = currentRootCorner.NEIGHBOURS[(i + 2) % count];

					//* Calculate what i'm HOPING is a unique set sum
					Vector3Int setVectorSum = (currentRootCorner.POSITION + first.POSITION + second.POSITION + third.POSITION) / 4;
					int currentSetSum = (int) setVectorSum.magnitude / 3; //first.INDEX + second.INDEX + third.INDEX + fourth.INDEX;

					//* Check that the set sum key is unique and hasn't been checked before
					if (!tetrahedrons.ContainsKey(currentSetSum))
					{
						Debug.Log($"Unique tetrahedron: {count} neighbours");

						Debug.DrawLine(currentRootCorner.POSITION, first.POSITION, Color.blue, 5f);
						Debug.DrawLine(currentRootCorner.POSITION, second.POSITION, Color.blue, 5f);
						Debug.DrawLine(currentRootCorner.POSITION, third.POSITION, Color.blue, 5f);

						Debug.DrawLine(first.POSITION, second.POSITION, Color.blue, 5f);
						Debug.DrawLine(second.POSITION, third.POSITION, Color.blue, 5f);
						Debug.DrawLine(third.POSITION, first.POSITION, Color.blue, 5f);

						await Task.Delay(5000);

						//* Create and save new entry for checked tetrahedron
						tetrahedrons.Add(currentSetSum, new DTTetrahedronAsync(new DTPoint3DAsync[]
						{
							currentRootCorner,
							first,
							second,
							third
						}));

						//* Enqueue any point that hasn't been checked yet
						pointsQueue.Enqueue(first);
						pointsQueue.Enqueue(second);
						pointsQueue.Enqueue(third);
					}
					else
					{
						Debug.Log($"Non-Unique tetrahedron: {count} neighbours");

						Debug.DrawLine(currentRootCorner.POSITION, first.POSITION, Color.red, 5f);
						Debug.DrawLine(currentRootCorner.POSITION, second.POSITION, Color.red, 5f);
						Debug.DrawLine(currentRootCorner.POSITION, third.POSITION, Color.red, 5f);

						Debug.DrawLine(first.POSITION, second.POSITION, Color.red, 5f);
						Debug.DrawLine(second.POSITION, third.POSITION, Color.red, 5f);
						Debug.DrawLine(third.POSITION, first.POSITION, Color.red, 5f);

						await Task.Delay(5000);
					}
				}
			}
		}

		if (tetrahedrons.Count == 0)
		{
			Debug.Log("Point set is empty");
		}

		//* Look through the tetrahedrons and check if point is withing any of the sets
		List<int> indexesToRemove = new List<int>();
		foreach (KeyValuePair<int, DTTetrahedronAsync> set in tetrahedrons)
		{
			tetrahedrons.TryGetValue(set.Key, out DTTetrahedronAsync value);
			int index = await CheckCircumSphere(value, point);
			if (index == -10)
			{
				indexesToRemove.Add(set.Key);
			}
		}

		//* Remove any tetrahedrons that don't contain the point
		for (int i = 0; i < indexesToRemove.Count; i++)
		{
			tetrahedrons.Remove(indexesToRemove[i]);
		}

		//* Transfer the remaining tetrahedrons to an array for returning
		DTTetrahedronAsync[] tetrahedronCollection = new DTTetrahedronAsync[tetrahedrons.Count];
		int counter = 0;
		foreach (DTTetrahedronAsync tetrahedron in tetrahedrons.Values)
		{
			tetrahedronCollection[counter] = tetrahedron;
			counter++;
		}

		tetrahedronList.AddRange(tetrahedronCollection);
	}

	async Task<int> CheckCircumSphere(DTTetrahedronAsync points, DTPoint3DAsync room)
	{
		await Task.Delay(0000001);

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
			Debug.Log("Point inside circumsphere");
			// foreach (DTPoint3DAsync point in points.CORNERS)
			// {
			// 	Debug.DrawLine(center, point.POSITION, Color.red, .25f);
			// }
			// Debug.DrawLine(center, room.POSITION, Color.blue, .25f);
			// await Task.Delay(250);
			return room.INDEX;
		}
		else
		{
			Debug.Log("Point outside circumsphere");
			// foreach (DTPoint3DAsync point in points.CORNERS)
			// {
			// 	Debug.DrawLine(center, point.POSITION, Color.green, .25f);
			// }
			// Debug.DrawLine(center, room.POSITION, Color.blue, .25f);
			// await Task.Delay(250);
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

	bool IsTetrahedronAPermutation(DTTetrahedronAsync tetrahedron, List<DTPoint3DAsync> usedPoints = null)
	{
		if (usedPoints != null && usedPoints.Count == 4)
		{
			tetrahedronPermutations.Add((usedPoints[0], usedPoints[1], usedPoints[2], usedPoints[3]));
			return false;
		}

		if (usedPoints == null)
			usedPoints = new List<DTPoint3DAsync>();

		if (tetrahedronPermutations.Count == 0)
		{
			for (int i = 0; i < tetrahedron.CORNERS.Length; i++)
			{
				DTPoint3DAsync point = tetrahedron.CORNERS[i];
				if (usedPoints.Contains(point))
					continue;
				usedPoints.Add(point);
				return IsTetrahedronAPermutation(tetrahedron, usedPoints);
			}
			return false;
		}
		else
		{
			for (int i = 0; i < tetrahedron.CORNERS.Length; i++)
			{
				int length = tetrahedron.CORNERS.Length;
				if (tetrahedronPermutations.Contains((tetrahedron.CORNERS[i], tetrahedron.CORNERS[(i + 1) % length],
						tetrahedron.CORNERS[(i + 2) % length], tetrahedron.CORNERS[(i + 3) % length])))
					return true;
				DTPoint3DAsync point = tetrahedron.CORNERS[i];
				if (usedPoints.Contains(point))
					continue;
				usedPoints.Add(point);
				return IsTetrahedronAPermutation(tetrahedron, usedPoints);
			}
			return false;
		}
	}
}

public class DTPoint3DAsync : System.Object
{
	public Vector3Int POSITION { get; private set; }

	public List<DTEdge3DAsync> EDGES { get; private set; } = new List<DTEdge3DAsync>();

	public List<DTPoint3DAsync> NEIGHBOURS { get; private set; } = new List<DTPoint3DAsync>();

	public int INDEX { get; private set; } = -10;

	public Room ROOM { get; private set; }

	public DTPoint3DAsync(DTPoint3DAsync copy)
	{
		POSITION = copy.POSITION;
		INDEX = copy.INDEX;
		ROOM = copy.ROOM;
	}

	public DTPoint3DAsync(int X, int Y, int Z, int index, Room room)
	{
		POSITION = new Vector3Int(X, Y, Z);
		INDEX = index;
		ROOM = room;
	}

	public DTPoint3DAsync(Vector3 pos, int index, Room room)
	{
		POSITION = new Vector3Int((int) pos.x, (int) pos.y, (int) pos.z);
		INDEX = index;
		ROOM = room;
	}

	public DTPoint3DAsync(Vector3Int pos, int index, Room room)
	{
		POSITION = pos;
		INDEX = index;
		ROOM = room;
	}

	public void AddEdge(DTPoint3DAsync neighbour)
	{
		if (EDGES.Contains(new DTEdge3DAsync(this, neighbour))) return;
		EDGES.Add(new DTEdge3DAsync(this, neighbour));
	}

	public void RemoveEdge(Vector3Int start, Vector3Int end)
	{
		DTEdge3DAsync edge = EDGES.Find(x => x.START.POSITION == start && x.END.POSITION == end);
		EDGES.Remove(edge);
	}

	public void RemoveEdge(DTPoint3DAsync neighbour)
	{
		DTEdge3DAsync edge = EDGES.Find(x => x.END == neighbour);
		EDGES.Remove(edge);
	}

	public void AddNeighbour(DTPoint3DAsync point)
	{
		if (NEIGHBOURS.Contains(point)) return;
		NEIGHBOURS.Add(point);
	}

	public void RemoveNeighbour(DTPoint3DAsync point) => NEIGHBOURS.Remove(point);
}

public class DTEdge3DAsync : System.Object, IEquatable<DTEdge3DAsync>
	{
		public DTPoint3DAsync START { get; private set; }
		public DTPoint3DAsync END { get; private set; }
		public Vector3 DIRECTION { get; private set; }
		public int MAGNITUDE { get; private set; }

		public DTEdge3DAsync(DTEdge3DAsync copy)
		{
			START = copy.START;
			END = copy.END;
			DIRECTION = ((Vector3) copy.END.POSITION - (Vector3) copy.START.POSITION).normalized;
			MAGNITUDE = (int) (copy.END.POSITION - copy.START.POSITION).magnitude;
		}

		public DTEdge3DAsync(DTPoint3DAsync start, DTPoint3DAsync end)
		{
			START = start;
			END = end;
			DIRECTION = ((Vector3) end.POSITION - (Vector3) start.POSITION).normalized;
			MAGNITUDE = (int) (end.POSITION - start.POSITION).magnitude;
		}

		public bool Equals(DTEdge3DAsync other)
		{
			return other == this || other.START.POSITION == START.POSITION && other.END.POSITION == END.POSITION || other.START.POSITION == END.POSITION && other.END.POSITION == START.POSITION;
		}
	}

public class DTTetrahedronAsync : System.Object
{
	public DTPoint3DAsync[] CORNERS { get; private set; }

	public Vector3Int CENTER { get; private set; }

	public DTTetrahedronAsync(DTPoint3DAsync[] corners)
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