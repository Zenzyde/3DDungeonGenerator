using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonDecorator : MonoBehaviour
{
	[SerializeField] private DungeonDecoration[] decorations;
	[SerializeField] private float decorChance;

	private DungeonGenerator3D dungeonGenerator;

	// Start is called before the first frame update
	IEnumerator Start()
	{
		dungeonGenerator = FindObjectOfType<DungeonGenerator3D>();

		yield return new WaitUntil(() => dungeonGenerator.DUNGEONDONE);

		DecorateDungeon();
	}

	void DecorateDungeon()
	{
		foreach (Room room in dungeonGenerator.ROOMS)
		{
			if (room.ChildTiles.Length == 0)
			{
				continue;
			}

			foreach (Transform t in room.ChildTiles)
			{
				if (t == null)
				{
					continue;
				}

				if (LayerMask.LayerToName(t.gameObject.layer) == "Wall")
				{
					if (Random.value > 1f - decorChance)
					{
						List<DungeonDecoration> dungeonDecorations = new List<DungeonDecoration>();

						for (int i = 0; i < decorations.Length; i++)
						{
							DungeonDecoration decoration = decorations[i];
							if (decoration.decorationType == EDungeonTileType.wall)
								dungeonDecorations.Add(decoration);
						}

						if (dungeonDecorations.Count == 0)
							continue;

						int decorIndex = Random.Range(0, dungeonDecorations.Count);

						DungeonDecoration dungeonDecoration = dungeonDecorations[decorIndex];

						GameObject decorationObject = dungeonDecoration.decoration;

						if (Random.value > 1f - dungeonDecoration.chance)
						{
							if (dungeonDecoration.alignLookDirectionToTile)
							{
								Instantiate(decorationObject, t.position, Quaternion.LookRotation(-t.forward));
							}
							else
							{
								Instantiate(decorationObject, t.position, Quaternion.Euler(new Vector3(0, Random.value * 360f, 0)));
							}
						}
					}
				}
				else if (LayerMask.LayerToName(t.gameObject.layer) == "Floor")
				{
					List<DungeonDecoration> dungeonDecorations = new List<DungeonDecoration>();

					for (int i = 0; i < decorations.Length; i++)
					{
						DungeonDecoration decoration = decorations[i];
						if (decoration.decorationType == EDungeonTileType.floor)
							dungeonDecorations.Add(decoration);
					}

					if (dungeonDecorations.Count == 0)
						continue;

					int decorIndex = Random.Range(0, dungeonDecorations.Count);

					DungeonDecoration dungeonDecoration = dungeonDecorations[decorIndex];

					GameObject decorationObject = dungeonDecoration.decoration;

					if (Random.value > 1f - dungeonDecoration.chance)
					{
						if (dungeonDecoration.alignLookDirectionToTile)
						{
							Instantiate(decorationObject, t.position, Quaternion.LookRotation(-t.forward));
						}
						else
						{
							Instantiate(decorationObject, t.position, Quaternion.Euler(new Vector3(0, Random.value * 360f, 0)));
						}
					}
				}
				else if (LayerMask.LayerToName(t.gameObject.layer) == "Roof")
				{
					List<DungeonDecoration> dungeonDecorations = new List<DungeonDecoration>();

					for (int i = 0; i < decorations.Length; i++)
					{
						DungeonDecoration decoration = decorations[i];
						if (decoration.decorationType == EDungeonTileType.roof)
							dungeonDecorations.Add(decoration);
					}

					if (dungeonDecorations.Count == 0)
						continue;

					int decorIndex = Random.Range(0, dungeonDecorations.Count);

					DungeonDecoration dungeonDecoration = dungeonDecorations[decorIndex];

					GameObject decorationObject = dungeonDecoration.decoration;

					if (Random.value > 1f - dungeonDecoration.chance)
					{
						if (dungeonDecoration.alignLookDirectionToTile)
						{
							Instantiate(decorationObject, t.position, Quaternion.LookRotation(-t.forward));
						}
						else
						{
							Instantiate(decorationObject, t.position, Quaternion.Euler(new Vector3(0, Random.value * 360f, 0)));
						}
					}
				}
			}
		}
	}

	[System.Serializable]
	internal struct DungeonDecoration
	{
		public GameObject decoration;
		public EDungeonTileType decorationType;
		public float chance;
		public bool alignLookDirectionToTile;
	}

	internal enum EDungeonTileType
	{
		floor,
		wall,
		roof
	}
}