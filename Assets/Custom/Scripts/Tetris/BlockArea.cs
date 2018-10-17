﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using gs = GameSettings;

// responsible for layed blocks and the planting floor

public class BlockArea : MonoBehaviour {
	public static BlockArea main;

	public static GameObject[,,] blocks; // [x, y, z]

	// 0 = unoccupied
	// -1 = occupied by clear layer
	// n = occupied by block with id n
	public static int [,,] occupied;

	static float heightOffset, corner;


	void Start () {
		main = this;

		blocks = new GameObject[gs.areaSize, gs.fullAreaHeight, gs.areaSize];
		occupied = new int[gs.areaSize, gs.fullAreaHeight, gs.areaSize];

		heightOffset = gs.floorHeight + gs.blockSize;
		corner = -(gs.areaSize - 1) * gs.blockSize / 2;
	}

	public static IntegerVector3 WorldSpaceToIndex (Vector3 position, bool floorHeight = true) {
		int x = Mathf.RoundToInt ((position.x - corner) / gs.blockSize);
		int y;
		int z = Mathf.RoundToInt ((position.z - corner) / gs.blockSize);

		float floatY = (position.y - heightOffset) / gs.blockSize;
		if (floorHeight)
			y = Mathf.FloorToInt (floatY);
		else
			y = Mathf.RoundToInt (floatY);

		return new IntegerVector3 (x, y, z);
	}
	public static Vector3 IndexToWorldSpace (IntegerVector3 index) {
		float x = corner + gs.blockSize * index.x;
		float y = heightOffset + gs.blockSize * index.y;
		float z = corner + gs.blockSize * index.z;

		return new Vector3(x, y, z);
	}

	public static IntegerVector3 Clamp (IntegerVector3 index) {
		index.x = Mathf.Clamp (index.x, 0, gs.areaSize - 1);
		index.z = Mathf.Clamp (index.z, 0, gs.areaSize - 1);
		return index;
	}

	public static bool IsCubeOver (IntegerVector3 index) {
		return index.y >= gs.areaHeight;
	}
	public static bool IsCubeOverfull (IntegerVector3 index) {
		return index.y >= gs.fullAreaHeight;
	}
	public static bool IsCubeUnder (IntegerVector3 index) {
		return index.y < 0;
	}
	public static bool IsCubeInPlanarBounds (IntegerVector3 index) {
		return index.x >= 0 && index.x < gs.areaSize &&
			index.z >= 0 && index.z < gs.areaSize;
	}
	public static bool IsCubeInBounds (IntegerVector3 index) {
		return IsCubeInPlanarBounds (index) &&
			index.y >= 0; 
	}

	public static bool IsCubeOccupied (IntegerVector3 index) {
		if (IsCubeOverfull (index))
			return false;

		if (IsCubeUnder (index))
			return true;

		if (!IsCubeInBounds (index))
			return true;
		
		return index.At (occupied) != 0;
	}

	public static bool IsOccupied (IntegerVector3 anchor, List<IntegerVector3> structure, Quaternion rotation) {
		foreach (IntegerVector3 cube in structure) {
			if (IsCubeOccupied (anchor + rotation * cube)) {
				return true;
			}
		}
		return false;
	}

	public static bool IsOver (IntegerVector3 anchor, List<IntegerVector3> structure, Quaternion rotation) {
		foreach (IntegerVector3 cube in structure) {
			if (IsCubeOver (anchor + rotation * cube)) {
				return true;
			}
		}
		return false;
	}

	static void GeneralPlace (Block block, int id) {
		foreach (GameObject cube in block.cubes) {
			IntegerVector3 index = WorldSpaceToIndex (cube.transform.position, false);
			if (index.inBounds (occupied)) {
				index.Set (occupied, id);
			}
		}
	}

	public static void Place (Block block) {
		GeneralPlace (block, block.id);
	}
	public static void Unplace (Block block) {
		GeneralPlace (block, 0);
	}

