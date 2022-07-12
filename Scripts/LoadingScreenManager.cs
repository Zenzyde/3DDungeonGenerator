using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenManager : MonoBehaviour
{
	[SerializeField] RectTransform loadingImageParent;
	[SerializeField] Canvas instructionCanvas;

	private DungeonGenerator3D dungeonGenerator;

	// Start is called before the first frame update
	IEnumerator Start()
	{
		dungeonGenerator = FindObjectOfType<DungeonGenerator3D>();

		while (!dungeonGenerator.DUNGEONDONE)
		{
			loadingImageParent.Rotate(new Vector3(0, 0, 1));
			yield return null;
		}

		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;

		instructionCanvas.gameObject.SetActive(true);

		Destroy(gameObject);
	}
}