	public static void Plant (Block block) {
		BlockManager.Remove (block);

		foreach (GameObject cube in block.cubes) {
			cube.transform.SetParent (null);
			IntegerVector3 index = WorldSpaceToIndex (cube.transform.position, false);
			index.Set (blocks, cube);

			if (index.At (occupied) != block.id)
				Debug.Log (string.Format("! {0}", index.At (occupied)));
		}

		Object.Destroy (block.gameObject);

		RemovePlanes ();

		if (IsFull()) {
			Game.EndGame ();
			Game.StartGame ();
		}
	}


	public static bool IsFull () {
		int nLayers = 0;

		for (int y = 0; y < gs.fullAreaHeight; y++) {
			bool clear = true;

			for (int x = 0; x < gs.areaSize; x++) {
				for (int z = 0; z < gs.areaSize; z++) {
					if (occupied [x, y, z] > 0) {
						clear = false;
						break;
					}
				}
				if (!clear)
					break;
			}

			if (!clear)
				nLayers++;
		}
		return nLayers > gs.areaHeight;
	}


	public static void RemovePlanes () {
		List<int> removeQueue = new List<int> ();

		for (int y = gs.fullAreaHeight - 1; y > -1; y--) {
			bool full = true;

			for (int x = 0; x < gs.areaSize; x++) {
				for (int z = 0; z < gs.areaSize; z++) {
					full &= occupied [x, y, z] > 0;
				}
			}

			if (full) {
				Spawner.SpeedUp();
				removeQueue.Add (y);

				for (int x = 0; x < gs.areaSize; x++) {
					for (int z = 0; z < gs.areaSize; z++) {
						occupied [x, y, z] = -1;
					}
				}
			}
		}
		Game.AddScore (removeQueue.Count);

		BlockArea.main.StartCoroutine("RemoveLayers", removeQueue);
	}

	public static float ClampDimension (float u, int bound) {
		int forwardSize = Mathf.Max (0, bound);
		int backwardSize = Mathf.Max (0, -bound);

		return Mathf.Clamp (u, corner + backwardSize * gs.blockSize,
			corner + (gs.areaSize - forwardSize - 1) * gs.blockSize);
	}

	public static float SnapDimension (float u) {
		return corner + gs.blockSize * Mathf.Round ((u - corner) / gs.blockSize);
	}

	// TODO: rewrite this method
	public IEnumerator RemoveLayers (List<int> layers) {
		foreach (int y in layers) {
			for (int x = 0; x < gs.areaSize; x++) {
				for (int z = 0; z < gs.areaSize; z++) {
					blocks [x, y, z].GetComponent<MeshRenderer> ().material.color = Utility.Array.RandElement(LayerClearEffect.main.colors);
				}
			}
		}

		yield return new WaitForSeconds(0.25f);

		foreach (int layer in layers) {
			int y = layer;

			for (int x = 0; x < gs.areaSize; x++) {
				for (int z = 0; z < gs.areaSize; z++) {
					occupied [x, y, z] = 0;
					GameObject.Destroy(blocks [x, y, z]);
					blocks [x, y, z] = null;

					LayerClearEffect.main.Emit (IndexToWorldSpace(new IntegerVector3(x, y, z)));
				}
			}
			y++;
			while (y < gs.fullAreaHeight) {
				for (int x = 0; x < gs.areaSize; x++) {
					for (int z = 0; z < gs.areaSize; z++) {
						occupied[x, y-1, z] = occupied[x, y, z];
						occupied[x, y, z] = 0;

						blocks [x, y - 1, z] = blocks [x, y, z];
						blocks [x, y, z] = null;
						if (blocks [x, y - 1, z] != null) {
							LayerClearEffect.main.Drop (blocks [x, y - 1, z]);
						}
					}
				}
				y++;
			}
		}
	}

	public static void Reset () {

		// Remove all blocks from the block area
		for (int x = 0; x < gs.areaSize; x++) {
			for (int y = 0; y < gs.fullAreaHeight; y++) {
				for (int z = 0; z < gs.areaSize; z++) {
					if (occupied [x, y, z] != 0) {
						Object.Destroy (BlockArea.blocks [x, y, z]);

						blocks [x, y, z] = null;
						occupied [x, y, z] = 0;
					}
				}
			}
		}

	}
}
